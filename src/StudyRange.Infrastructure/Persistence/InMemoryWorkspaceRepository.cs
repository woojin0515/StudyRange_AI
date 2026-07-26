using System.Collections.Concurrent;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;

namespace StudyRange.Infrastructure.Persistence;

public sealed class InMemoryWorkspaceRepository : IWorkspaceRepository
{
    private readonly ConcurrentDictionary<Guid, Workspace> _store = new();

    public Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        if (!_store.TryAdd(workspace.Id, workspace))
        {
            throw new InvalidOperationException("Workspace with same id already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        _store[workspace.Id] = workspace;
        return Task.CompletedTask;
    }

    public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        _store.TryGetValue(workspaceId, out var workspace);
        return Task.FromResult(workspace);
    }

    public Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Workspace> result = _store.Values.ToList();
        return Task.FromResult(result);
    }
}
