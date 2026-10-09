using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Application.Users.RefreshToken;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Integration.Tests.Endpoints.Users;

public class AuthTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Register_Should_CreateUserAndReturnTokens_WhenRequestIsValid()
    {
        // Arrange
        var request = new UserRegisterCommand(
            "test1@test.com",
            "John",
            "Doe",
            "Password123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UserRegisterCommandResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeEmpty();
        result.RefreshToken.Should().NotBeEmpty();

        var emailResult = Email.Create(request.Email);
        var userInDb = await DbContext.Users.FirstOrDefaultAsync(u => u.Email == emailResult.Data);
        userInDb.Should().NotBeNull();
        userInDb!.Name.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task Register_Should_ReturnConflict_WhenEmailIsAlreadyTaken()
    {
        // Arrange
        var request = new UserRegisterCommand(
            "test2@test.com",
            "John",
            "Doe",
            "Password123!");

        // Register once
        await HttpClient.PostAsJsonAsync("/users/register", request);

        // Act
        // Try to register again with the same email
        var response = await HttpClient.PostAsJsonAsync("/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_Should_ReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var password = "Password123!";
        var registerRequest = new UserRegisterCommand(
            "test3@test.com",
            "John",
            "Doe",
            password);

        await HttpClient.PostAsJsonAsync("/users/register", registerRequest);

        var loginRequest = new UserLoginCommand(registerRequest.Email, password);

        // Act
        var response = await HttpClient.PostAsJsonAsync("/users/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<UserLoginCommandResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeEmpty();
        result.RefreshToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_Should_ReturnUnauthorized_WhenPasswordIsWrong()
    {
        // Arrange
        var registerRequest = new UserRegisterCommand(
            "test4@test.com",
            "John",
            "Doe",
            "Password123!");

        await HttpClient.PostAsJsonAsync("/users/register", registerRequest);

        var loginRequest = new UserLoginCommand(registerRequest.Email, "WrongPassword123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/users/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnNewTokens_WhenTokenIsValid()
    {
        // Arrange
        var registerRequest = new UserRegisterCommand(
            "test5@test.com",
            "John",
            "Doe",
            "Password123!");

        var registerResponse = await HttpClient.PostAsJsonAsync("/users/register", registerRequest);
        var tokens = await registerResponse.Content.ReadFromJsonAsync<UserRegisterCommandResponse>();
        
        var refreshRequest = new UserRefreshTokenCommandRequest(tokens!.RefreshToken);

        // Refresh token endpoint requires the Auth bearer token in headers
        // (AuthPolicies.RefreshToken usually just checks valid JWT signature)
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/users/refresh-token");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        requestMessage.Content = JsonContent.Create(refreshRequest);

        // Act
        var response = await HttpClient.SendAsync(requestMessage);

        // Assert
        var errorContent = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"because the error was: {errorContent}");
        
        var newTokens = await response.Content.ReadFromJsonAsync<UserRefreshTokenCommandResponse>();
        newTokens.Should().NotBeNull();
        newTokens!.AccessToken.Should().NotBeEmpty();
        newTokens.RefreshToken.Should().NotBeEmpty();
        newTokens.AccessToken.Should().NotBe(tokens.AccessToken);
        newTokens.RefreshToken.Should().NotBe(tokens.RefreshToken);
    }
}