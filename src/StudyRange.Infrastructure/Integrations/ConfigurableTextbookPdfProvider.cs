using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class ConfigurableTextbookPdfProvider : ITextbookPdfProvider
{
    private readonly HttpClient _httpClient;
    private readonly TextbookPdfOptions _options;

    public string SourceName => "TEXTBOOK-PDF";

    public ConfigurableTextbookPdfProvider(HttpClient httpClient, IOptions<TextbookPdfOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SourceFetchResult<TextbookPdfAssetModel>> FetchTextbookPdfAsync(
        GeneratedContentContextModel context,
        CancellationToken cancellationToken)
    {
        var fileName = BuildCatalogFileName(context);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new SourceFetchResult<TextbookPdfAssetModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: "개정년도/과목/출판사 메타데이터가 부족해 파일 키를 만들 수 없습니다.");
        }

        var catalogPath = ResolveCatalogPath(fileName);
        if (File.Exists(catalogPath))
        {
            var bytes = await File.ReadAllBytesAsync(catalogPath, cancellationToken);
            return new SourceFetchResult<TextbookPdfAssetModel>(
                Source: SourceName,
                Items: [new TextbookPdfAssetModel(fileName, bytes)],
                Success: true,
                Message: "로컬 카탈로그에서 교과서 PDF를 찾았습니다.");
        }

        if (!string.Equals(_options.Provider, "HttpTemplate", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_options.UrlTemplate))
        {
            return new SourceFetchResult<TextbookPdfAssetModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: $"로컬 카탈로그에 `{fileName}`가 없고, HttpTemplate 공급자가 비활성화되어 있습니다.");
        }

        var url = BuildUrlFromTemplate(_options.UrlTemplate, context);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey) && !string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return new SourceFetchResult<TextbookPdfAssetModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: $"원격 교과서 API 호출 실패: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new SourceFetchResult<TextbookPdfAssetModel>(
                    Source: SourceName,
                    Items: [],
                    Success: false,
                    Message: $"원격 교과서 API 응답 실패: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!LooksLikePdf(bytes))
            {
                return new SourceFetchResult<TextbookPdfAssetModel>(
                    Source: SourceName,
                    Items: [],
                    Success: false,
                    Message: "원격 응답이 PDF 형식이 아닙니다.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath) ?? ".");
            await File.WriteAllBytesAsync(catalogPath, bytes, cancellationToken);
            return new SourceFetchResult<TextbookPdfAssetModel>(
                Source: SourceName,
                Items: [new TextbookPdfAssetModel(fileName, bytes)],
                Success: true,
                Message: "원격 교과서 PDF를 내려받아 로컬 카탈로그에 저장했습니다.");
        }
    }

    private string ResolveCatalogPath(string fileName)
    {
        var directory = string.IsNullOrWhiteSpace(_options.CatalogDirectory)
            ? "App_Data/textbook-catalog"
            : _options.CatalogDirectory;
        return Path.Combine(directory, fileName);
    }

    private static string BuildCatalogFileName(GeneratedContentContextModel context)
    {
        if (string.IsNullOrWhiteSpace(context.CurriculumRevision) ||
            string.IsNullOrWhiteSpace(context.Subject) ||
            string.IsNullOrWhiteSpace(context.TextbookPublisher))
        {
            return string.Empty;
        }

        var revision = Sanitize(context.CurriculumRevision);
        var subject = Sanitize(context.Subject);
        var publisher = Sanitize(context.TextbookPublisher);
        return $"{revision}_{subject}_{publisher}.pdf";
    }

    private static string BuildUrlFromTemplate(string template, GeneratedContentContextModel context)
    {
        return template
            .Replace("{revision}", Uri.EscapeDataString(context.CurriculumRevision ?? string.Empty), StringComparison.Ordinal)
            .Replace("{subject}", Uri.EscapeDataString(context.Subject), StringComparison.Ordinal)
            .Replace("{publisher}", Uri.EscapeDataString(context.TextbookPublisher ?? string.Empty), StringComparison.Ordinal)
            .Replace("{schoolLevel}", Uri.EscapeDataString(context.SchoolLevel.ToString()), StringComparison.Ordinal)
            .Replace("{grade}", Uri.EscapeDataString(context.Grade.ToString()), StringComparison.Ordinal)
            .Replace("{birthYear}", Uri.EscapeDataString(context.BirthYear?.ToString() ?? string.Empty), StringComparison.Ordinal)
            .Replace("{schoolName}", Uri.EscapeDataString(context.SchoolName ?? string.Empty), StringComparison.Ordinal);
    }

    private static string Sanitize(string raw)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(raw.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return cleaned.Replace(' ', '_');
    }

    private static bool LooksLikePdf(byte[] content)
    {
        return content.Length >= 4 &&
               content[0] == 0x25 &&
               content[1] == 0x50 &&
               content[2] == 0x44 &&
               content[3] == 0x46;
    }
}
