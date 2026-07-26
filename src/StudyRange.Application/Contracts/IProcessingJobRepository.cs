using StudyRange.Domain.Entities;

namespace StudyRange.Application.Contracts;

public interface IProcessingJobRepository
{
    Task AddAsync(ProcessingJob job, CancellationToken cancellationToken);
    Task<ProcessingJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessingJob>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken);
}
