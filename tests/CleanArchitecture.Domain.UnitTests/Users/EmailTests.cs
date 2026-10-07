using FluentAssertions;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Domain.UnitTests.Users;

public class EmailTests
{
    [Fact]
    public void Create_WhenEmailIsValid_ShouldReturnSuccess()
    {
        // Arrange
        var validEmail = "test@example.com";

        // Act
        var result = Email.Create(validEmail);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Value.Should().Be(validEmail);
        result.Data.ToString().Should().Be(validEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WhenEmailIsEmpty_ShouldFail(string? invalidEmail)
    {
        // Act
        var result = Email.Create(invalidEmail!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Email.Empty);
    }

    [Fact]
    public void Create_WhenEmailExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var longEmail = new string('a', Email.MaxLength) + "@example.com";

        // Act
        var result = Email.Create(longEmail);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Email.TooLong);
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("#@%^%#$@#$@#.com")]
    [InlineData("@example.com")]
    [InlineData("email.example.com")]
    [InlineData("email@example@example.com")]
    public void Create_WhenEmailFormatIsInvalid_ShouldFail(string invalidEmail)
    {
        // Act
        var result = Email.Create(invalidEmail);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Email.InvalidFormat);
    }

    [Fact]
    public void FromValue_ShouldCreateEmailDirectly()
    {
        // Arrange
        var emailStr = "existing@example.com";

        // Act
        var email = Email.FromValue(emailStr);

        // Assert
        email.Value.Should().Be(emailStr);
    }

    [Fact]
    public void Equality_WhenSameValue_ShouldBeEqual()
    {
        // Arrange
        var email1 = Email.FromValue("test@example.com");
        var email2 = Email.FromValue("test@example.com");

        // Act & Assert
        (email1 == email2).Should().BeTrue();
        email1.Equals(email2).Should().BeTrue();
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }
}