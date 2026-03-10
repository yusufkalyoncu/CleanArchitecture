using CleanArchitecture.Shared;
using FluentValidation;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class JwtOptions : IAppOption
{
    public const string SectionName = "JwtOptions";
    
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SecretKey { get; init; }
    public TimeSpan AccessTokenLifeTime { get; init; }
    public TimeSpan RefreshTokenLifetime { get; init; }
    public TimeSpan GracePeriodLifeTime { get; init; }
}

internal sealed class JwtOptionsValidator : AbstractValidator<JwtOptions>
{
    public JwtOptionsValidator()
    {
        RuleFor(x => x.Issuer)
            .NotEmpty().WithMessage("JWT Issuer is required.");

        RuleFor(x => x.Audience)
            .NotEmpty().WithMessage("JWT Audience is required.");

        RuleFor(x => x.SecretKey)
            .NotEmpty().WithMessage("JWT SecretKey is required.")
            .MinimumLength(32).WithMessage("SecretKey must be at least 32 characters for security (HMACSHA256).");

        RuleFor(x => x.AccessTokenLifeTime)
            .GreaterThan(TimeSpan.Zero).WithMessage("AccessTokenLifeTime must be greater than zero.");

        RuleFor(x => x.RefreshTokenLifetime)
            .GreaterThan(x => x.AccessTokenLifeTime)
            .WithMessage("RefreshTokenLifetime must be longer than AccessTokenLifeTime.");

        RuleFor(x => x.GracePeriodLifeTime)
            .NotNull().WithMessage("GracePeriodLifeTime must be defined.");
    }
}