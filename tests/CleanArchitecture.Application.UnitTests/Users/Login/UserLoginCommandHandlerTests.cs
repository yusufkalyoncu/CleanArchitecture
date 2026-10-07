using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Shared;
using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Users.Login;

public class UserLoginCommandHandlerTests
{
    private readonly IApplicationDbContext _dbContextMock;
    private readonly ITokenProvider _tokenProviderMock;
    private readonly ISessionService _sessionServiceMock;
    private readonly UserLoginCommandHandler _handler;

    public UserLoginCommandHandlerTests()
    {
        _dbContextMock = Substitute.For<IApplicationDbContext>();
        _tokenProviderMock = Substitute.For<ITokenProvider>();
        _sessionServiceMock = Substitute.For<ISessionService>();

        _handler = new UserLoginCommandHandler(
            _dbContextMock,
            _tokenProviderMock,
            _sessionServiceMock);
    }

    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ShouldReturnTokens()
    {
        // Arrange
        var command = new UserLoginCommand("test@example.com", "Password123!");
        
        var user = User.Create("test@example.com", "John", "Doe", "Password123!").Data;
        var mockDbSet = new List<User> { user }.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        var expectedJti = Guid.NewGuid().ToString();
        var expectedAccessToken = "access-token";
        var expectedRefreshToken = "refresh-token";
        
        _tokenProviderMock.CreateAccessToken(user).Returns((expectedJti, expectedAccessToken));
        _tokenProviderMock.CreateRefreshToken().Returns(expectedRefreshToken);
        
        _sessionServiceMock.CreateLoginSessionAsync(user.Id, expectedJti, expectedRefreshToken)
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.AccessToken.Should().Be(expectedAccessToken);
        result.Data.RefreshToken.Should().Be(expectedRefreshToken);
        
        await _sessionServiceMock.Received(1).CreateLoginSessionAsync(user.Id, expectedJti, expectedRefreshToken);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnInvalidCredentials()
    {
        // Arrange
        var command = new UserLoginCommand("notfound@example.com", "Password123!");
        
        var mockDbSet = new List<User>().BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
        
        await _sessionServiceMock.DidNotReceive().CreateLoginSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WhenPasswordIsIncorrect_ShouldReturnInvalidCredentials()
    {
        // Arrange
        var command = new UserLoginCommand("test@example.com", "WrongPassword!");
        
        var user = User.Create("test@example.com", "John", "Doe", "CorrectPassword123!").Data;
        var mockDbSet = new List<User> { user }.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
        
        await _sessionServiceMock.DidNotReceive().CreateLoginSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }
    
    [Fact]
    public async Task Handle_WhenSessionCreationFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new UserLoginCommand("test@example.com", "Password123!");
        
        var user = User.Create("test@example.com", "John", "Doe", "Password123!").Data;
        var mockDbSet = new List<User> { user }.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        _tokenProviderMock.CreateAccessToken(user).Returns(("jti", "token"));
        
        var sessionError = Error.InternalServerError("Session.Failed");
        _sessionServiceMock.CreateLoginSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Failure(sessionError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(sessionError);
    }
}