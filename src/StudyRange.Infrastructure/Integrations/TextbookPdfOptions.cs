namespace StudyRange.Infrastructure.Integrations;

public sealed class TextbookPdfOptions
{
    public const string SectionName = "EducationApis:TextbookPdf";

    public string Provider { get; set; } = "LocalCatalog";
    public string CatalogDirectory { get; set; } = "App_Data/textbook-catalog";
    public string? UrlTemplate { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiKeyHeaderName { get; set; } = "X-API-Key";
}
