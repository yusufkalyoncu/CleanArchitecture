using FluentAssertions;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Domain.UnitTests.Users;

public class NameTests
{
    [Theory]
    [InlineData("John", "Doe", "John", "Doe")]
    [InlineData("  John  ", "  Doe  ", "John", "Doe")] // Should trim whitespace
    public void Create_WhenNamesAreValid_ShouldReturnSuccessAndTrimWhitespace(
        string firstName, string lastName, string expectedFirst, string expectedLast)
    {
        // Act
        var result = Name.Create(firstName, lastName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.FirstName.Should().Be(expectedFirst);
        result.Data.LastName.Should().Be(expectedLast);
        
        result.Data.ToString().Should().Be($"{expectedFirst} {expectedLast}");
        result.Data.FullName.Should().Be($"{expectedFirst} {expectedLast}");
    }

    [Theory]
    [InlineData("", "Doe")]
    [InlineData(" ", "Doe")]
    [InlineData(null, "Doe")]
    public void Create_WhenFirstNameIsEmpty_ShouldFail(string? emptyFirstName, string lastName)
    {
        // Act
        var result = Name.Create(emptyFirstName!, lastName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.FirstName.Empty);
    }

    [Theory]
    [InlineData("John", "")]
    [InlineData("John", " ")]
    [InlineData("John", null)]
    public void Create_WhenLastNameIsEmpty_ShouldFail(string firstName, string? emptyLastName)
    {
        // Act
        var result = Name.Create(firstName, emptyLastName!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.LastName.Empty);
    }

    [Fact]
    public void Create_WhenFirstNameIsTooShort_ShouldFail()
    {
        // Arrange (Business rule: Min length is 2)
        var tooShortFirst = "A"; // 1 char
        
        // Act
        var result = Name.Create(tooShortFirst, "Doe");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.FirstName.TooShort);
    }

    [Fact]
    public void Create_WhenFirstNameIsTooLong_ShouldFail()
    {
        // Arrange (Business rule: Max length is 50)
        var tooLongFirst = new string('a', 51);
        
        // Act
        var result = Name.Create(tooLongFirst, "Doe");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.FirstName.TooLong);
    }

    [Fact]
    public void Create_WhenLastNameIsTooShort_ShouldFail()
    {
        // Arrange
        var tooShortLast = new string('a', Name.LastNameMinLength - 1);
        
        // Act
        var result = Name.Create("John", tooShortLast);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.LastName.TooShort);
    }

    [Fact]
    public void Create_WhenLastNameIsTooLong_ShouldFail()
    {
        // Arrange
        var tooLongLast = new string('a', Name.LastNameMaxLength + 1);
        
        // Act
        var result = Name.Create("John", tooLongLast);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Name.LastName.TooLong);
    }

    [Fact]
    public void Equality_WhenSameValues_ShouldBeEqual()
    {
        // Arrange
        var name1 = Name.Create("John", "Doe").Data;
        var name2 = Name.Create("John", "Doe").Data;

        // Act & Assert
        (name1 == name2).Should().BeTrue();
        name1.Equals(name2).Should().BeTrue();
        name1.GetHashCode().Should().Be(name2.GetHashCode());
    }
}