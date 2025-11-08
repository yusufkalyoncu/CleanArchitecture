using CleanArchitecture.Application.Abstractions.Option;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class JwtOptions : IAppOption
{
    public const string SectionName = "JwtOptions";
    
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SecretKey { get; init; }
    public TimeSpan AccessTokenLifeTime { get; init; }
    public TimeSpan RefreshTokenLifetime { get; init; }
    public TimeSpan TokenCooldownLifeTime { get; init; }
}