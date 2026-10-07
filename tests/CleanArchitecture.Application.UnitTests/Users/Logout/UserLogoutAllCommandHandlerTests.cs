using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users.Logout;
using FluentAssertions;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Users.Logout;

public class UserLogoutAllCommandHandlerTests
{
    private readonly IUserContext _userContextMock;
    private readonly ISessionService _sessionServiceMock;
    private readonly UserLogoutAllCommandHandler _handler;

    public UserLogoutAllCommandHandlerTests()
    {
        _userContextMock = Substitute.For<IUserContext>();
        _sessionServiceMock = Substitute.For<ISessionService>();

        _handler = new UserLogoutAllCommandHandler(_userContextMock, _sessionServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldRevokeAllSessionsForUser()
    {
        // Arrange
        var command = new UserLogoutAllCommand();
        var userId = Guid.NewGuid();
        
        _userContextMock.Id.Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _sessionServiceMock.Received(1).RevokeAllSessionsAsync(userId);
    }
}