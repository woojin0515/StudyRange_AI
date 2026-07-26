using System.Xml.Linq;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class KotryTextbookProvider : IEducationTextbookProvider
{
    private readonly HttpClient _httpClient;
    private readonly KotryApiOptions _options;

    public string SourceName => "KOTRY";

    public KotryTextbookProvider(HttpClient httpClient, KotryApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<SourceFetchResult<TextbookCatalogModel>> FetchTextbooksAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new SourceFetchResult<TextbookCatalogModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: "KOTRY API 키가 설정되지 않았습니다.");
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var uri =
            $"{baseUrl}/openapi/search/book.do?key={Uri.EscapeDataString(_options.ApiKey)}&pageUnit=20&pageIndex=1&f1=title&v1={Uri.EscapeDataString(query.Subject)}";

        try
        {
            var xml = await _httpClient.GetStringAsync(uri, cancellationToken);
            var doc = XDocument.Parse(xml);

            var books = doc
                .Descendants()
                .Where(x => x.Name.LocalName.Equals("book", StringComparison.OrdinalIgnoreCase))
                .Select(x => new TextbookCatalogModel(
                    Subject: query.Subject,
                    Publisher: GetElementValue(x, "publisher") ?? GetElementValue(x, "publer") ?? "미상",
                    Title: GetElementValue(x, "title") ?? "제목 없음",
                    Isbn: GetElementValue(x, "isbn"),
                    CurriculumRevision: GetElementValue(x, "edc_crse") ?? "미상",
                    Source: SourceName))
                .ToList();

            if (books.Count == 0)
            {
                return new SourceFetchResult<TextbookCatalogModel>(
                    Source: SourceName,
                    Items: [],
                    Success: false,
                    Message: "KOTRY 응답에서 교과서 항목을 찾지 못했습니다.");
            }

            return new SourceFetchResult<TextbookCatalogModel>(
                Source: SourceName,
                Items: books,
                Success: true,
                Message: $"{books.Count}건의 교과서 메타데이터를 수집했습니다.");
        }
        catch (Exception ex)
        {
            return new SourceFetchResult<TextbookCatalogModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: $"KOTRY 호출 실패: {ex.Message}");
        }
    }

    private static string? GetElementValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
    }
}
