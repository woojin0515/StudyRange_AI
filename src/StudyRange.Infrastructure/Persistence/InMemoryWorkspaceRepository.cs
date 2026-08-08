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

    public Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        _store.TryRemove(workspaceId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_store.ContainsKey(workspaceId));
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

    public Task<IReadOnlyList<Workspace>> ListSummariesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Workspace> result = _store.Values
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new Workspace(x.Id, x.Name, x.CreatedAtUtc))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<WorkspaceReadBundle> GetWorkspaceReadBundleAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult(new WorkspaceReadBundle([], [], []));
        }

        return Task.FromResult(new WorkspaceReadBundle(
            ExamRanges: workspace.ExamRanges
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList(),
            Documents: workspace.Documents
                .OrderByDescending(x => x.UploadedAtUtc)
                .ToList(),
            GeneratedContents: workspace.GeneratedContents
                .OrderByDescending(x => x.GeneratedAtUtc)
                .ToList()));
    }

    public Task<WorkspaceDashboardSnapshot> GetDashboardSnapshotAsync(int recentCount, CancellationToken cancellationToken)
    {
        var ordered = _store.Values
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        var snapshot = new WorkspaceDashboardSnapshot(
            WorkspaceCount: ordered.Count,
            ExamRangeCount: ordered.Sum(x => x.ExamRanges.Count),
            DocumentCount: ordered.Sum(x => x.Documents.Count),
            GeneratedCount: ordered.Sum(x => x.GeneratedContents.Count),
            RecentWorkspaces: ordered.Take(Math.Max(recentCount, 0)).ToList());

        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ExamRange>> ListExamRangesAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult<IReadOnlyList<ExamRange>>([]);
        }

        IReadOnlyList<ExamRange> ranges = workspace.ExamRanges
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();
        return Task.FromResult(ranges);
    }

    public Task<IReadOnlyList<DocumentAsset>> ListDocumentsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult<IReadOnlyList<DocumentAsset>>([]);
        }

        IReadOnlyList<DocumentAsset> documents = workspace.Documents
            .OrderByDescending(x => x.UploadedAtUtc)
            .ToList();
        return Task.FromResult(documents);
    }

    public Task<IReadOnlyList<GeneratedContentArtifact>> ListGeneratedContentsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult<IReadOnlyList<GeneratedContentArtifact>>([]);
        }

        IReadOnlyList<GeneratedContentArtifact> generated = workspace.GeneratedContents
            .OrderByDescending(x => x.GeneratedAtUtc)
            .ToList();
        return Task.FromResult(generated);
    }

    public Task AddExamRangeAsync(Guid workspaceId, ExamRange examRange, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        workspace.AttachExamRange(examRange);
        return Task.CompletedTask;
    }

    public Task AddDocumentAsync(DocumentAsset document, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(document.WorkspaceId, out var workspace))
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        workspace.AttachDocument(document);
        return Task.CompletedTask;
    }

    public Task UpdateDocumentProcessingAsync(Guid workspaceId, Guid documentId, ProcessingStatus status, string? summary, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        var document = workspace.GetDocument(documentId);
        document.UpdateProcessing(status, summary);
        return Task.CompletedTask;
    }

    public Task AddGeneratedContentAsync(GeneratedContentArtifact content, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(content.WorkspaceId, out var workspace))
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        workspace.AttachGeneratedContent(content);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteGeneratedContentAsync(Guid workspaceId, Guid generatedContentId, CancellationToken cancellationToken)
    {
        if (!_store.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult(false);
        }

        try
        {
            workspace.RemoveGeneratedContent(generatedContentId);
            return Task.FromResult(true);
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }
}
