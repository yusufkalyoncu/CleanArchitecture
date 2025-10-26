using System.Reflection;
using CleanArchitecture.Application.Abstractions.Behaviors;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Application.Abstractions.Option;
using CleanArchitecture.Application.Database;
using CleanArchitecture.Shared;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddCqrs()
            .AddValidationDecorator()
            .AddLoggingDecorator()
            .AddValidators()
            .AddDomainEventHandler()
            .AddAppOptions(configuration, typeof(PostgresOptions).Assembly);

    private static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

    private static IServiceCollection AddValidationDecorator(this IServiceCollection services)
    {
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        services.Decorate(typeof(ICommandHandler<>), typeof(ValidationDecorator.CommandBaseHandler<>));

        return services;
    }

    private static IServiceCollection AddLoggingDecorator(this IServiceCollection services)
    {
        services.Decorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        services.Decorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandBaseHandler<>));
        
        return services;
    }

    private static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }

    private static IServiceCollection AddDomainEventHandler(this IServiceCollection services)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
    
    public static IServiceCollection AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var optionTypes = assembly
            .GetTypes()
            .Where(t => typeof(IAppOptions).IsAssignableFrom(t) 
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
}