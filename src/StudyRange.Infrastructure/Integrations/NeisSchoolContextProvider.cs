using System.Text.Json;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class NeisSchoolContextProvider : IEducationSchoolContextProvider
{
    private readonly HttpClient _httpClient;
    private readonly NeisApiOptions _options;

    public string SourceName => "NEIS";

    public NeisSchoolContextProvider(HttpClient httpClient, NeisApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<SourceFetchResult<SchoolContextFactModel>> FetchSchoolContextAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new SourceFetchResult<SchoolContextFactModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: "NEIS API 키가 설정되지 않았습니다.");
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var level = MapSchoolLevel(query.SchoolLevel);
        var uri = $"{baseUrl}/hub/schoolInfo?KEY={Uri.EscapeDataString(_options.ApiKey)}&Type=json&pIndex=1&pSize=5&SCHUL_KND_SC_NM={Uri.EscapeDataString(level)}";
        if (!string.IsNullOrWhiteSpace(query.SchoolName))
        {
            uri += $"&SCHUL_NM={Uri.EscapeDataString(query.SchoolName)}";
        }

        try
        {
            var json = await _httpClient.GetStringAsync(uri, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var rows = TryGetRows(doc.RootElement);
            if (rows.Count == 0)
            {
                return new SourceFetchResult<SchoolContextFactModel>(
                    Source: SourceName,
                    Items: BuildFallbackFacts(query),
                    Success: false,
                    Message: "NEIS에서 학교 정보를 찾지 못했습니다.");
            }

            var school = rows[0];
            var facts = new List<SchoolContextFactModel>
            {
                new("school_level", query.SchoolLevel.ToString(), SourceName),
                new("grade", query.Grade.ToString(), SourceName),
                new("school_name", GetString(school, "SCHUL_NM") ?? (query.SchoolName ?? "미입력"), SourceName),
                new("education_office", GetString(school, "ATPT_OFCDC_SC_NM") ?? "미상", SourceName),
                new("district", GetString(school, "LCTN_SC_NM") ?? "미상", SourceName)
            };

            return new SourceFetchResult<SchoolContextFactModel>(
                Source: SourceName,
                Items: facts,
                Success: true,
                Message: "NEIS 학교 맥락 정보를 조회했습니다.");
        }
        catch (HttpRequestException ex)
        {
            return new SourceFetchResult<SchoolContextFactModel>(
                Source: SourceName,
                Items: BuildFallbackFacts(query),
                Success: false,
                Message: $"NEIS 호출 실패: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return new SourceFetchResult<SchoolContextFactModel>(
                Source: SourceName,
                Items: BuildFallbackFacts(query),
                Success: false,
                Message: $"NEIS 응답 파싱 실패: {ex.Message}");
        }
    }

    private static List<SchoolContextFactModel> BuildFallbackFacts(EducationMetadataQueryModel query)
    {
        return
        [
            new("school_level", query.SchoolLevel.ToString(), "NEIS"),
            new("grade", query.Grade.ToString(), "NEIS"),
            new("school_name", string.IsNullOrWhiteSpace(query.SchoolName) ? "미입력" : query.SchoolName, "NEIS")
        ];
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

    private static List<JsonElement> TryGetRows(JsonElement root)
    {
        if (!root.TryGetProperty("schoolInfo", out var schoolInfo) || schoolInfo.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var block in schoolInfo.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object &&
                block.TryGetProperty("row", out var rows) &&
                rows.ValueKind == JsonValueKind.Array)
            {
                return rows.EnumerateArray().ToList();
            }
        }

        return [];
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
