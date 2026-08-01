using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StudyRange.Infrastructure.Storage;

namespace StudyRange.Infrastructure.HealthChecks;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly StorageOptions _storageOptions;

    public StorageHealthCheck(IOptions<StorageOptions> storageOptions)
    {
        _storageOptions = storageOptions.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_storageOptions.RootDirectory))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage:RootDirectory is empty."));
        }

        try
        {
            Directory.CreateDirectory(_storageOptions.RootDirectory);
            var probePath = Path.Combine(_storageOptions.RootDirectory, $".healthcheck-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return Task.FromResult(HealthCheckResult.Healthy("Storage directory is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage directory is not writable.", ex));
        }
    }
}
