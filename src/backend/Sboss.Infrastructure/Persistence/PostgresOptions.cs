namespace Sboss.Infrastructure.Persistence;

public sealed class PostgresOptions
{
    public const string SectionName = "ConnectionStrings";
    public string Default { get; init; } = string.Empty;
}
