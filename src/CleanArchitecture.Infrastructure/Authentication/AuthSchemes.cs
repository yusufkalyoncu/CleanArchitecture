using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CleanArchitecture.Infrastructure.Authentication;

public static class AuthSchemes
{
    public const string Default = JwtBearerDefaults.AuthenticationScheme;
    public const string IgnoreLifetime = "BearerIgnoreLifetime";
}