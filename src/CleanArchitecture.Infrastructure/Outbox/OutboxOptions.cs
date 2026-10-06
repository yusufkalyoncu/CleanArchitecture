using CleanArchitecture.Shared;
using FluentValidation;

namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxOptions : IAppOption
{
    public static string SectionName => "OutboxOptions";

    /// <summary>Max number of messages claimed per batch.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>Max concurrent handler executions in Parallel.ForEachAsync.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 4;

    /// <summary>Max processing time per message before forcing a timeout.</summary>
    public int MessageTimeoutSeconds { get; init; } = 30;

    /// <summary>Lease duration. Messages are unlocked if processing exceeds this time.</summary>
    public int LockTimeoutSeconds { get; init; } = 420;

    /// <summary>Base backoff step in seconds. Multiplied by the retry count.</summary>
    public int BackoffStepSeconds { get; init; } = 15;

    /// <summary>Maximum backoff duration in seconds.</summary>
    public int MaxBackoffSeconds { get; init; } = 300;

    /// <summary>Max retries before moving the message to the dead-letter state.</summary>
    public int MaxRetryCount { get; init; } = 5;

    /// <summary>Fallback polling interval when no wake-up signals are received.</summary>
    public int PollingIntervalSeconds { get; init; } = 10;

    /// <summary>Number of days to keep processed and dead-lettered messages before cleanup.</summary>
    public int RetentionDays { get; init; } = 7;

    // Computed helpers
    public TimeSpan MessageTimeout => TimeSpan.FromSeconds(MessageTimeoutSeconds);
    public TimeSpan LockTimeout => TimeSpan.FromSeconds(LockTimeoutSeconds);
    public TimeSpan BackoffStep => TimeSpan.FromSeconds(BackoffStepSeconds);
    public TimeSpan MaxBackoff => TimeSpan.FromSeconds(MaxBackoffSeconds);
    public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingIntervalSeconds);
}

internal sealed class OutboxOptionsValidator : AbstractValidator<OutboxOptions>
{
    public OutboxOptionsValidator()
    {
        RuleFor(x => x.BatchSize).InclusiveBetween(1, 1000);
        RuleFor(x => x.MaxDegreeOfParallelism).InclusiveBetween(1, 64);
        RuleFor(x => x.MessageTimeoutSeconds).InclusiveBetween(1, 300);
        RuleFor(x => x.LockTimeoutSeconds).InclusiveBetween(1, 3600);
        RuleFor(x => x.BackoffStepSeconds).InclusiveBetween(1, 300);
        RuleFor(x => x.MaxBackoffSeconds).InclusiveBetween(1, 3600);
        RuleFor(x => x.MaxRetryCount).InclusiveBetween(1, 100);
        RuleFor(x => x.PollingIntervalSeconds).InclusiveBetween(1, 600);
        RuleFor(x => x.RetentionDays).InclusiveBetween(1, 365);

        // Cross-property validation to ensure lease duration covers worst-case batch processing time:
        RuleFor(x => x.LockTimeoutSeconds)
            .Must((options, lockTimeout) =>
            {
                var minLockTimeout = (int)Math.Ceiling((double)options.BatchSize / options.MaxDegreeOfParallelism) * options.MessageTimeoutSeconds;
                return lockTimeout > minLockTimeout;
            })
            .WithMessage(x => 
                $"LockTimeoutSeconds ({x.LockTimeoutSeconds}s) must be greater than " +
                $"ceil(BatchSize / MaxDegreeOfParallelism) * MessageTimeoutSeconds = " +
                $"ceil({x.BatchSize} / {x.MaxDegreeOfParallelism}) * {x.MessageTimeoutSeconds} = " +
                $"{(int)Math.Ceiling((double)x.BatchSize / x.MaxDegreeOfParallelism) * x.MessageTimeoutSeconds}s. " +
                "Otherwise the lease expires before the batch completes and another instance will pick up the same messages.");
    }
}
