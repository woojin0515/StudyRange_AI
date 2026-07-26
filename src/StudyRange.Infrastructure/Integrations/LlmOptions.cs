namespace StudyRange.Infrastructure.Integrations;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";
    public string Provider { get; set; } = "Mock";
    public string Model { get; set; } = "mock-korean-study-v1";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Deployment { get; set; }
}
