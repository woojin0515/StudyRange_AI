using System.Threading.Channels;
using StudyRange.Application.Contracts;

namespace StudyRange.Infrastructure.Processing;

public sealed class InMemoryProcessingQueue : IProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
