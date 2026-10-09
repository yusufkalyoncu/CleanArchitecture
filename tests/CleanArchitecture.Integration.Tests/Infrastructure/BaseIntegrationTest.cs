using CleanArchitecture.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Respawn;

namespace CleanArchitecture.Integration.Tests.Infrastructure;

[Collection(SharedTestCollection.Name)]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private readonly IServiceScope _scope;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ApplicationDbContext DbContext;
    protected readonly HttpClient HttpClient;
    
    private readonly IntegrationTestWebAppFactory _factory;
    private Respawner? _respawner;

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        
        // Scope for resolving dependencies
        _scope = factory.Services.CreateScope();
        ServiceProvider = _scope.ServiceProvider;
        
        DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // HttpClient for E2E tests
        HttpClient = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await SetupRespawnerAsync();
        await ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    private async Task SetupRespawnerAsync()
    {
        if (_respawner is not null)
        {
            return;
        }
        
        var connectionString = _factory.DbConnectionString;
        await using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore =
            [
                "__EFMigrationsHistory"
            ],
            WithReseed = true
        });
    }

    private async Task ResetDatabaseAsync()
    {
        if (_respawner is not null)
        {
            await using var connection = new Npgsql.NpgsqlConnection(_factory.DbConnectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }
    }
}