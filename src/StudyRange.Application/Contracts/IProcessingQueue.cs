namespace StudyRange.Application.Contracts;

public interface IProcessingQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
