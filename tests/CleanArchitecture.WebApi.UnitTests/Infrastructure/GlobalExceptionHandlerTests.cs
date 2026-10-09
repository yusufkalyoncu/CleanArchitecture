using System.Text.Json;
using CleanArchitecture.WebApi.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CleanArchitecture.WebApi.UnitTests.Infrastructure;

public class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task TryHandleAsync_Should_LogException_And_ReturnProblemDetails()
    {
        // Arrange
        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(logger);

        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream() // Capture the output
            }
        };

        var exception = new Exception("Something went terribly wrong");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        // Reset the body stream position to read the response
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, JsonOptions);

        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Server failure");
        problemDetails.Type.Should().Be("https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1");
    }
}