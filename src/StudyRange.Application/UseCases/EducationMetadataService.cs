using StudyRange.Application.Contracts;

namespace StudyRange.Application.UseCases;

public sealed class EducationMetadataService : IEducationMetadataService
{
    private readonly IReadOnlyList<IEducationCurriculumProvider> _curriculumProviders;
    private readonly IReadOnlyList<IEducationTextbookProvider> _textbookProviders;
    private readonly IReadOnlyList<IEducationSchoolContextProvider> _schoolContextProviders;

    public EducationMetadataService(
        IEnumerable<IEducationCurriculumProvider> curriculumProviders,
        IEnumerable<IEducationTextbookProvider> textbookProviders,
        IEnumerable<IEducationSchoolContextProvider> schoolContextProviders)
    {
        _curriculumProviders = curriculumProviders.ToList();
        _textbookProviders = textbookProviders.ToList();
        _schoolContextProviders = schoolContextProviders.ToList();
    }

    public async Task<EducationMetadataBundleModel> CollectAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        var curriculums = new List<CurriculumRevisionModel>();
        var textbooks = new List<TextbookCatalogModel>();
        var schoolFacts = new List<SchoolContextFactModel>();
        var statuses = new List<SourceStatusModel>();

        foreach (var provider in _curriculumProviders)
        {
            var result = await provider.FetchCurriculumsAsync(query, cancellationToken);
            curriculums.AddRange(result.Items);
            statuses.Add(new SourceStatusModel(result.Source, result.Success, result.Message));
        }

        foreach (var provider in _textbookProviders)
        {
            var result = await provider.FetchTextbooksAsync(query, cancellationToken);
            textbooks.AddRange(result.Items);
            statuses.Add(new SourceStatusModel(result.Source, result.Success, result.Message));
        }

        foreach (var provider in _schoolContextProviders)
        {
            var result = await provider.FetchSchoolContextAsync(query, cancellationToken);
            schoolFacts.AddRange(result.Items);
            statuses.Add(new SourceStatusModel(result.Source, result.Success, result.Message));
        }

        return new EducationMetadataBundleModel(
            Curriculums: curriculums,
            Textbooks: textbooks,
            SchoolFacts: schoolFacts,
            SourceStatuses: statuses);
    }
}
