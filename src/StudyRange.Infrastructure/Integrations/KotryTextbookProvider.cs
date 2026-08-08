using System.Xml.Linq;
using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class KotryTextbookProvider : IEducationTextbookProvider
{
    private readonly HttpClient _httpClient;
    private readonly KotryApiOptions _options;
    private static readonly string[] SearchEndpoints =
    [
        "/openapi/search/openTextBook.do",
        "/openapi/search/book.do"
    ];

    public string SourceName => "KOTRY";

    public KotryTextbookProvider(HttpClient httpClient, IOptions<KotryApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
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
        var queryString = BuildQueryString(query);

        try
        {
            var errors = new List<string>();
            foreach (var endpoint in SearchEndpoints)
            {
                var uri = $"{baseUrl}{endpoint}?{queryString}";
                string xml;
                try
                {
                    xml = await _httpClient.GetStringAsync(uri, cancellationToken);
                }
                catch (Exception ex)
                {
                    errors.Add($"{endpoint}: {ex.Message}");
                    continue;
                }

                var doc = XDocument.Parse(xml);
                var books = ParseBooks(doc, query.Subject, SourceName);
                if (books.Count > 0)
                {
                    return new SourceFetchResult<TextbookCatalogModel>(
                        Source: SourceName,
                        Items: books,
                        Success: true,
                        Message: $"{books.Count}건의 교과서 메타데이터를 수집했습니다. ({endpoint})");
                }

                var result = doc.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("result", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                var count = doc.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("count", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                errors.Add($"{endpoint}: result={result ?? "N/A"}, count={count ?? "N/A"}");
            }

            return new SourceFetchResult<TextbookCatalogModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: $"KOTRY 응답에서 교과서 항목을 찾지 못했습니다. ({string.Join(" | ", errors)})");
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

    private string BuildQueryString(EducationMetadataQueryModel query)
    {
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("key", _options.ApiKey),
            new("pageUnit", "50"),
            new("pageIndex", "1"),
            // KOTRY 문서: 검색조건(f1) + 검색어(v1) 중 최소 1쌍 필수
            new("f1", "keyword"),
            new("v1", query.Subject),
            new("schulGrad", MapSchoolLevel(query.SchoolLevel)),
            new("sort", "ipub_year"),
            new("desc", "desc")
        };

        var filtered = parameters
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}");
        return string.Join("&", filtered);
    }

    private static List<TextbookCatalogModel> ParseBooks(XDocument doc, string subject, string sourceName)
    {
        var nodes = doc
            .Descendants()
            .Where(x => x.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase) ||
                        x.Name.LocalName.Equals("book", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return nodes
            .Select(x => new TextbookCatalogModel(
                Subject: subject,
                Publisher: FirstNonEmpty(
                    GetElementValue(x, "publisher"),
                    GetElementValue(x, "ipublisher"),
                    GetElementValue(x, "publer")) ?? "미상",
                Title: FirstNonEmpty(
                    GetElementValue(x, "title"),
                    GetElementValue(x, "ititle")) ?? "제목 없음",
                Isbn: GetElementValue(x, "isbn"),
                CurriculumRevision: FirstNonEmpty(
                    GetElementValue(x, "edcCrse"),
                    GetElementValue(x, "edc_crse")) ?? "미상",
                Source: sourceName))
            .ToList();
    }

    private static string MapSchoolLevel(SchoolLevel schoolLevel)
    {
        return schoolLevel switch
        {
            SchoolLevel.Elementary => "초등학교",
            SchoolLevel.Middle => "중학교",
            SchoolLevel.High => "고등학교",
            _ => "중학교"
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetElementValue(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
    }
}
