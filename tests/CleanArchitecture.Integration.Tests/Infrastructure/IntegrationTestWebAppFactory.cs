using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace CleanArchitecture.Integration.Tests.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("clean_architecture_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine")
        .Build();

    public string DbConnectionString => _dbContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove background services so they don't interfere with tests and Respawn
            services.RemoveAll<IHostedService>();
            // Note: We completely removed all hosted services, which means the API will start without polling outbox/inbox.
            // If any specific test needs to run them, we can run them manually.
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();

        Environment.SetEnvironmentVariable("PostgresOptions__Host", _dbContainer.Hostname);
        Environment.SetEnvironmentVariable("PostgresOptions__Port", _dbContainer.GetMappedPublicPort(5432).ToString());
        Environment.SetEnvironmentVariable("PostgresOptions__Database", "clean_architecture_test");
        Environment.SetEnvironmentVariable("PostgresOptions__Username", "postgres");
        Environment.SetEnvironmentVariable("PostgresOptions__Password", "postgres");

        Environment.SetEnvironmentVariable("RedisOptions__Host", _redisContainer.Hostname);
        Environment.SetEnvironmentVariable("RedisOptions__Port", _redisContainer.GetMappedPublicPort(6379).ToString());

        Environment.SetEnvironmentVariable("RateLimitOptions__Global__PermitLimit", "1000");
        Environment.SetEnvironmentVariable("RateLimitOptions__Login__PermitLimit", "500");
        Environment.SetEnvironmentVariable("RateLimitOptions__Registration__PermitLimit", "500");
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync().AsTask();
        await _redisContainer.DisposeAsync().AsTask();
    }
}