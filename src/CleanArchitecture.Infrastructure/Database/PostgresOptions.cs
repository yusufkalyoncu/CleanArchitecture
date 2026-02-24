using CleanArchitecture.Application.Abstractions.Option;

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