using FluentAssertions;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Domain.UnitTests.Users;

public class PasswordTests
{
    [Fact]
    public void Create_WhenPasswordIsValid_ShouldReturnSuccessWithHash()
    {
        // Arrange
        var validPassword = "SecurePassword123!";

        // Act
        var result = Password.Create(validPassword);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.HashedValue.Should().NotBeNullOrWhiteSpace();
        result.Data.HashedValue.Should().NotBe(validPassword);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WhenPasswordIsEmpty_ShouldFail(string? invalidPassword)
    {
        // Act
        var result = Password.Create(invalidPassword!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Password.Empty);
    }

    [Fact]
    public void Create_WhenPasswordIsTooShort_ShouldFail()
    {
        // Arrange (Business rule: Min length is 6)
        var tooShortPassword = "abcde"; // 5 chars

        // Act
        var result = Password.Create(tooShortPassword);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Password.TooShort);
    }

    [Fact]
    public void Create_WhenPasswordIsTooLong_ShouldFail()
    {
        // Arrange (Business rule: Max length is 30)
        var tooLongPassword = new string('a', 31);

        // Act
        var result = Password.Create(tooLongPassword);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Password.TooLong);
    }

    [Fact]
    public void VerifyPassword_WhenPasswordIsCorrect_ShouldReturnTrue()
    {
        // Arrange
        var plainText = "SecurePassword123!";
        var password = Password.Create(plainText).Data;

        // Act
        var isValid = password.VerifyPassword(plainText);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WhenPasswordIsIncorrect_ShouldReturnFalse()
    {
        // Arrange
        var password = Password.Create("CorrectPassword!").Data;

        // Act
        var isValid = password.VerifyPassword("WrongPassword!");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void FromHash_ShouldRehydratePasswordObject()
    {
        // Arrange
        var originalPassword = Password.Create("MySecret!").Data;
        
        // Act
        var rehydratedPassword = Password.FromHash(originalPassword.HashedValue);

        // Assert
        rehydratedPassword.HashedValue.Should().Be(originalPassword.HashedValue);
        rehydratedPassword.VerifyPassword("MySecret!").Should().BeTrue();
    }
}