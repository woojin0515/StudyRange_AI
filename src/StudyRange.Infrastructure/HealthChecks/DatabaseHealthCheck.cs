using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudyRange.Infrastructure.Persistence;

namespace StudyRange.Infrastructure.HealthChecks;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly PersistenceOptions _persistenceOptions;
    private readonly PostgreSqlConnectionFactory? _connectionFactory;

    public DatabaseHealthCheck(PersistenceOptions persistenceOptions, PostgreSqlConnectionFactory? connectionFactory = null)
    {
        _persistenceOptions = persistenceOptions;
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_persistenceOptions.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("InMemory persistence is enabled.");
        }

        if (_connectionFactory is null)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection factory is not registered.");
        }

        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL connectivity is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connectivity failed.", ex);
        }
    }
}
