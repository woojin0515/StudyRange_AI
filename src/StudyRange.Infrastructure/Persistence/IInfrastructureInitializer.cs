namespace StudyRange.Infrastructure.Persistence;

public interface IInfrastructureInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
