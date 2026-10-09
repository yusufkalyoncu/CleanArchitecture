using CleanArchitecture.Infrastructure.Inbox;
using CleanArchitecture.Shared;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CleanArchitecture.Integration.Tests.Infrastructure.Inbox;

public class InboxHandlerDecoratorTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TestEvent : IIntegrationEvent
    {
        public static string EventType => "test.event.v1";
    }

    private sealed class FakeInnerHandler : IIntegrationEventHandler<TestEvent>
    {
        public bool WasCalled { get; private set; }
        public bool ShouldThrow { get; init; }

        public Task Handle(TestEvent @event, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            if (ShouldThrow)
            {
                throw new Exception("Inner handler failed!");
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_Should_CallInner_And_SaveInboxMessage_WhenMessageIsNew()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var messageContext = new MessageContext { MessageId = messageId };
        var innerHandler = new FakeInnerHandler();
        var consumer = "TestConsumer";
        
        var decorator = new InboxHandlerDecorator<TestEvent>(
            innerHandler,
            consumer,
            DbContext,
            messageContext,
            new NullLogger<InboxHandlerDecorator<TestEvent>>());

        // Act
        await decorator.Handle(new TestEvent());

        // Assert
        innerHandler.WasCalled.Should().BeTrue();

        var dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
        
        var inboxCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM inbox_messages WHERE message_id = @Id AND consumer = @Consumer",
            new { Id = messageId, Consumer = consumer });
            
        inboxCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_SkipInner_WhenMessageIsDuplicate()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var consumer = "TestConsumer_Duplicate";
        
        var dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO inbox_messages (message_id, consumer, processed_on_utc) VALUES (@Id, @Consumer, now())",
            new { Id = messageId, Consumer = consumer });

        var messageContext = new MessageContext { MessageId = messageId };
        var innerHandler = new FakeInnerHandler();
        
        var decorator = new InboxHandlerDecorator<TestEvent>(
            innerHandler,
            consumer,
            DbContext,
            messageContext,
            new NullLogger<InboxHandlerDecorator<TestEvent>>());

        // Act
        await decorator.Handle(new TestEvent());

        // Assert
        innerHandler.WasCalled.Should().BeFalse("Duplicate message should not trigger inner handler.");
    }

    [Fact]
    public async Task Handle_Should_RollbackInboxMessage_WhenInnerHandlerThrows()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var consumer = "TestConsumer_Fails";
        var messageContext = new MessageContext { MessageId = messageId };
        
        var innerHandler = new FakeInnerHandler { ShouldThrow = true };
        
        var decorator = new InboxHandlerDecorator<TestEvent>(
            innerHandler,
            consumer,
            DbContext,
            messageContext,
            new NullLogger<InboxHandlerDecorator<TestEvent>>());

        // Act
        var act = async () => await decorator.Handle(new TestEvent());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Handler {consumer} failed for message {messageId}.*");

        var dataSource = ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();
        
        var inboxCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM inbox_messages WHERE message_id = @Id AND consumer = @Consumer",
            new { Id = messageId, Consumer = consumer });
            
        inboxCount.Should().Be(0, "Transaction should be rolled back and the inbox message should not be saved.");
    }

    [Fact]
    public async Task Handle_Should_Throw_WhenTransactionIsAlreadyActive()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var consumer = "TestConsumer_Tx";
        var messageContext = new MessageContext { MessageId = messageId };
        var innerHandler = new FakeInnerHandler();
        
        var decorator = new InboxHandlerDecorator<TestEvent>(
            innerHandler,
            consumer,
            DbContext,
            messageContext,
            new NullLogger<InboxHandlerDecorator<TestEvent>>());

        await using var externalTransaction = await DbContext.Database.BeginTransactionAsync();

        // Act
        var act = async () => await decorator.Handle(new TestEvent());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("InboxHandlerDecorator must be the outermost transaction owner.");
            
        await externalTransaction.RollbackAsync();
    }
}