using StudyRange.Application.UseCases;

namespace StudyRange.Application.Contracts;

public interface IEducationSchoolContextProvider
{
    string SourceName { get; }
    Task<SourceFetchResult<SchoolContextFactModel>> FetchSchoolContextAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken);
}
