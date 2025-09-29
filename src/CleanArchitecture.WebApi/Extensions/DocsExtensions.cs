using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Scalar.AspNetCore;

namespace CleanArchitecture.WebApi.Extensions;

public static class DocsExtensions
{
    public static void AddDocs(this IServiceCollection services)
        => services
            .AddApiVersioning()
            .AddScalar();

    private static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new HeaderApiVersionReader("X-Version");
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = false;
            });

        services.AddEndpointsApiExplorer();

        return services;
    }

    private static void AddScalar(this IServiceCollection services)
    {
        services.AddOpenApi();
    }
    
    public static void UseDocs(this WebApplication app)
    {
        var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        
        foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
        {
            app.MapOpenApi($"/openapi/{description.GroupName}.json");
        }
    
        app.MapScalarApiReference(options =>
        {
            foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
            {
                options.WithOpenApiRoutePattern($"/openapi/{description.GroupName}.json");
            }
        });
    }
}