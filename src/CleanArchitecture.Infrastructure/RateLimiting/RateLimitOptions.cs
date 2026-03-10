using CleanArchitecture.Shared;
using FluentValidation;

namespace CleanArchitecture.Infrastructure.RateLimiting;

public sealed class RateLimitOptions : IAppOption
{
    public const string SectionName = "RateLimitOptions";
    public PolicySettings Global { get; init; } = new(100, 60);
    public PolicySettings Login { get; init; } = new(5, 180);
    public PolicySettings Registration { get; init; } = new(5, 600);
}

public record PolicySettings(int PermitLimit, int WindowInSeconds);

internal sealed class RateLimitOptionsValidator : AbstractValidator<RateLimitOptions>
{
    public RateLimitOptionsValidator()
    {
        RuleFor(x => x.Global).SetValidator(new PolicySettingsValidator());
        RuleFor(x => x.Login).SetValidator(new PolicySettingsValidator());
        RuleFor(x => x.Registration).SetValidator(new PolicySettingsValidator());

        RuleFor(x => x.Login.PermitLimit)
            .LessThan(x => x.Global.PermitLimit)
            .WithMessage("Login rate limit should be stricter than the Global limit.");
    }
}

internal sealed class PolicySettingsValidator : AbstractValidator<PolicySettings>
{
    public PolicySettingsValidator()
    {
        RuleFor(x => x.PermitLimit)
            .GreaterThan(0)
            .WithMessage("PermitLimit must be at least 1.");

        RuleFor(x => x.WindowInSeconds)
            .GreaterThan(0)
            .WithMessage("WindowInSeconds must be a positive value.");
    }
}