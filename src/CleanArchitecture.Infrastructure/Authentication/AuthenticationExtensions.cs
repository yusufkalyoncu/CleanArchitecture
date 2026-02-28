using System.Text;
using CleanArchitecture.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Infrastructure.Authentication;

public static class AuthenticationExtensions
{
    public const string IgnoreLifetimeScheme = "BearerIgnoreLifetime";

    public static void AddAuthenticationInternal(this IServiceCollection services, IConfiguration configuration)
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
            .AddJwtBearer(IgnoreLifetimeScheme, o => ConfigureJwtBearer(o, key, jwtOptions, validateLifetime: false));
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        SecurityKey key,
        JwtOptions jwtOptions,
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
}