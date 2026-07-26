using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyRange.Application.Contracts;
using StudyRange.Infrastructure.Persistence;
using StudyRange.Infrastructure.Processing;
using StudyRange.Infrastructure.Storage;

namespace StudyRange.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        services.AddSingleton<IProcessingJobRepository, InMemoryProcessingJobRepository>();
        services.AddSingleton<IProcessingQueue, InMemoryProcessingQueue>();
        services.AddSingleton<IFileStorage>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;
            return new LocalFileStorage(options.RootDirectory);
        });

        services.AddHostedService<DocumentProcessingWorker>();
        return services;
    }
}
