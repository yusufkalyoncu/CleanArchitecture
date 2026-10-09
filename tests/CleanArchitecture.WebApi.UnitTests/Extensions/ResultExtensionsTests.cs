using CleanArchitecture.Shared;
using CleanArchitecture.Shared.Resources.Languages;
using CleanArchitecture.WebApi.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace CleanArchitecture.WebApi.UnitTests.Extensions;

public class ResultExtensionsTests
{
    private readonly IStringLocalizer<Lang> _mockLocalizer;

    public ResultExtensionsTests()
    {
        _mockLocalizer = Substitute.For<IStringLocalizer<Lang>>();
        
        // Mock localizer to just return the key as the localized string for basic tests
        _mockLocalizer[Arg.Any<string>()].Returns(callInfo => 
            new LocalizedString(callInfo.Arg<string>(), callInfo.Arg<string>()));
    }

    [Fact]
    public void ToOk_Should_ReturnOkResult_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var httpResult = result.ToOk(_mockLocalizer);

        // Assert
        httpResult.Should().BeOfType<Ok>();
    }

    [Fact]
    public void ToOk_Generic_Should_ReturnOkObjectResult_WhenResultIsSuccess()
    {
        // Arrange
        var data = "test_data";
        var result = Result.Success(data);

        // Act
        var httpResult = result.ToOk(_mockLocalizer);

        // Assert
        var okResult = httpResult.Should().BeOfType<Ok<string>>().Subject;
        okResult.Value.Should().Be(data);
    }

    [Fact]
    public void ToNoContent_Should_ReturnNoContentResult_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var httpResult = result.ToNoContent(_mockLocalizer);

        // Assert
        httpResult.Should().BeOfType<NoContent>();
    }

    [Theory]
    [InlineData(400)] // BadRequest
    [InlineData(401)] // Unauthorized
    [InlineData(403)] // Forbidden
    [InlineData(404)] // NotFound
    [InlineData(409)] // Conflict
    [InlineData(429)] // TooManyRequests
    [InlineData(500)] // InternalServerError
    public void ToOk_Should_ReturnProblemDetails_WithCorrectStatusCode_WhenResultIsFailure(int expectedStatusCode)
    {
        // Arrange
        var error = expectedStatusCode switch
        {
            400 => Error.BadRequest("Test.Code"),
            401 => Error.Unauthorized("Test.Code"),
            403 => Error.Forbidden("Test.Code"),
            404 => Error.NotFound("Test.Code"),
            409 => Error.Conflict("Test.Code"),
            429 => Error.TooManyRequests("Test.Code"),
            _ => Error.InternalServerError("Test.Code")
        };
        
        var result = Result.Failure(error);

        // Act
        var httpResult = result.ToOk(_mockLocalizer);

        // Assert
        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(expectedStatusCode);
        
        // We know ProblemDetails will be populated
        problemResult.ProblemDetails.Should().NotBeNull();
        problemResult.ProblemDetails.Status.Should().Be(expectedStatusCode);
    }

    [Fact]
    public void ProblemDetails_Should_FormatValidationErrors_WithCamelCase()
    {
        // Arrange
        var errors = new[]
        {
            Error.Validation("Email.Invalid", field: "UserEmailAddress"),
            Error.Validation("Name.Empty", field: "User.FirstName")
        };
        var validationError = new ValidationError(errors);
        var result = Result.Failure(validationError);

        // Act
        var httpResult = result.ToOk(_mockLocalizer);

        // Assert
        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        var problemDetails = problemResult.ProblemDetails;
        
        problemDetails.Should().NotBeNull();
        problemDetails.Extensions.Should().ContainKey("errors");
        
        var extensions = problemDetails.Extensions["errors"];
        var json = System.Text.Json.JsonSerializer.Serialize(extensions);
        
        json.Should().Contain("userEmailAddress");
        json.Should().Contain("user.firstName");
    }
}