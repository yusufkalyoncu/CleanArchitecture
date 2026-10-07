using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Domain.Users;
using FluentAssertions;

namespace CleanArchitecture.Application.UnitTests.Users.Login;

public class UserLoginCommandValidatorTests
{
    private readonly UserLoginCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenAllPropertiesAreValid_ShouldReturnNoErrors()
    {
        // Arrange
        var command = new UserLoginCommand("test@example.com", "SecurePassword123!");

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
    public async Task Validate_WhenEmailIsEmpty_ShouldFail(string? email)
    {
        // Arrange
        var command = new UserLoginCommand(email!, "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorCode == "NotEmptyValidator");
    }

    [Fact]
    public async Task Validate_WhenEmailIsTooLong_ShouldFailWithDomainErrorCode()
    {
        // Arrange
        var command = new UserLoginCommand(new string('a', Email.MaxLength + 1), "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorCode == UserErrors.Email.TooLong.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Validate_WhenPasswordIsEmpty_ShouldFail(string? password)
    {
        // Arrange
        var command = new UserLoginCommand("test@example.com", password!);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorCode == "NotEmptyValidator");
    }

    [Fact]
    public async Task Validate_WhenPasswordIsTooShort_ShouldFailWithDomainErrorCode()
    {
        // Arrange
        var command = new UserLoginCommand("test@example.com", "12345");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorCode == UserErrors.Password.TooShort.ErrorCode);
    }
}