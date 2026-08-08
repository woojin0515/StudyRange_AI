using StudyRange.Domain.Entities;

namespace StudyRange.Application.Contracts;

public interface IWorkspaceRepository
{
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);
    Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken);
    Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> ListSummariesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ExamRange>> ListExamRangesAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentAsset>> ListDocumentsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GeneratedContentArtifact>> ListGeneratedContentsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddExamRangeAsync(Guid workspaceId, ExamRange examRange, CancellationToken cancellationToken);
    Task AddDocumentAsync(DocumentAsset document, CancellationToken cancellationToken);
    Task UpdateDocumentProcessingAsync(Guid workspaceId, Guid documentId, ProcessingStatus status, string? summary, CancellationToken cancellationToken);
    Task AddGeneratedContentAsync(GeneratedContentArtifact content, CancellationToken cancellationToken);
    Task<bool> DeleteGeneratedContentAsync(Guid workspaceId, Guid generatedContentId, CancellationToken cancellationToken);
    Task<WorkspaceDashboardSnapshot> GetDashboardSnapshotAsync(int recentCount, CancellationToken cancellationToken);
}

public sealed record WorkspaceDashboardSnapshot(
    int WorkspaceCount,
    int ExamRangeCount,
    int DocumentCount,
    int GeneratedCount,
    IReadOnlyList<Workspace> RecentWorkspaces);
