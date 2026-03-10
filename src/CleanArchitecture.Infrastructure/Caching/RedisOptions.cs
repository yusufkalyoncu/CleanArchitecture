using CleanArchitecture.Shared;
using FluentValidation;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class RedisOptions : IAppOption
{
    public const string SectionName = "RedisOptions";

    public string Host { get; init; } = null!;
    public int Port { get; init; }
    public string InstanceName { get; init; } = null!;
    public int Database { get; init; }
    public bool AbortOnConnectFail { get; init; }
    public int ConnectTimeout { get; init; }
    public int SyncTimeout { get; init; }
    public TimeSpan DefaultExpiration { get; init; }
    
    public string ConnectionString => $"{Host}:{Port}";
}

internal sealed class RedisOptionsValidator : AbstractValidator<RedisOptions>
{
    public RedisOptionsValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty().WithMessage("Redis Host is required.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Redis Port must be between 1 and 65535.");

        RuleFor(x => x.InstanceName)
            .NotEmpty().WithMessage("Redis InstanceName (Prefix) is required to avoid cache collisions.");

        RuleFor(x => x.Database)
            .InclusiveBetween(0, 15).WithMessage("Standard Redis supports databases 0 through 15.");

        RuleFor(x => x.ConnectTimeout)
            .GreaterThan(0).WithMessage("ConnectTimeout must be a positive integer (ms).");

        RuleFor(x => x.SyncTimeout)
            .GreaterThan(0).WithMessage("SyncTimeout must be a positive integer (ms).");

        RuleFor(x => x.DefaultExpiration)
            .GreaterThan(TimeSpan.Zero).WithMessage("DefaultExpiration must be greater than zero.");
    }
}