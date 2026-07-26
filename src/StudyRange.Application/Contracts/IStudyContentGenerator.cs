using StudyRange.Application.UseCases;
using StudyRange.Domain.Entities;

namespace StudyRange.Application.Contracts;

public interface IStudyContentGenerator
{
    Task<GeneratedStudyContentModel> GenerateAsync(
        Workspace workspace,
        ExamRange examRange,
        GeneratedContentType contentType,
        CancellationToken cancellationToken);
}
