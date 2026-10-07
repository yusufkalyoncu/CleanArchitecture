using CleanArchitecture.Application.Users.RefreshToken;
using FluentAssertions;

namespace CleanArchitecture.Application.UnitTests.Users.RefreshToken;

public class UserRefreshTokenCommandValidatorTests
{
    private readonly UserRefreshTokenCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenRefreshTokenIsValid_ShouldReturnNoErrors()
    {
        // Arrange
        var command = new UserRefreshTokenCommand("some-valid-token-string");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Validate_WhenRefreshTokenIsEmpty_ShouldFailWithCustomErrorCode(string? token)
    {
        // Arrange
        var command = new UserRefreshTokenCommand(token!);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RefreshToken" && e.ErrorCode == "RefreshToken.Empty");
    }
}