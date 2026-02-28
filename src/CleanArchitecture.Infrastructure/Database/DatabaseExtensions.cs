using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Infrastructure.Audit;
using CleanArchitecture.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Database;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresOptions = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>();
        if (postgresOptions is null)
        {
            throw new InvalidOperationException("Postgres options are not configured.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(postgresOptions.ConnectionString);
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var outboxInterceptor = sp.GetRequiredService<OutboxInsertInterceptor>();
            var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();

            options.UseNpgsql(postgresOptions.ConnectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, DatabaseSchemas.Default))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(outboxInterceptor, auditInterceptor);
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}