using Microsoft.Extensions.Logging;
using NSubstitute;
using CleanArchitecture.Infrastructure.Outbox;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Outbox;

public class OutboxCleanupBackgroundServiceTests : BaseIntegrationTest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NpgsqlDataSource _dataSource;
    private readonly OutboxOptions _options;

    public OutboxCleanupBackgroundServiceTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _scopeFactory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        _dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        
        _options = new OutboxOptions
        {
            BatchSize = 10,
            PollingIntervalSeconds = 5,
            RetentionDays = 7
        };
    }

    private OutboxCleanupBackgroundService CreateService(ILogger<OutboxCleanupBackgroundService>? logger = null)
    {
        return new OutboxCleanupBackgroundService(
            _scopeFactory,
            Options.Create(_options),
            logger ?? NullLogger<OutboxCleanupBackgroundService>.Instance);
    }

    [Fact]
    public async Task CleanupAsync_Should_DeleteOnlyOldProcessedAndDeadLetteredMessages()
    {
        // Arrange
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var oldDate = cutoff.AddDays(-1);
        var newDate = cutoff.AddDays(1);

        await using var connection = await _dataSource.OpenConnectionAsync();

        // 1. Old processed message (Should be deleted)
        var id1 = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO outbox_messages (id, type, content, occurred_on_utc, processed_on_utc) VALUES (@Id, 'Test', '{}', @OccurredOn, @ProcessedOn)", 
            new { Id = id1, OccurredOn = oldDate.AddMinutes(-5), ProcessedOn = oldDate });

        // 2. Old dead-lettered message (Should be deleted)
        var id2 = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO outbox_messages (id, type, content, occurred_on_utc, dead_lettered_on_utc) VALUES (@Id, 'Test', '{}', @OccurredOn, @DeadOn)", 
            new { Id = id2, OccurredOn = oldDate.AddMinutes(-5), DeadOn = oldDate });

        // 3. New processed message (Should NOT be deleted)
        var id3 = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO outbox_messages (id, type, content, occurred_on_utc, processed_on_utc) VALUES (@Id, 'Test', '{}', @OccurredOn, @ProcessedOn)", 
            new { Id = id3, OccurredOn = newDate.AddMinutes(-5), ProcessedOn = newDate });

        // 4. Old UNPROCESSED message (Should NOT be deleted)
        var id4 = Guid.NewGuid();
        await connection.ExecuteAsync("INSERT INTO outbox_messages (id, type, content, occurred_on_utc) VALUES (@Id, 'Test', '{}', @OccurredOn)", 
            new { Id = id4, OccurredOn = oldDate });

        var mockLogger = Substitute.For<ILogger<OutboxCleanupBackgroundService>>();
        mockLogger.IsEnabled(LogLevel.Information).Returns(true);
        var service = CreateService(mockLogger);

        // Act
        var deletedCount = await service.CleanupAsync(CancellationToken.None);

        // Assert
        deletedCount.Should().Be(2);

        var existingIds = (await connection.QueryAsync<Guid>("SELECT id FROM outbox_messages")).ToList();
        existingIds.Should().NotContain(id1);
        existingIds.Should().NotContain(id2);
        existingIds.Should().Contain(id3);
        existingIds.Should().Contain(id4);
    }

    [Fact]
    public async Task CleanupAsync_Should_DeleteInBatches()
    {
        // Arrange
        var oldDate = DateTime.UtcNow.AddDays(-_options.RetentionDays - 1);
        await using var connection = await _dataSource.OpenConnectionAsync();

        // BatchSize in code is hardcoded to 500 (CleanupBatchSize)
        // We need to insert 501 messages to trigger the batch loop twice.
        
        var sql = "INSERT INTO outbox_messages (id, type, content, occurred_on_utc, processed_on_utc) VALUES (@Id, 'Test', '{}', @OccurredOn, @ProcessedOn)";
        
        var messages = Enumerable.Range(0, 501).Select(_ => new
        {
            Id = Guid.NewGuid(),
            OccurredOn = oldDate,
            ProcessedOn = oldDate
        });

        // Fast bulk insert using Dapper
        await connection.ExecuteAsync(sql, messages);

        var service = CreateService();

        // Act
        var deletedCount = await service.CleanupAsync(CancellationToken.None);

        // Assert
        deletedCount.Should().BeGreaterOrEqualTo(501); 
        // Note: It might delete more if previous tests left artifacts (though Respawn clears between tests, but just to be safe).
    }

    [Fact]
    public async Task ExecuteAsync_Should_CatchGenericException_And_Continue()
    {
        // To test ExecuteAsync's loop and exception handling, we need a mock IServiceScopeFactory 
        // because we want CleanupAsync to throw an exception when it resolves the DB connection.
        var mockScopeFactory = Substitute.For<IServiceScopeFactory>();
        var mockScope = Substitute.For<IServiceScope>();
        mockScopeFactory.CreateScope().Returns(mockScope);
        
        // Make the service provider throw a random exception to simulate DB failure
        mockScope.ServiceProvider.GetService(typeof(NpgsqlDataSource))
            .Returns(_ => throw new InvalidOperationException("Simulated DB failure"));

        var service = new OutboxCleanupBackgroundService(
            mockScopeFactory,
            Options.Create(_options),
            NullLogger<OutboxCleanupBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();
        
        // Start the service. It will throw inside CleanupAsync, catch it, log it, and hit Task.Delay(24h).
        var task = service.StartAsync(cts.Token);
        
        // Wait just a tiny bit for it to enter the catch block and hit Task.Delay
        await Task.Delay(50, CancellationToken.None);
        
        // Cancel to break out of the 24h delay
        await cts.CancelAsync();
        
        // Wait for it to gracefully exit
        await Task.WhenAny(task, Task.Delay(1000, CancellationToken.None));
        
        // Assert it didn't crash
        task.IsCompleted.Should().BeTrue();
    }
}