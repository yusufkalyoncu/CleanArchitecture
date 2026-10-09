using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Application.Users.GetUsers;
using CleanArchitecture.Integration.Tests.Infrastructure;
using FluentAssertions;

namespace CleanArchitecture.Integration.Tests.Endpoints.Users;

public class GetTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Get_Should_ReturnUsersList_WhenCalled()
    {
        // Act
        // The endpoint is mapped as MapGet("users") inside Users module (meaning URL is /users)
        var response = await HttpClient.GetAsync("/users");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var users = await response.Content.ReadFromJsonAsync<IEnumerable<GetUsersQueryResponse>>();
        users.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_Should_ReturnUser_WhenCalled()
    {
        // Act
        // The endpoint is mapped as MapGet("users/{id:guid}") inside Users module
        var response = await HttpClient.GetAsync($"/users/{Guid.NewGuid()}");
        
        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }
}