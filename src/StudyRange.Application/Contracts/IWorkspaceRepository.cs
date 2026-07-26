using StudyRange.Domain.Entities;

namespace StudyRange.Application.Contracts;

public interface IWorkspaceRepository
{
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);
    Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken);
}
