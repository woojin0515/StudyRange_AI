using StudyRange.Application.UseCases;

namespace StudyRange.Application.Contracts;

public interface IEducationTextbookProvider
{
    string SourceName { get; }
    Task<SourceFetchResult<TextbookCatalogModel>> FetchTextbooksAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken);
}
