using CleanArchitecture.Integration.Tests.Infrastructure;
using FluentAssertions;

namespace CleanArchitecture.Integration.Tests.WebApi;

public class DocsEndpointsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task OpenApiJson_Should_ReturnSuccess_And_ContainBearerSecurityScheme()
    {
        // Act
        var response = await HttpClient.GetAsync("/openapi/v1.json");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Ensure the OpenAPI document is generated
        content.Should().NotBeNullOrEmpty();
        
        // Ensure the document transformer correctly added the Bearer token scheme
        content.Should().Contain("Bearer");
        content.Should().Contain("JWT");
        content.Should().Contain("bearer"); // The scheme name
    }

    [Fact]
    public async Task ScalarApiReference_Should_ReturnSuccess()
    {
        // Act
        var response = await HttpClient.GetAsync("/scalar/v1");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Ensure the Scalar UI page is returned
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("scalar");
    }
}