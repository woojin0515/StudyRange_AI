using StudyRange.Domain.Entities;

namespace StudyRange.Application.Contracts;

public interface IWorkspaceRepository
{
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);
    Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken);
    Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken);
    Task<WorkspaceDashboardSnapshot> GetDashboardSnapshotAsync(int recentCount, CancellationToken cancellationToken);
}

public sealed record WorkspaceDashboardSnapshot(
    int WorkspaceCount,
    int ExamRangeCount,
    int DocumentCount,
    int GeneratedCount,
    IReadOnlyList<Workspace> RecentWorkspaces);
