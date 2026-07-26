using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class NeisSchoolContextProvider : IEducationSchoolContextProvider
{
    private readonly NeisApiOptions _options;

    public string SourceName => "NEIS";

    public NeisSchoolContextProvider(NeisApiOptions options)
    {
        _options = options;
    }

    public Task<SourceFetchResult<SchoolContextFactModel>> FetchSchoolContextAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Task.FromResult(new SourceFetchResult<SchoolContextFactModel>(
                Source: SourceName,
                Items: [],
                Success: false,
                Message: "NEIS API 키가 설정되지 않았습니다."));
        }

        var facts = new List<SchoolContextFactModel>
        {
            new("school_level", query.SchoolLevel.ToString(), SourceName),
            new("grade", query.Grade.ToString(), SourceName),
            new("school_name", string.IsNullOrWhiteSpace(query.SchoolName) ? "미입력" : query.SchoolName, SourceName)
        };

        return Task.FromResult(new SourceFetchResult<SchoolContextFactModel>(
            Source: SourceName,
            Items: facts,
            Success: true,
            Message: "NEIS 기본 학교 맥락 정보를 구성했습니다."));
    }
}
