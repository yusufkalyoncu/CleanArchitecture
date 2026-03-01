using CleanArchitecture.Infrastructure.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.RefreshToken, policy =>
            {
                policy.AddAuthenticationSchemes(AuthSchemes.IgnoreLifetime);
                policy.RequireAuthenticatedUser();
            });

        return services;
    }
}