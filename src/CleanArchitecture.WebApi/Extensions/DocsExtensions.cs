using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
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
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();

                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                var scheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme."
                };

                document.Components.SecuritySchemes["Bearer"] = scheme;

                var securityRequirement = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                };

                document.Security ??= new List<OpenApiSecurityRequirement>();
                document.Security.Add(securityRequirement);

                return Task.CompletedTask;
            });
        });
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