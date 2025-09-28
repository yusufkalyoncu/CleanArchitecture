using CleanArchitecture.WebApi.Middleware;

namespace CleanArchitecture.WebApi.Extensions;

public static class MiddlewareExtensions
{
    public static void UseMiddlewares(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestContextLoggingMiddleware>();
    }
}