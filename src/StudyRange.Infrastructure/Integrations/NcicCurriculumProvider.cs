using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class NcicCurriculumProvider : IEducationCurriculumProvider
{
    public string SourceName => "NCIC";

    public Task<SourceFetchResult<CurriculumRevisionModel>> FetchCurriculumsAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        var items = new List<CurriculumRevisionModel>
        {
            new("2015", "2015 개정 교육과정", SourceName, "중/고등학교에서 단계적 적용"),
            new("2022", "2022 개정 교육과정", SourceName, "2025년 이후 단계적 적용")
        };

        var inferred = InferRevision(query.BirthYear, query.Grade);
        if (!string.IsNullOrWhiteSpace(inferred))
        {
            items.Insert(0, new CurriculumRevisionModel(
                RevisionCode: inferred,
                DisplayName: $"{inferred} 개정 (추정)",
                Source: SourceName,
                Description: "출생년도/학년 기반 추정값"));
        }

        return Task.FromResult(new SourceFetchResult<CurriculumRevisionModel>(
            Source: SourceName,
            Items: items,
            Success: true,
            Message: "개정 교육과정 기본 목록을 제공했습니다."));
    }

    private static string? InferRevision(int? birthYear, int grade)
    {
        if (!birthYear.HasValue || grade <= 0)
        {
            return null;
        }

        // 초1 입학년도를 기준으로 단순 추정
        var entryYear = birthYear.Value + 7;
        var currentYear = DateTime.UtcNow.Year;
        var schoolYear = entryYear + grade - 1;
        if (schoolYear >= 2025 || currentYear >= 2025)
        {
            return "2022";
        }

        return "2015";
    }
}
