using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users.Logout;
using FluentAssertions;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Users.Logout;

public class UserLogoutCommandHandlerTests
{
    private readonly IUserContext _userContextMock;
    private readonly ISessionService _sessionServiceMock;
    private readonly UserLogoutCommandHandler _handler;

    public UserLogoutCommandHandlerTests()
    {
        _userContextMock = Substitute.For<IUserContext>();
        _sessionServiceMock = Substitute.For<ISessionService>();

        _handler = new UserLogoutCommandHandler(_userContextMock, _sessionServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldRevokeCurrentSession()
    {
        // Arrange
        var command = new UserLogoutCommand();
        var userId = Guid.NewGuid();
        var jti = "test-jti";
        var lifetime = TimeSpan.FromMinutes(15);
        
        _userContextMock.Id.Returns(userId);
        _userContextMock.Jti.Returns(jti);
        _userContextMock.AccessTokenRemainingLifetime.Returns(lifetime);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _sessionServiceMock.Received(1).RevokeSessionAsync(userId, jti, lifetime);
    }
}