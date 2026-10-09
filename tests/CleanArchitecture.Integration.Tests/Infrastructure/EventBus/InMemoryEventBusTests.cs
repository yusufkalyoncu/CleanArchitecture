using CleanArchitecture.Infrastructure.EventBus;
using CleanArchitecture.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanArchitecture.Integration.Tests.Infrastructure.EventBus;

public class InMemoryEventBusTests
{
    private sealed record TestEvent : IIntegrationEvent
    {
        public static string EventType => "test.event.v1";
    }

    private sealed class HandlerTracker
    {
        public List<Guid> ReceivedMessageIds { get; } = new();
        public int InvocationCount => ReceivedMessageIds.Count;
        public bool ShouldThrow { get; init; }
    }

    private sealed class TestEventHandler1(HandlerTracker tracker, MessageContext context) : IIntegrationEventHandler<TestEvent>
    {
        public Task Handle(TestEvent @event, CancellationToken cancellationToken = default)
        {
            tracker.ReceivedMessageIds.Add(context.MessageId);
            if (tracker.ShouldThrow) throw new InvalidOperationException("Handler1 failed");
            return Task.CompletedTask;
        }
    }

    private sealed class TestEventHandler2(HandlerTracker tracker, MessageContext context) : IIntegrationEventHandler<TestEvent>
    {
        public Task Handle(TestEvent @event, CancellationToken cancellationToken = default)
        {
            tracker.ReceivedMessageIds.Add(context.MessageId);
            if (tracker.ShouldThrow) throw new FormatException("Handler2 failed");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_Should_InvokeAllHandlers_And_InjectMessageContext()
    {
        // Arrange
        var registry = new IntegrationEventHandlerRegistry();
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1));
        registry.Register(typeof(TestEvent), typeof(TestEventHandler2));

        var tracker1 = new HandlerTracker();
        var tracker2 = new HandlerTracker();

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<ILogger<InMemoryEventBus>>(NullLogger<InMemoryEventBus>.Instance);
        services.AddScoped<MessageContext>();
        
        // Pass the trackers directly
        services.AddSingleton(_ => tracker1);
        services.AddSingleton(_ => tracker2);

        // Need keyed services matching the handler type
        services.AddKeyedScoped<IIntegrationEventHandler<TestEvent>, TestEventHandler1>(typeof(TestEventHandler1), (sp, _) => new TestEventHandler1(tracker1, sp.GetRequiredService<MessageContext>()));
        services.AddKeyedScoped<IIntegrationEventHandler<TestEvent>, TestEventHandler2>(typeof(TestEventHandler2), (sp, _) => new TestEventHandler2(tracker2, sp.GetRequiredService<MessageContext>()));

        await using var provider = services.BuildServiceProvider();
        var eventBus = new InMemoryEventBus(provider, registry, provider.GetRequiredService<ILogger<InMemoryEventBus>>());

        var testEvent = new TestEvent();
        var messageId = Guid.NewGuid();

        // Act
        await eventBus.PublishAsync(testEvent, messageId);

        // Assert
        tracker1.InvocationCount.Should().Be(1);
        tracker1.ReceivedMessageIds[0].Should().Be(messageId);

        tracker2.InvocationCount.Should().Be(1);
        tracker2.ReceivedMessageIds[0].Should().Be(messageId);
    }

    [Fact]
    public async Task PublishAsync_Should_ReturnSilently_WhenNoHandlersRegistered()
    {
        // Arrange
        var registry = new IntegrationEventHandlerRegistry();
        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddScoped<MessageContext>();
        await using var provider = services.BuildServiceProvider();
        
        var eventBus = new InMemoryEventBus(provider, registry, NullLogger<InMemoryEventBus>.Instance);

        // Act
        var act = async () => await eventBus.PublishAsync(new TestEvent(), Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_Should_UnwrapAndThrowInnerException_WhenSingleHandlerFails()
    {
        // Arrange
        var registry = new IntegrationEventHandlerRegistry();
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1));

        var tracker1 = new HandlerTracker { ShouldThrow = true };

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddScoped<MessageContext>();
        services.AddKeyedScoped<IIntegrationEventHandler<TestEvent>, TestEventHandler1>(typeof(TestEventHandler1), (sp, _) => new TestEventHandler1(tracker1, sp.GetRequiredService<MessageContext>()));

        await using var provider = services.BuildServiceProvider();
        var eventBus = new InMemoryEventBus(provider, registry, NullLogger<InMemoryEventBus>.Instance);

        // Act
        var act = async () => await eventBus.PublishAsync(new TestEvent(), Guid.NewGuid());

        // Assert - Should NOT be TargetInvocationException, but the real InvalidOperationException
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.WithMessage("Handler1 failed");
    }

    [Fact]
    public async Task PublishAsync_Should_ThrowAggregateException_WhenMultipleHandlersFail()
    {
        // Arrange
        var registry = new IntegrationEventHandlerRegistry();
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1));
        registry.Register(typeof(TestEvent), typeof(TestEventHandler2));

        var tracker1 = new HandlerTracker { ShouldThrow = true };
        var tracker2 = new HandlerTracker { ShouldThrow = true };

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddScoped<MessageContext>();
        services.AddKeyedScoped<IIntegrationEventHandler<TestEvent>, TestEventHandler1>(typeof(TestEventHandler1), (sp, _) => new TestEventHandler1(tracker1, sp.GetRequiredService<MessageContext>()));
        services.AddKeyedScoped<IIntegrationEventHandler<TestEvent>, TestEventHandler2>(typeof(TestEventHandler2), (sp, _) => new TestEventHandler2(tracker2, sp.GetRequiredService<MessageContext>()));

        await using var provider = services.BuildServiceProvider();
        var eventBus = new InMemoryEventBus(provider, registry, NullLogger<InMemoryEventBus>.Instance);

        // Act
        var act = async () => await eventBus.PublishAsync(new TestEvent(), Guid.NewGuid());

        // Assert
        var exception = await act.Should().ThrowAsync<AggregateException>();
        exception.Which.InnerExceptions.Should().HaveCount(2);
        exception.Which.InnerExceptions[0].Should().BeOfType<InvalidOperationException>();
        exception.Which.InnerExceptions[1].Should().BeOfType<FormatException>();
    }
}