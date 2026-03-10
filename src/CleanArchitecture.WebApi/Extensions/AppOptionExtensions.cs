using System.Reflection;
using CleanArchitecture.Shared;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.WebApi.Extensions;

public static class AppOptionExtensions
{
    public static void AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        var optionTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IAppOption).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false });

        var registerMethod = typeof(AppOptionExtensions)
            .GetMethod(nameof(RegisterOption), BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var type in optionTypes)
        {
            var sectionName = type.GetField("SectionName")?.GetValue(null)?.ToString();

            if (string.IsNullOrWhiteSpace(sectionName)) continue;

            registerMethod?
                .MakeGenericMethod(type)
                .Invoke(null, [services, configuration, sectionName]);
        }
    }

    private static void RegisterOption<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName) where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateFluentValidation()
            .ValidateOnStart();
    }

    private static OptionsBuilder<TOptions> ValidateFluentValidation<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(provider =>
            new FluentValidateOptions<TOptions>(provider, builder.Name));
        return builder;
    }
}

public class FluentValidateOptions<TOptions>(IServiceProvider serviceProvider, string? name)
    : IValidateOptions<TOptions>
    where TOptions : class
{
    public ValidateOptionsResult Validate(string? name1, TOptions options)
    {
        if (name != null && name != name1) return ValidateOptionsResult.Skip;

        ArgumentNullException.ThrowIfNull(options);

        using var scope = serviceProvider.CreateScope();
        var validator = scope.ServiceProvider.GetService<IValidator<TOptions>>();

        if (validator is null) return ValidateOptionsResult.Success;

        var result = validator.Validate(options);

        if (result.IsValid) return ValidateOptionsResult.Success;

        var errors = result.Errors.Select(e =>
            $"Options validation failed for {typeof(TOptions).Name}.{e.PropertyName} with error: {e.ErrorMessage}");

        return ValidateOptionsResult.Fail(errors);
    }
}