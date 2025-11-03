using CleanArchitecture.Application.Abstractions.Option;

namespace CleanArchitecture.Infrastructure.Database;

public sealed class PostgresOptions : IAppOption
{
    public const string SectionName = "PostgresOptions";

    public string UserId { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string Host { get; init; } = default!;
    public int Port { get; init; }
    public string Database { get; init; } = default!;

    public string ConnectionString =>
        $"User ID={UserId};Password={Password};Host={Host};Port={Port};Database={Database};";
}