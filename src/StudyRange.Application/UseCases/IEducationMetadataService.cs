namespace StudyRange.Application.UseCases;

public interface IEducationMetadataService
{
    Task<EducationMetadataBundleModel> CollectAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken);
}
