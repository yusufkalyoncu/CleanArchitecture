using System.Reflection;
using System.Text;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Caching;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.DomainEvents;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Application.Abstractions.Locking;
using CleanArchitecture.Application.Abstractions.Option;
using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Infrastructure.Audit;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Caching;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.DomainEvents;
using CleanArchitecture.Infrastructure.EventBus;
using CleanArchitecture.Infrastructure.Locking;
using CleanArchitecture.Infrastructure.Outbox;
using CleanArchitecture.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Scrutor;
using Serilog;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddServices()
            .AddAppOptions(configuration, typeof(PostgresOptions).Assembly)
            .AddEventBus()
            .AddOutboxServices()
            .AddDatabase(configuration)
            .AddRedisConfiguration(configuration)
            .AddCacheServices()
            .AddLockManager()
            .AddAuthenticationInternal(configuration);

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        return services;
    }

    private static IServiceCollection AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var optionTypes = assembly
            .GetTypes()
            .Where(t => typeof(IAppOption).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false });

        foreach (var type in optionTypes)
        {
            var sectionName = type.GetField("SectionName")?.GetValue(null)?.ToString();

            if (string.IsNullOrWhiteSpace(sectionName))
                continue;

            var method = typeof(OptionsConfigurationServiceCollectionExtensions)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == nameof(OptionsConfigurationServiceCollectionExtensions.Configure)
                            && m.GetParameters().Length == 2)
                .MakeGenericMethod(type);

            method.Invoke(null, [services, configuration.GetSection(sectionName)]);
        }

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
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

    private static IServiceCollection AddRedisConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        if (redisOptions == null)
        {
            throw new InvalidOperationException("RedisOptions section is missing in configuration.");
        }

        var redisConfig = ConfigurationOptions.Parse(redisOptions.ConnectionString);

        redisConfig.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
        redisConfig.ConnectTimeout = redisOptions.ConnectTimeout;
        redisConfig.SyncTimeout = redisOptions.SyncTimeout;
        redisConfig.DefaultDatabase = redisOptions.Database;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));

        return services;
    }

    private static IServiceCollection AddCacheServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<IDistributedCacheService, RedisCacheService>();

        return services;
    }

    private static IServiceCollection AddLockManager(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLockManager, RedisLockManager>();

        return services;
    }

    private static void AddAuthenticationInternal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwtOptions == null)
        {
            throw new InvalidOperationException("JwtOptions section is missing in configuration.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme,
                o => ConfigureJwtBearer(o, key, jwtOptions, validateLifetime: true))
            .AddJwtBearer("BearerIgnoreLifetime", o => ConfigureJwtBearer(o, key, jwtOptions, validateLifetime: false));
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, SecurityKey key, JwtOptions jwtOptions,
        bool validateLifetime)
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            ClockSkew = TimeSpan.Zero,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionService = context.HttpContext.RequestServices.GetRequiredService<ISessionService>();
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrEmpty(jti))
                {
                    context.Fail("Token does not contain JTI claim.");
                    return;
                }

                var isBlacklisted = await sessionService.IsAccessTokenBlacklistedAsync(jti);
                if (isBlacklisted)
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    }
    
    private static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        services.Scan(selector => selector
            .FromAssemblies(typeof(IEventBus).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IIntegrationEventHandler<>)), publicOnly: false)
            .UsingRegistrationStrategy(RegistrationStrategy.Append)
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        
        return services;
    }

    private static IServiceCollection AddOutboxServices(this IServiceCollection services)
    {
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddSingleton<IOutboxSignal, OutboxSignal>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();
        services.AddScoped<OutboxInsertInterceptor>();

        return services;
    }

    public static void AddSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));
    }
}