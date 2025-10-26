using CleanArchitecture.Application.Abstractions.Option;

namespace CleanArchitecture.Application.Database;

public sealed class PostgresOptions : IAppOptions
{
    public const string SectionName = "PostgresOptions";

    public string UserId { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string Host { get; set; } = default!;
    public int Port { get; set; }
    public string Database { get; set; } = default!;

    public string ConnectionString =>
        $"User ID={UserId};Password={Password};Host={Host};Port={Port};Database={Database};";
}