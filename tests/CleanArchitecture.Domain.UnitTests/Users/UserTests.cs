using FluentAssertions;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Domain.Users.Events;

namespace CleanArchitecture.Domain.UnitTests.Users;

public class UserTests
{
    private const string ValidEmail = "test@example.com";
    private const string ValidFirstName = "John";
    private const string ValidLastName = "Doe";
    private const string ValidPassword = "SecurePassword123!";

    [Fact]
    public void Create_WhenAllInputsAreValid_ShouldCreateUserAndRaiseEvent()
    {
        // Act
        var result = User.Create(ValidEmail, ValidFirstName, ValidLastName, ValidPassword);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = result.Data;

        // Verify state
        user.Should().NotBeNull();
        user.Id.Should().NotBeEmpty();
        user.Email.Value.Should().Be(ValidEmail);
        user.Name.FirstName.Should().Be(ValidFirstName);
        user.Name.LastName.Should().Be(ValidLastName);
        user.Password.VerifyPassword(ValidPassword).Should().BeTrue();

        // Verify event
        var domainEvents = user.DomainEvents;
        domainEvents.Should().ContainSingle();
        
        var userRegisteredEvent = domainEvents[0] as UserRegisteredDomainEvent;
        userRegisteredEvent.Should().NotBeNull();
        userRegisteredEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Create_WhenEmailIsInvalid_ShouldReturnFailure()
    {
        // Arrange
        var invalidEmail = "invalid-email";

        // Act
        var result = User.Create(invalidEmail, ValidFirstName, ValidLastName, ValidPassword);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Email.InvalidFormat);
    }

    [Fact]
    public void Create_WhenNameIsInvalid_ShouldReturnFailure()
    {
        // Arrange
        var invalidFirstName = "";

        // Act
        var result = User.Create(ValidEmail, invalidFirstName, ValidLastName, ValidPassword);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.FirstName.Empty);
    }

    [Fact]
    public void Create_WhenPasswordIsInvalid_ShouldReturnFailure()
    {
        // Arrange
        var invalidPassword = "123"; // Too short

        // Act
        var result = User.Create(ValidEmail, ValidFirstName, ValidLastName, invalidPassword);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Password.TooShort);
    }

    [Fact]
    public void ClearEvents_ShouldRemoveAllDomainEvents()
    {
        // Arrange
        var user = User.Create(ValidEmail, ValidFirstName, ValidLastName, ValidPassword).Data;
        user.DomainEvents.Should().NotBeEmpty();

        // Act
        user.ClearEvents();

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }
}