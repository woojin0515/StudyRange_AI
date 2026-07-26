using StudyRange.Application.UseCases;

namespace StudyRange.Application.Contracts;

public interface IEducationCurriculumProvider
{
    string SourceName { get; }
    Task<SourceFetchResult<CurriculumRevisionModel>> FetchCurriculumsAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken);
}

public sealed record SourceFetchResult<T>(
    string Source,
    IReadOnlyList<T> Items,
    bool Success,
    string Message);
