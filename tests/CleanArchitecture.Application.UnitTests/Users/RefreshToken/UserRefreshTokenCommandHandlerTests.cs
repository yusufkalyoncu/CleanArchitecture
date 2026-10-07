using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Users.RefreshToken;
using CleanArchitecture.Domain.Users;
using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Users.RefreshToken;

public class UserRefreshTokenCommandHandlerTests
{
    private readonly IUserContext _userContextMock;
    private readonly IApplicationDbContext _dbContextMock;
    private readonly ITokenProvider _tokenProviderMock;
    private readonly ISessionService _sessionServiceMock;
    private readonly UserRefreshTokenCommandHandler _handler;

    public UserRefreshTokenCommandHandlerTests()
    {
        _userContextMock = Substitute.For<IUserContext>();
        _dbContextMock = Substitute.For<IApplicationDbContext>();
        _tokenProviderMock = Substitute.For<ITokenProvider>();
        _sessionServiceMock = Substitute.For<ISessionService>();

        _handler = new UserRefreshTokenCommandHandler(
            _userContextMock,
            _dbContextMock,
            _tokenProviderMock,
            _sessionServiceMock);
    }

    [Fact]
    public async Task Handle_WhenTokenConsumptionFails_ShouldReturnInvalidTokenError()
    {
        // Arrange
        var command = new UserRefreshTokenCommand("old-refresh-token");
        
        _userContextMock.Id.Returns(Guid.NewGuid());
        _userContextMock.Jti.Returns("current-jti");

        _sessionServiceMock.ConsumeRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ConsumeResult(false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Auth.InvalidToken);
    }

    [Fact]
    public async Task Handle_WhenCacheHits_ShouldReturnCachedTokensAndNotRotate()
    {
        // Arrange
        var command = new UserRefreshTokenCommand("old-refresh-token");
        
        _userContextMock.Id.Returns(Guid.NewGuid());
        _userContextMock.Jti.Returns("current-jti");

        var cachedResult = new ConsumeResult(true, "cached-access", "cached-refresh");
        _sessionServiceMock.ConsumeRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(cachedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.AccessToken.Should().Be("cached-access");
        result.Data.RefreshToken.Should().Be("cached-refresh");
        
        await _sessionServiceMock.DidNotReceive().RotateSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WhenCacheMissesAndUserExists_ShouldRotateSessionAndReturnNewTokens()
    {
        // Arrange
        var command = new UserRefreshTokenCommand("old-refresh-token");
        var user = User.Create("test@example.com", "John", "Doe", "Password123!").Data;
        var userId = user.Id;
        
        _userContextMock.Id.Returns(userId);
        _userContextMock.Jti.Returns("current-jti");

        var consumeResult = new ConsumeResult(true, string.Empty, string.Empty);
        _sessionServiceMock.ConsumeRefreshTokenAsync(userId, "current-jti", "old-refresh-token")
            .Returns(consumeResult);
        
        var mockDbSet = new List<User> { user }.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        var expectedJti = "new-jti";
        var expectedAccessToken = "new-access";
        var expectedRefreshToken = "new-refresh";

        _tokenProviderMock.CreateAccessToken(user).Returns((expectedJti, expectedAccessToken));
        _tokenProviderMock.CreateRefreshToken().Returns(expectedRefreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.AccessToken.Should().Be(expectedAccessToken);
        result.Data.RefreshToken.Should().Be(expectedRefreshToken);

        await _sessionServiceMock.Received(1).RotateSessionAsync(
            userId,
            "current-jti",
            expectedJti,
            expectedAccessToken,
            expectedRefreshToken);
    }
}