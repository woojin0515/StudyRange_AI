namespace StudyRange.Infrastructure.Integrations;

public sealed class OcrOptions
{
    public const string SectionName = "Ocr";
    public string Provider { get; set; } = "None";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
}
