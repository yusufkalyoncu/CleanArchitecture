using CleanArchitecture.Application.Abstractions.Option;

namespace CleanArchitecture.Infrastructure.RateLimiting;

public sealed class RateLimitOptions : IAppOption
{
    public const string SectionName = "RateLimitOptions";
    public PolicySettings Global { get; init; } = new(100, 60);
    public PolicySettings Login { get; init; } = new(5, 180);
    public PolicySettings Registration { get; init; } = new(5, 600);
}

public record PolicySettings(int PermitLimit, int WindowInSeconds);