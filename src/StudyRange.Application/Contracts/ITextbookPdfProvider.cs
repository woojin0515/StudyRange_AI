using StudyRange.Application.UseCases;

namespace StudyRange.Application.Contracts;

public interface ITextbookPdfProvider
{
    string SourceName { get; }

    Task<SourceFetchResult<TextbookPdfAssetModel>> FetchTextbookPdfAsync(
        GeneratedContentContextModel context,
        CancellationToken cancellationToken);
}
