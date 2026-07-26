namespace StudyRange.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";
    public string Provider { get; set; } = "InMemory";
    public string? PostgreSqlConnectionString { get; set; }
}
