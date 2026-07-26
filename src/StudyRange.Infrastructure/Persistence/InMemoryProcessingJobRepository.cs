using System.Collections.Concurrent;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;

namespace StudyRange.Infrastructure.Persistence;

public sealed class InMemoryProcessingJobRepository : IProcessingJobRepository
{
    private readonly ConcurrentDictionary<Guid, ProcessingJob> _jobs = new();

    public Task AddAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        if (!_jobs.TryAdd(job.Id, job))
        {
            throw new InvalidOperationException("Processing job with same id already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<ProcessingJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<ProcessingJob>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProcessingJob> result = _jobs.Values.Where(j => j.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(result);
    }
}
