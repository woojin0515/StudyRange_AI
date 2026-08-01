using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Infrastructure.Integrations;
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
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<KotryApiOptions>(configuration.GetSection(KotryApiOptions.SectionName));
        services.Configure<NcicApiOptions>(configuration.GetSection(NcicApiOptions.SectionName));
        services.Configure<NeisApiOptions>(configuration.GetSection(NeisApiOptions.SectionName));

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

        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        var ocrOptions = configuration.GetSection(OcrOptions.SectionName).Get<OcrOptions>() ?? new OcrOptions();
        services.AddSingleton(llmOptions);
        services.AddSingleton(ocrOptions);
        services.AddHttpClient<AzureOpenAiStudyContentGenerator>();
        services.AddHttpClient<KotryTextbookProvider>();
        services.AddHttpClient<NeisSchoolContextProvider>();
        if (!string.Equals(llmOptions.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddTransient<IStudyContentGenerator, AzureOpenAiStudyContentGenerator>();
        }
        else
        {
            services.AddSingleton<IStudyContentGenerator, MockStudyContentGenerator>();
        }

        var kotryOptions = configuration.GetSection(KotryApiOptions.SectionName).Get<KotryApiOptions>() ?? new KotryApiOptions();
        var ncicOptions = configuration.GetSection(NcicApiOptions.SectionName).Get<NcicApiOptions>() ?? new NcicApiOptions();
        var neisOptions = configuration.GetSection(NeisApiOptions.SectionName).Get<NeisApiOptions>() ?? new NeisApiOptions();

        services.AddSingleton(kotryOptions);
        services.AddSingleton(ncicOptions);
        services.AddSingleton(neisOptions);

        services.AddSingleton<IEducationCurriculumProvider, NcicCurriculumProvider>();
        if (string.IsNullOrWhiteSpace(kotryOptions.ApiKey))
        {
            services.AddSingleton<IEducationTextbookProvider, MockTextbookProvider>();
        }
        else
        {
            services.AddTransient<IEducationTextbookProvider, KotryTextbookProvider>();
        }
        services.AddSingleton<IEducationSchoolContextProvider, NeisSchoolContextProvider>();

        services.AddHostedService<DocumentProcessingWorker>();
        return services;
    }
}
