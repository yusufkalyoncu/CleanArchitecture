using CleanArchitecture.Application.Users.Register.EventHandlers;
using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Shared;
using NSubstitute;
using FluentAssertions;

namespace CleanArchitecture.Application.UnitTests.Users.Register;

public class SendWelcomeSmsHandlerTests
{
    private readonly IMessageContext _messageContextMock;
    private readonly SendWelcomeSmsHandler _handler;

    public SendWelcomeSmsHandlerTests()
    {
        _messageContextMock = Substitute.For<IMessageContext>();
        _handler = new SendWelcomeSmsHandler(_messageContextMock);
    }

    [Fact]
    public async Task Handle_ShouldRunWithoutExceptions()
    {
        // Arrange
        var integrationEvent = new UserRegisteredIntegrationEvent(Guid.NewGuid());
        _messageContextMock.MessageId.Returns(Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(integrationEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}