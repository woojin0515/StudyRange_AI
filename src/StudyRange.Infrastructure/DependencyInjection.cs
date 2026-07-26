using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Infrastructure.Persistence;
using StudyRange.Infrastructure.Processing;
using StudyRange.Infrastructure.Storage;

namespace StudyRange.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var persistenceOptions = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new PersistenceOptions();

        if (string.Equals(persistenceOptions.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = persistenceOptions.PostgreSqlConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Persistence:PostgreSqlConnectionString is required when Provider=PostgreSql.");
            }

            services.AddSingleton(new PostgreSqlConnectionFactory(connectionString));
            services.AddSingleton<IWorkspaceRepository, PostgreSqlWorkspaceRepository>();
            services.AddSingleton<IProcessingJobRepository, PostgreSqlProcessingJobRepository>();
            services.AddSingleton<IInfrastructureInitializer, PostgreSqlInfrastructureInitializer>();
        }
        else
        {
            services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
            services.AddSingleton<IProcessingJobRepository, InMemoryProcessingJobRepository>();
            services.AddSingleton<IInfrastructureInitializer, NoOpInfrastructureInitializer>();
        }

        services.AddSingleton<IProcessingQueue, InMemoryProcessingQueue>();
        services.AddSingleton<IFileStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new LocalFileStorage(options.RootDirectory);
        });

        services.AddHostedService<DocumentProcessingWorker>();
        return services;
    }
}
