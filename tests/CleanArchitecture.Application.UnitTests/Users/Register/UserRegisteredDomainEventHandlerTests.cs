using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Application.Users.Register.EventHandlers;
using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Domain.Users.Events;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Users.Register;

public class UserRegisteredDomainEventHandlerTests
{
    private readonly IOutboxService _outboxServiceMock;
    private readonly UserRegisteredDomainEventHandler _handler;

    public UserRegisteredDomainEventHandlerTests()
    {
        _outboxServiceMock = Substitute.For<IOutboxService>();
        _handler = new UserRegisteredDomainEventHandler(_outboxServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldAddIntegrationEventToOutbox()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var domainEvent = new UserRegisteredDomainEvent(userId);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outboxServiceMock.Received(1).AddAsync(
            Arg.Is<UserRegisteredIntegrationEvent>(e => e.UserId == userId),
            Arg.Any<CancellationToken>());
    }
}