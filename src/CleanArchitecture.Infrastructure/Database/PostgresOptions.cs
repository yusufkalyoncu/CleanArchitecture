using CleanArchitecture.Shared;
using FluentValidation;

namespace CleanArchitecture.Infrastructure.Database;

public sealed class PostgresOptions : IAppOption
{
    public const string SectionName = "PostgresOptions";

    public string UserId { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string Host { get; init; } = null!;
    public int Port { get; init; }
    public string Database { get; init; } = null!;

    public string ConnectionString =>
        $"User ID={UserId};Password={Password};Host={Host};Port={Port};Database={Database};";
}

internal sealed class PostgresOptionsValidator : AbstractValidator<PostgresOptions>
{
    public PostgresOptionsValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Database UserId is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Database Password is required.");

        RuleFor(x => x.Host)
            .NotEmpty().WithMessage("Database Host (e.g., localhost or an IP) is required.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Database Port must be between 1 and 65535 (usually 5432).");

        RuleFor(x => x.Database)
            .NotEmpty().WithMessage("Database Name is required.");
    }
}