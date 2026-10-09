using System.Net;
using CleanArchitecture.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Integration.Tests;

public class SmokeTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task App_Should_StartAndConnectToDatabase()
    {
        // Act
        // Verify we can connect and run a simple query via EF Core
        var canConnect = await DbContext.Database.CanConnectAsync();
        var pendingMigrations = await DbContext.Database.GetPendingMigrationsAsync();

        // Assert
        canConnect.Should().BeTrue("because the application should be able to connect to the Testcontainers PostgreSQL database");
        pendingMigrations.Should().BeEmpty("because WebApplicationFactory should have applied all migrations automatically on startup");
    }

    [Fact]
    public async Task Api_Should_RespondWith404_OnRoot()
    {
        // Act
        // We don't have a specific root endpoint mapped, so it should return 404, but it proves the API is listening.
        var response = await HttpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}