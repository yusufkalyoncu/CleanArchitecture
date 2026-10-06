using System.Text.Json;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Shared;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
    NpgsqlDataSource dataSource,
    IEventBus eventBus,
    OutboxEventTypeRegistry registry,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private readonly OutboxOptions _options = options.Value;

    /// <summary>
    /// Processes a single batch of outbox messages:
    /// 1. Atomically claims messages (leases) using UPDATE ... RETURNING.
    /// 2. Dispatches handlers concurrently outside a transaction.
    /// 3. Persists all results in a single bulk UPDATE.
    /// Note: Does not guarantee ordered execution.
    /// </summary>
    public async Task<int> ProcessBatchAsync(CancellationToken stoppingToken)
    {
        // ── 1. CLAIM ──────────────────────────────────────────────────────────
        var messages = await ClaimBatchAsync(stoppingToken);

        if (messages.Count == 0)
            return 0;

        // ── 2. DISPATCH ───────────────────────────────────────────────────────
        // null = message processing was cancelled due to shutdown.
        var results = new OutboxUpdateResult?[messages.Count];

        // We DO NOT pass stoppingToken to ParallelOptions.
        // If we did, cancellation would throw an exception, bypassing the write phase
        // and causing successfully processed messages to be lost and re-processed.
        // Instead, we manually check the token inside the loop.
        await Parallel.ForEachAsync(
            messages.Select((msg, idx) => (msg, idx)),
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism },
            async (item, _) =>
            {
                // Graceful shutdown: skip processing, leave result as null to release the lock.
                if (stoppingToken.IsCancellationRequested)
                    return;

                // Link the message timeout with the global shutdown token.
                using var msgCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                msgCts.CancelAfter(_options.MessageTimeout);

                results[item.idx] = await ProcessSingleMessageAsync(item.msg, msgCts.Token, stoppingToken);
            });

        // Separate completed and incomplete messages.
        var completed = new List<OutboxUpdateResult>();
        var incompleteIds = new List<Guid>();

        for (int i = 0; i < messages.Count; i++)
        {
            if (results[i].HasValue)
                completed.Add(results[i]!.Value);
            else
                incompleteIds.Add(messages[i].Id);
        }

        // Persist completed messages regardless of shutdown using CancellationToken.None.
        if (completed.Count > 0)
            await WriteResultsAsync(completed, CancellationToken.None);

        // Immediately release locks for incomplete messages so other instances can claim them.
        if (incompleteIds.Count > 0)
            await ReleaseLockAsync([.. incompleteIds], CancellationToken.None);

        if (logger.IsEnabled(LogLevel.Information))
        {
            var successCount = completed.Count(r => r.IsSuccess);
            var failCount = completed.Count(r => r.IsRetry);
            var deadCount = completed.Count(r => r.IsDeadLettered);

            logger.LogInformation(
                "Outbox batch done. Total={Total} Success={Success} Retry={Retry} DeadLetter={Dead} Incomplete={Incomplete}",
                messages.Count, successCount, failCount, deadCount, incompleteIds.Count);
        }

        return messages.Count;
    }

    // ── CLAIM ─────────────────────────────────────────────────────────────────

    private async Task<List<OutboxMessageRow>> ClaimBatchAsync(CancellationToken ct)
    {
        // Atomically claims a batch using FOR UPDATE SKIP LOCKED to prevent locking contention.
        const string sql = """
            UPDATE public.outbox_messages
            SET locked_until_utc = now() + make_interval(secs => @LockTimeoutSeconds::float)
            WHERE id IN (
                SELECT id FROM public.outbox_messages
                WHERE processed_on_utc IS NULL
                  AND dead_lettered_on_utc IS NULL
                  AND (locked_until_utc IS NULL OR locked_until_utc < now())
                  AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= now())
                ORDER BY occurred_on_utc
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, type, content, retry_count AS RetryCount
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<OutboxMessageRow>(sql, new
        {
            _options.LockTimeoutSeconds,
            _options.BatchSize
        });

        return rows.ToList();
    }

    // ── PROCESS SINGLE MESSAGE ────────────────────────────────────────────────

    private async Task<OutboxUpdateResult?> ProcessSingleMessageAsync(
        OutboxMessageRow message,
        CancellationToken msgCt,
        CancellationToken stoppingToken)
    {
        try
        {
            var msgType = registry.Resolve(message.Type);

            // Poison message: type not found. Dead-letter immediately.
            if (msgType is null)
            {
                logger.LogError(
                    "Outbox message {Id} type '{Type}' not found in registry. Dead-lettering without retry.",
                    message.Id, message.Type);

                return OutboxUpdateResult.DeadLetter(message.Id, message.RetryCount,
                    $"Type not found in registry: {message.Type}");
            }

            IIntegrationEvent? integrationEvent;
            try
            {
                var deserialized = JsonSerializer.Deserialize(message.Content, msgType, OutboxJsonOptions.Default);

                // Poison message: deserialized object is not an IIntegrationEvent.
                if (deserialized is not IIntegrationEvent ev)
                {
                    logger.LogError(
                        "Outbox message {Id} deserialized to non-IIntegrationEvent. Dead-lettering.",
                        message.Id);

                    return OutboxUpdateResult.DeadLetter(message.Id, message.RetryCount,
                        $"Deserialized object is not IIntegrationEvent. Type: {message.Type}");
                }

                integrationEvent = ev;
            }
            catch (Exception ex) when (IsDeserializationPoison(ex))
            {
                // Poison message: deserialization failed. Dead-letter immediately.
                logger.LogError(ex, "Outbox message {Id} deserialization failed (poison). Dead-lettering.", message.Id);
                return OutboxUpdateResult.DeadLetter(message.Id, message.RetryCount, Truncate(ex.ToString()));
            }

            // Dispatch to handlers. Exceptions here are treated as transient failures.
            await eventBus.PublishAsync(integrationEvent, message.Id, msgCt);

            // Success: preserve existing retry_count for diagnostics.
            return OutboxUpdateResult.Success(message.Id, message.RetryCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown: return null to release the lock without incrementing retry_count.
            return null;
        }
        catch (Exception ex)
        {
            var newRetryCount = message.RetryCount + 1;
            var error = Truncate(ex.ToString());

            if (newRetryCount >= _options.MaxRetryCount)
            {
                logger.LogError(ex,
                    "Outbox message {Id} type '{Type}' dead-lettered after {RetryCount} retries.",
                    message.Id, message.Type, newRetryCount);

                return OutboxUpdateResult.DeadLetter(message.Id, newRetryCount, error);
            }

            // Calculate backoff in seconds. Added to the DB clock via make_interval in SQL.
            int backoffSeconds = (int)Math.Min(
                newRetryCount * _options.BackoffStep.TotalSeconds,
                _options.MaxBackoff.TotalSeconds);

            logger.LogWarning(
                "Outbox message {Id} type '{Type}' failed (retry {RetryCount}/{MaxRetry}), backoff={Backoff}s.",
                message.Id, message.Type, newRetryCount, _options.MaxRetryCount, backoffSeconds);

            return OutboxUpdateResult.Retry(message.Id, newRetryCount, backoffSeconds, error);
        }
    }

    /// <summary>
    /// Identifies deterministic failures that should never be retried.
    /// </summary>
    private static bool IsDeserializationPoison(Exception ex) => ex is JsonException or NotSupportedException;

    // ── WRITE RESULTS ─────────────────────────────────────────────────────────

    private async Task WriteResultsAsync(IReadOnlyList<OutboxUpdateResult> results, CancellationToken ct)
    {
        // Bulk UNNEST UPDATE for all result types (success/retry/dead-letter).
        // Uses DB clock (now()) for timestamps to avoid clock skew issues.
        // Includes basic lease protection in the WHERE clause.
        const string sql = """
            UPDATE public.outbox_messages AS m
            SET processed_on_utc    = CASE WHEN u.is_success  THEN now()                               ELSE m.processed_on_utc    END,
                dead_lettered_on_utc = CASE WHEN u.is_dead    THEN now()                               ELSE m.dead_lettered_on_utc END,
                next_attempt_at_utc  = CASE WHEN u.is_retry   THEN now() + make_interval(secs => u.backoff_seconds::float)
                                            ELSE m.next_attempt_at_utc END,
                error               = u.error::text,
                retry_count         = u.retry_count::int,
                locked_until_utc    = NULL
            FROM (
                SELECT * FROM UNNEST(
                    @Ids::uuid[],
                    @Errors::text[],
                    @RetryCounts::int[],
                    @BackoffSeconds::int[],
                    @IsSuccess::boolean[],
                    @IsRetry::boolean[],
                    @IsDead::boolean[]
                ) AS t(id, error, retry_count, backoff_seconds, is_success, is_retry, is_dead)
            ) AS u
            WHERE m.id = u.id
              AND m.processed_on_utc IS NULL
              AND m.dead_lettered_on_utc IS NULL
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(sql, new
        {
            Ids         = results.Select(r => r.Id).ToArray(),
            Errors      = results.Select(r => r.Error).ToArray(),
            RetryCounts = results.Select(r => r.RetryCount).ToArray(),
            BackoffSeconds = results.Select(r => r.BackoffSeconds).ToArray(),
            IsSuccess   = results.Select(r => r.IsSuccess).ToArray(),
            IsRetry     = results.Select(r => r.IsRetry).ToArray(),
            IsDead      = results.Select(r => r.IsDeadLettered).ToArray()
        });
    }

    private async Task ReleaseLockAsync(Guid[] ids, CancellationToken ct)
    {
        const string sql = """
            UPDATE public.outbox_messages
            SET locked_until_utc = NULL
            WHERE id = ANY(@Ids::uuid[])
              AND processed_on_utc IS NULL
              AND dead_lettered_on_utc IS NULL
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(sql, new { Ids = ids });
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private static string Truncate(string s, int maxLength = 4000) =>
        s.Length <= maxLength ? s : s[..maxLength];

    // ── INTERNAL TYPES ────────────────────────────────────────────────────────

    private sealed record OutboxMessageRow(Guid Id, string Type, string Content, int RetryCount);

    private readonly record struct OutboxUpdateResult(
        Guid Id,
        string? Error,
        int RetryCount,
        int BackoffSeconds,
        bool IsSuccess,
        bool IsRetry,
        bool IsDeadLettered)
    {
        /// <summary>
        /// Success result. Preserves existing retryCount for diagnostics.
        /// </summary>
        public static OutboxUpdateResult Success(Guid id, int retryCount) =>
            new(id, null, retryCount, 0, true, false, false);

        public static OutboxUpdateResult Retry(Guid id, int retryCount, int backoffSeconds, string error) =>
            new(id, error, retryCount, backoffSeconds, false, true, false);

        /// <summary>
        /// Dead-letter result.
        /// </summary>
        public static OutboxUpdateResult DeadLetter(Guid id, int retryCount, string error) =>
            new(id, error, retryCount, 0, false, false, true);
    }
}