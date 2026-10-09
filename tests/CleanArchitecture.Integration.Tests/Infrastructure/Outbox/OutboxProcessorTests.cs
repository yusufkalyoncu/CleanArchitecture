using System.Text.Json;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Infrastructure.Outbox;
using CleanArchitecture.Shared;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Outbox;

public class OutboxProcessorTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed class FakeEventBus : IEventBus
    {
        public bool ShouldThrow { get; init; }
        public int PublishCount { get; private set; }

        public Task PublishAsync(IIntegrationEvent integrationEvent, Guid messageId, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            if (ShouldThrow)
            {
                throw new Exception("EventBus simulated failure.");
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_Should_MarkMessageAsProcessed_WhenEventBusSucceeds()
    {
        // Arrange
        var dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var registry = ServiceProvider.GetRequiredService<OutboxEventTypeRegistry>();
        var options = ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
        var fakeEventBus = new FakeEventBus();

        var processor = new OutboxProcessor(
            dataSource,
            fakeEventBus,
            registry,
            options,
            new NullLogger<OutboxProcessor>());

        var messageId = Guid.NewGuid();
        var content = JsonSerializer.Serialize(new UserRegisteredIntegrationEvent(Guid.NewGuid()), OutboxJsonOptions.Default);

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO public.outbox_messages (id, type, content, occurred_on_utc, retry_count) VALUES (@Id, 'identity.user-registered.v1', @Content::jsonb, now(), 0)",
            new { Id = messageId, Content = content });

        // Act
        var processedCount = await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert
        processedCount.Should().Be(1);
        fakeEventBus.PublishCount.Should().Be(1);

        var processedOnUtc = await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT processed_on_utc FROM public.outbox_messages WHERE id = @Id",
            new { Id = messageId });
            
        processedOnUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessBatchAsync_Should_IncrementRetryCount_And_SetNextAttempt_WhenEventBusThrows()
    {
        // Arrange
        var dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var registry = ServiceProvider.GetRequiredService<OutboxEventTypeRegistry>();
        var options = ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
        var fakeEventBus = new FakeEventBus { ShouldThrow = true };

        var processor = new OutboxProcessor(
            dataSource,
            fakeEventBus,
            registry,
            options,
            new NullLogger<OutboxProcessor>());

        var messageId = Guid.NewGuid();
        var content = JsonSerializer.Serialize(new UserRegisteredIntegrationEvent(Guid.NewGuid()), OutboxJsonOptions.Default);

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO public.outbox_messages (id, type, content, occurred_on_utc, retry_count) VALUES (@Id, 'identity.user-registered.v1', @Content::jsonb, now(), 0)",
            new { Id = messageId, Content = content });

        // Act
        var processedCount = await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert
        processedCount.Should().Be(1);
        fakeEventBus.PublishCount.Should().Be(1);

        var result = await connection.QuerySingleAsync(
            "SELECT processed_on_utc, dead_lettered_on_utc, retry_count, next_attempt_at_utc, error FROM public.outbox_messages WHERE id = @Id",
            new { Id = messageId });

        ((DateTime?)result.processed_on_utc).Should().BeNull();
        ((DateTime?)result.dead_lettered_on_utc).Should().BeNull();
        ((int)result.retry_count).Should().Be(1);
        ((DateTime?)result.next_attempt_at_utc).Should().NotBeNull();
        ((string)result.error).Should().Contain("EventBus simulated failure.");
    }

    [Fact]
    public async Task ProcessBatchAsync_Should_DeadLetter_WhenDeserializationFails()
    {
        // Arrange
        var dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var registry = ServiceProvider.GetRequiredService<OutboxEventTypeRegistry>();
        var options = ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
        var fakeEventBus = new FakeEventBus();

        var processor = new OutboxProcessor(
            dataSource,
            fakeEventBus,
            registry,
            options,
            new NullLogger<OutboxProcessor>());

        var messageId = Guid.NewGuid();
        var badContent = "{ \"userId\": \"not-a-guid\" }"; // Valid JSON, but fails deserialization to Guid

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO public.outbox_messages (id, type, content, occurred_on_utc, retry_count) VALUES (@Id, 'identity.user-registered.v1', @Content::jsonb, now(), 0)",
            new { Id = messageId, Content = badContent });

        // Act
        var processedCount = await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert
        processedCount.Should().Be(1);
        fakeEventBus.PublishCount.Should().Be(0); // Bus not called

        var result = await connection.QuerySingleAsync(
            "SELECT processed_on_utc, dead_lettered_on_utc, error FROM public.outbox_messages WHERE id = @Id",
            new { Id = messageId });

        ((DateTime?)result.processed_on_utc).Should().BeNull();
        ((DateTime?)result.dead_lettered_on_utc).Should().NotBeNull();
        ((string)result.error).Should().Contain("JsonException");
    }
}