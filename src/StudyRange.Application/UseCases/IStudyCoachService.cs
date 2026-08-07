using StudyRange.Domain.Entities;

namespace StudyRange.Application.UseCases;

public interface IStudyCoachService
{
    Task<IReadOnlyList<WorkspaceModel>> GetWorkspacesAsync(CancellationToken cancellationToken);
    Task<WorkspaceModel> CreateWorkspaceAsync(string name, CancellationToken cancellationToken);
    Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExamRangeModel>> GetExamRangesAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<ExamRangeModel> AddExamRangeAsync(Guid workspaceId, string subject, int startPage, int endPage, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentModel>> GetDocumentsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<DocumentModel> UploadDocumentAsync(
        Guid workspaceId,
        DocumentType documentType,
        string originalFileName,
        string contentType,
        long fileSize,
        Stream content,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessingJobModel>> GetProcessingJobsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task RetryProcessingJobAsync(Guid workspaceId, Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GeneratedContentHistoryModel>> GetGeneratedContentsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task DeleteGeneratedContentAsync(Guid workspaceId, Guid generatedContentId, CancellationToken cancellationToken);
    Task<GeneratedStudyContentModel> GenerateContentAsync(
        Guid workspaceId,
        Guid examRangeId,
        GeneratedContentType contentType,
        GeneratedContentContextModel context,
        CancellationToken cancellationToken);
    Task<EducationMetadataBundleModel> CollectEducationMetadataAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken);
}
