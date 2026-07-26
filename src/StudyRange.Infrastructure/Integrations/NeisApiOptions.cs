namespace StudyRange.Infrastructure.Integrations;

public sealed class NeisApiOptions
{
    public const string SectionName = "EducationApis:Neis";
    public string BaseUrl { get; set; } = "https://open.neis.go.kr";
    public string? ApiKey { get; set; }
}
