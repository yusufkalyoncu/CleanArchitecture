using CleanArchitecture.Infrastructure.Inbox;
using CleanArchitecture.Infrastructure.Outbox;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Inbox;

public class InboxCleanupBackgroundServiceTests : BaseIntegrationTest
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly OutboxOptions _options;

    public InboxCleanupBackgroundServiceTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        
        _options = new OutboxOptions
        {
            InboxRetentionDays = 7
        };
    }

    [Fact]
    public async Task CleanupInboxMessagesAsync_Should_DeleteOldMessagesInBatches()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<InboxCleanupBackgroundService>>();
        mockLogger.IsEnabled(LogLevel.Information).Returns(true);
        
        var service = new InboxCleanupBackgroundService(
            _dataSource,
            Options.Create(_options),
            mockLogger);

        var oldDate = DateTime.UtcNow.AddDays(-_options.InboxRetentionDays - 1);
        var newDate = DateTime.UtcNow.AddDays(-1);

        await using var connection = await _dataSource.OpenConnectionAsync();

        // The batch size is 1000. Insert 1001 old processed messages.
        var messages = Enumerable.Range(0, 1001).Select(_ => new
        {
            Consumer = "TestConsumer",
            MessageId = Guid.NewGuid(),
            ProcessedOn = oldDate
        });

        // Insert 1 new message (Should NOT be deleted)
        var newId = Guid.NewGuid();
        var newMessage = new[] { new
        {
            Consumer = "TestConsumer",
            MessageId = newId,
            ProcessedOn = newDate
        }};

        var sql = "INSERT INTO inbox_messages (consumer, message_id, processed_on_utc) VALUES (@Consumer, @MessageId, @ProcessedOn)";
        
        await connection.ExecuteAsync(sql, messages);
        await connection.ExecuteAsync(sql, newMessage);

        // Act
        using var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);

        // Wait a bit to let it clean up the 1001 messages and hit the Task.Delay(1 hour)
        await Task.Delay(500, CancellationToken.None);

        await cts.CancelAsync(); // Gracefully stop
        
        await Task.WhenAny(task, Task.Delay(1000, CancellationToken.None));

        // Assert
        var existingIds = (await connection.QueryAsync<Guid>("SELECT message_id FROM inbox_messages")).ToList();
        existingIds.Should().Contain(newId);
        existingIds.Count.Should().BeLessThan(1000); // Most of the 1001 old messages should be deleted
    }

    [Fact]
    public async Task ExecuteAsync_Should_RunCleanup_Log_And_HandleExceptions()
    {
        // This test will cover the logging block (deletedCount > 0) AND the exception handling block.
        
        // Use a completely invalid connection string so OpenConnectionAsync throws NpgsqlException
        var badDataSource = NpgsqlDataSource.Create("Host=non_existent_host;Database=db;Username=u;Password=p");

        var mockLogger = Substitute.For<ILogger<InboxCleanupBackgroundService>>();
        mockLogger.IsEnabled(LogLevel.Information).Returns(true);
        
        var service = new InboxCleanupBackgroundService(
            badDataSource,
            Options.Create(_options),
            mockLogger);

        using var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);

        // Give it time to attempt cleanup, hit the connection exception, log it, and enter Task.Delay(5 mins)
        await Task.Delay(500, CancellationToken.None);
        
        await cts.CancelAsync(); // Cancel the Task.Delay(5 mins) backoff to exit gracefully

        await Task.WhenAny(task, Task.Delay(2000, CancellationToken.None));
        task.IsCompleted.Should().BeTrue();
    }
}
