namespace StudyRange.Infrastructure.Integrations;

public sealed class KotryApiOptions
{
    public const string SectionName = "EducationApis:Kotry";
    public string BaseUrl { get; set; } = "http://www.kotry.kr";
    public string? ApiKey { get; set; }
}
