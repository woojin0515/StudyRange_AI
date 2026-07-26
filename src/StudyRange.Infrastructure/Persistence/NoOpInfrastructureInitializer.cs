namespace StudyRange.Infrastructure.Persistence;

public sealed class NoOpInfrastructureInitializer : IInfrastructureInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
