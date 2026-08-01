using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StudyRange.Infrastructure.Integrations;

namespace StudyRange.Infrastructure.HealthChecks;

public sealed class ConfigurationHealthCheck : IHealthCheck
{
    private readonly LlmOptions _llmOptions;
    private readonly OcrOptions _ocrOptions;

    public ConfigurationHealthCheck(IOptions<LlmOptions> llmOptions, IOptions<OcrOptions> ocrOptions)
    {
        _llmOptions = llmOptions.Value;
        _ocrOptions = ocrOptions.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        if (!string.Equals(_llmOptions.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_llmOptions.ApiKey))
            {
                issues.Add("Llm:ApiKey is empty.");
            }

            if (string.IsNullOrWhiteSpace(_llmOptions.Model))
            {
                issues.Add("Llm:Model is empty.");
            }
        }

        if (string.Equals(_ocrOptions.Provider, "AzureVision", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_ocrOptions.Endpoint))
            {
                issues.Add("Ocr:Endpoint is empty.");
            }

            if (string.IsNullOrWhiteSpace(_ocrOptions.ApiKey))
            {
                issues.Add("Ocr:ApiKey is empty.");
            }
        }

        if (issues.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Configuration looks valid."));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            "Configuration has missing required values.",
            data: new Dictionary<string, object>
            {
                ["issues"] = issues
            }));
    }
}
