using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Integration.Tests.Infrastructure;
using FluentAssertions;

namespace CleanArchitecture.Integration.Tests.Endpoints.Users;

public class LogoutTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<UserRegisterCommandResponse> RegisterUserAsync(string email, string password)
    {
        var request = new UserRegisterCommand(email, "John", "Doe", password);
        var response = await HttpClient.PostAsJsonAsync("/users/register", request);
        return (await response.Content.ReadFromJsonAsync<UserRegisterCommandResponse>())!;
    }
    
    private async Task<UserLoginCommandResponse> LoginUserAsync(string email, string password)
    {
        var request = new UserLoginCommand(email, password);
        var response = await HttpClient.PostAsJsonAsync("/users/login", request);
        return (await response.Content.ReadFromJsonAsync<UserLoginCommandResponse>())!;
    }

    [Fact]
    public async Task Logout_Should_ReturnOk_And_BlacklistToken()
    {
        // Arrange
        var password = "Password123!";
        var tokens = await RegisterUserAsync("logout_test1@test.com", password);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/users/logout");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Act - 1: Logout
        var response = await HttpClient.SendAsync(requestMessage);

        // Assert - 1
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - 2: Try to use the same token to access a protected endpoint
        // (Using /users/logout again just to check if token is valid)
        var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/users/logout");
        verifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        
        var verifyResponse = await HttpClient.SendAsync(verifyRequest);

        // Assert - 2: Should be Unauthorized because the token is now blacklisted in Redis
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutAll_Should_ReturnOk_And_BlacklistAllTokens()
    {
        // Arrange
        var email = "logoutall_test1@test.com";
        var password = "Password123!";
        var tokens1 = await RegisterUserAsync(email, password);
        var tokens2 = await LoginUserAsync(email, password);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/users/logout-all");
        // Use tokens1 to trigger logout-all
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens1.AccessToken);

        // Act - 1: Logout All
        var response = await HttpClient.SendAsync(requestMessage);

        // Assert - 1
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - 2: Try to use tokens1
        var verifyRequest1 = new HttpRequestMessage(HttpMethod.Post, "/users/logout");
        verifyRequest1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens1.AccessToken);
        var verifyResponse1 = await HttpClient.SendAsync(verifyRequest1);

        // Assert - 2: Tokens1 should be Unauthorized
        verifyResponse1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        // Act - 3: Try to use tokens2
        var verifyRequest2 = new HttpRequestMessage(HttpMethod.Post, "/users/logout");
        verifyRequest2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens2.AccessToken);
        var verifyResponse2 = await HttpClient.SendAsync(verifyRequest2);

        // Assert - 3: Tokens2 should ALSO be Unauthorized because all sessions for this user were revoked
        verifyResponse2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}