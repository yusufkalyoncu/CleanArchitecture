using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Domain.Users;
using FluentAssertions;

namespace CleanArchitecture.Application.UnitTests.Users.Register;

public class UserRegisterCommandValidatorTests
{
    private readonly UserRegisterCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenAllPropertiesAreValid_ShouldReturnNoErrors()
    {
        // Arrange
        var command = new UserRegisterCommand(
            "test@example.com",
            "John",
            "Doe",
            "SecurePassword123!");

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
        var command = new UserRegisterCommand(email!, "John", "Doe", "Password123!");

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
        var command = new UserRegisterCommand(new string('a', Email.MaxLength + 1), "John", "Doe", "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorCode == UserErrors.Email.TooLong.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenFirstNameIsTooShort_ShouldFailWithDomainErrorCode()
    {
        // Arrange
        var command = new UserRegisterCommand("test@test.com", "A", "Doe", "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName" && e.ErrorCode == UserErrors.Name.FirstName.TooShort.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenFirstNameIsTooLong_ShouldFailWithDomainErrorCode()
    {
        // Arrange
        var command = new UserRegisterCommand("test@test.com", new string('a', Name.FirstNameMaxLength + 1), "Doe", "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName" && e.ErrorCode == UserErrors.Name.FirstName.TooLong.ErrorCode);
    }
    
    [Fact]
    public async Task Validate_WhenLastNameIsTooShort_ShouldFailWithDomainErrorCode()
    {
        // Arrange
        var command = new UserRegisterCommand("test@test.com", "John", "D", "Password123!");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName" && e.ErrorCode == UserErrors.Name.LastName.TooShort.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenPasswordIsTooShort_ShouldFailWithDomainErrorCode()
    {
        // Arrange
        var command = new UserRegisterCommand("test@test.com", "John", "Doe", "12345");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorCode == UserErrors.Password.TooShort.ErrorCode);
    }

    [Fact]
    public async Task Validate_WhenMultipleRulesViolated_ShouldReturnMultipleErrors()
    {
        // Arrange
        var command = new UserRegisterCommand("", "A", new string('B', 100), "123");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThan(3);
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}