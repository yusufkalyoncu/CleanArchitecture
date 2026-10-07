using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Domain.Users;
using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Users.Register;

public class UserRegisterCommandHandlerTests
{
    private readonly IApplicationDbContext _dbContextMock;
    private readonly ITokenProvider _tokenProviderMock;
    private readonly ISessionService _sessionServiceMock;
    private readonly UserRegisterCommandHandler _handler;

    public UserRegisterCommandHandlerTests()
    {
        _dbContextMock = Substitute.For<IApplicationDbContext>();
        _tokenProviderMock = Substitute.For<ITokenProvider>();
        _sessionServiceMock = Substitute.For<ISessionService>();

        _handler = new UserRegisterCommandHandler(
            _dbContextMock,
            _tokenProviderMock,
            _sessionServiceMock);
    }

    [Fact]
    public async Task Handle_WhenAllInputsAreValid_ShouldCreateUserAndReturnTokens()
    {
        // Arrange
        var command = new UserRegisterCommand("newuser@example.com", "John", "Doe", "SecurePassword123!");
        
        // Mock DB to return no existing users
        var usersList = new List<User>();
        var mockDbSet = usersList.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        // Mock TokenProvider
        var expectedJti = Guid.NewGuid().ToString();
        var expectedAccessToken = "access-token";
        var expectedRefreshToken = "refresh-token";
        
        _tokenProviderMock.CreateAccessToken(Arg.Any<User>())
            .Returns((expectedJti, expectedAccessToken));
        _tokenProviderMock.CreateRefreshToken()
            .Returns(expectedRefreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.AccessToken.Should().Be(expectedAccessToken);
        result.Data.RefreshToken.Should().Be(expectedRefreshToken);

        // Verify side effects
        await _dbContextMock.Users.Received(1).AddAsync(Arg.Is<User>(u => u.Email.Value == "newuser@example.com"), Arg.Any<CancellationToken>());
        await _dbContextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _sessionServiceMock.Received(1).CreateRegisterSessionAsync(Arg.Any<Guid>(), expectedJti, expectedRefreshToken);
    }

    [Fact]
    public async Task Handle_WhenEmailIsInvalid_ShouldReturnFailureAndNotSaveChanges()
    {
        // Arrange
        var command = new UserRegisterCommand("invalid-email", "John", "Doe", "SecurePassword123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Email.InvalidFormat);

        await _dbContextMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldReturnFailureAndNotSaveChanges()
    {
        // Arrange
        var command = new UserRegisterCommand("existing@example.com", "John", "Doe", "SecurePassword123!");
        
        // Mock DB to return an existing user with the same email
        var existingUser = User.Create("existing@example.com", "Jane", "Doe", "Password123!").Data;
        var usersList = new List<User> { existingUser };
        var mockDbSet = usersList.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.AlreadyExists);

        await _dbContextMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserCreationFails_ShouldReturnFailureAndNotSaveChanges()
    {
        // Arrange
        // Valid email, but invalid password (too short)
        var command = new UserRegisterCommand("new@example.com", "John", "Doe", "123");
        
        var usersList = new List<User>();
        var mockDbSet = usersList.BuildMockDbSet();
        _dbContextMock.Users.Returns(mockDbSet);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Password.TooShort);

        await _dbContextMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}