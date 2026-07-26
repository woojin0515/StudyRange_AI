using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;

namespace StudyRange.Infrastructure.Integrations;

public sealed class MockTextbookProvider : IEducationTextbookProvider
{
    public string SourceName => "MOCK-TEXTBOOK";

    public Task<SourceFetchResult<TextbookCatalogModel>> FetchTextbooksAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        var items = new List<TextbookCatalogModel>
        {
            new(query.Subject, "미래엔", $"{query.Subject} 교과서 A", null, "2022", SourceName),
            new(query.Subject, "비상교육", $"{query.Subject} 교과서 B", null, "2015", SourceName),
            new(query.Subject, "천재교육", $"{query.Subject} 교과서 C", null, "2022", SourceName)
        };

        return Task.FromResult(new SourceFetchResult<TextbookCatalogModel>(
            Source: SourceName,
            Items: items,
            Success: true,
            Message: "개발용 샘플 교과서 데이터를 반환했습니다."));
    }
}
