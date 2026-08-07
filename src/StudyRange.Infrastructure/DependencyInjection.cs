using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Infrastructure.HealthChecks;
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
        services.PostConfigure<LlmOptions>(options =>
        {
            options.ApiKey = FirstNonEmpty(options.ApiKey,
                configuration["OPENAI_API_KEY"],
                configuration["AZURE_OPENAI_API_KEY"],
                configuration["AZURE_OPENAI_KEY"],
                configuration["OPENAI_KEY"],
                configuration["LLM_API_KEY"]);

            options.Endpoint = FirstNonEmpty(options.Endpoint,
                configuration["OPENAI_BASE_URL"],
                configuration["AZURE_OPENAI_ENDPOINT"],
                configuration["OPENAI_ENDPOINT"],
                configuration["AZURE_OPENAI_BASE_URL"]);

            options.Model = FirstNonEmpty(options.Model,
                configuration["OPENAI_MODEL"],
                configuration["AZURE_OPENAI_MODEL"],
                configuration["LLM_MODEL"]) ?? options.Model;

            options.Deployment = FirstNonEmpty(options.Deployment,
                configuration["AZURE_OPENAI_DEPLOYMENT"]);
        });
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<KotryApiOptions>(configuration.GetSection(KotryApiOptions.SectionName));
        services.Configure<NcicApiOptions>(configuration.GetSection(NcicApiOptions.SectionName));
        services.Configure<NeisApiOptions>(configuration.GetSection(NeisApiOptions.SectionName));

        var persistenceOptions = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new PersistenceOptions();
        services.AddSingleton(persistenceOptions);

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
        services.AddHealthChecks()
            .AddCheck<ConfigurationHealthCheck>("configuration")
            .AddCheck<StorageHealthCheck>("storage")
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var normalized = Normalize(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
             (trimmed.StartsWith('\'') && trimmed.EndsWith('\''))))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed;
    }
}
