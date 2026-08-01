using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;
using StudyRange.Infrastructure.Integrations;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudyRange.Infrastructure.Processing;

public sealed class DocumentProcessingWorker : BackgroundService
{
    private readonly IProcessingQueue _processingQueue;
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OcrOptions _ocrOptions;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IProcessingQueue processingQueue,
        IProcessingJobRepository jobRepository,
        IWorkspaceRepository workspaceRepository,
        IHttpClientFactory httpClientFactory,
        IOptions<OcrOptions> ocrOptions,
        ILogger<DocumentProcessingWorker> logger)
    {
        _processingQueue = processingQueue;
        _jobRepository = jobRepository;
        _workspaceRepository = workspaceRepository;
        _httpClientFactory = httpClientFactory;
        _ocrOptions = ocrOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _processingQueue.DequeueAllAsync(stoppingToken))
        {
            ProcessingJob? job = null;
            try
            {
                job = await _jobRepository.GetByIdAsync(jobId, stoppingToken);
                if (job is null)
                {
                    _logger.LogWarning("Processing job {JobId} not found.", jobId);
                    continue;
                }

                job.MarkProcessing(DateTimeOffset.UtcNow);
                await _jobRepository.UpdateAsync(job, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

                var workspace = await _workspaceRepository.GetByIdAsync(job.WorkspaceId, stoppingToken);
                if (workspace is null)
                {
                    job.MarkFailed("Workspace not found during processing.", DateTimeOffset.UtcNow);
                    await _jobRepository.UpdateAsync(job, stoppingToken);
                    continue;
                }

                var document = workspace.GetDocument(job.DocumentId);
                var summary = await BuildProcessingSummaryAsync(document, stoppingToken);
                document.UpdateProcessing(
                    status: ProcessingStatus.Completed,
                    summary: summary);
                await _workspaceRepository.UpdateAsync(workspace, stoppingToken);

                job.MarkCompleted(DateTimeOffset.UtcNow);
                await _jobRepository.UpdateAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                if (job is not null)
                {
                    job.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
                    await _jobRepository.UpdateAsync(job, stoppingToken);
                }

                _logger.LogError(ex, "Unhandled error while processing a document job.");
            }
        }
    }

    private async Task<string> BuildProcessingSummaryAsync(DocumentAsset document, CancellationToken cancellationToken)
    {
        if (!File.Exists(document.StoredPath))
        {
            throw new InvalidOperationException($"업로드 파일을 찾을 수 없습니다: {document.StoredPath}");
        }

        var extension = Path.GetExtension(document.OriginalFileName).ToLowerInvariant();
        var fileInfo = new FileInfo(document.StoredPath);
        var sizeKb = Math.Max(1, fileInfo.Length / 1024);

        if (extension == ".pdf")
        {
            var bytes = await File.ReadAllBytesAsync(document.StoredPath, cancellationToken);
            var latin1 = Encoding.GetEncoding("iso-8859-1").GetString(bytes);
            var pageCount = Regex.Matches(latin1, @"\/Type\s*\/Page\b", RegexOptions.Compiled).Count;
            var textSample = ExtractTextSample(latin1);

            if (string.IsNullOrWhiteSpace(textSample))
            {
                return $"PDF 처리 완료: {sizeKb}KB, 페이지 추정 {Math.Max(1, pageCount)}쪽. 추출 가능한 텍스트 샘플이 없습니다.";
            }

            return $"PDF 처리 완료: {sizeKb}KB, 페이지 추정 {Math.Max(1, pageCount)}쪽. 텍스트 샘플: {textSample}";
        }

        if (IsImageExtension(extension))
        {
            var ocrResult = await TryExtractImageTextWithOcrAsync(document, cancellationToken);
            if (ocrResult.Configured && !string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                return $"이미지 OCR 처리 완료: {extension.TrimStart('.').ToUpperInvariant()} / {sizeKb}KB. 텍스트 샘플: {ocrResult.Text}";
            }

            if (ocrResult.Configured && !string.IsNullOrWhiteSpace(ocrResult.Error))
            {
                return $"이미지 처리 완료: {extension.TrimStart('.').ToUpperInvariant()} / {sizeKb}KB. OCR 실패: {ocrResult.Error}";
            }
        }

        return $"이미지 처리 완료: {extension.TrimStart('.').ToUpperInvariant()} / {sizeKb}KB. 현재 OCR 미구성으로 텍스트 추출은 생략되었습니다.";
    }

    private static string ExtractTextSample(string raw)
    {
        var cleaned = raw
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Regex.Replace(line, @"\s+", " "))
            .Where(line => line.Length >= 12 && line.Length <= 180)
            .Where(line => line.Any(char.IsLetter))
            .Where(line => line.Count(char.IsControl) == 0)
            .Where(line => !line.StartsWith("%") && !line.StartsWith("<<") && !line.StartsWith("/"))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        if (cleaned.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" | ", cleaned);
    }

    private static bool IsImageExtension(string extension)
    {
        return extension is ".png" or ".jpg" or ".jpeg" or ".webp";
    }

    private async Task<(bool Configured, string? Text, string? Error)> TryExtractImageTextWithOcrAsync(
        DocumentAsset document,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(_ocrOptions.Provider, "AzureVision", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_ocrOptions.Endpoint) ||
            string.IsNullOrWhiteSpace(_ocrOptions.ApiKey))
        {
            return (false, null, null);
        }

        var endpoint = _ocrOptions.Endpoint.TrimEnd('/');
        var startUrl = $"{endpoint}/vision/v3.2/read/analyze";
        var client = _httpClientFactory.CreateClient();

        await using var stream = File.OpenRead(document.StoredPath);
        using var startRequest = new HttpRequestMessage(HttpMethod.Post, startUrl);
        startRequest.Headers.Add("Ocp-Apim-Subscription-Key", _ocrOptions.ApiKey);
        startRequest.Content = new StreamContent(stream);
        startRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(GetImageContentType(document.OriginalFileName));

        using var startResponse = await client.SendAsync(startRequest, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
        {
            var errorBody = await startResponse.Content.ReadAsStringAsync(cancellationToken);
            return (true, null, $"시작 요청 실패: {(int)startResponse.StatusCode} {errorBody}");
        }

        if (!startResponse.Headers.TryGetValues("Operation-Location", out var operationLocations))
        {
            return (true, null, "OCR Operation-Location 헤더가 없습니다.");
        }

        var operationUrl = operationLocations.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(operationUrl))
        {
            return (true, null, "OCR Operation URL을 확인할 수 없습니다.");
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            statusRequest.Headers.Add("Ocp-Apim-Subscription-Key", _ocrOptions.ApiKey);
            using var statusResponse = await client.SendAsync(statusRequest, cancellationToken);
            var body = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!statusResponse.IsSuccessStatusCode)
            {
                return (true, null, $"상태 조회 실패: {(int)statusResponse.StatusCode} {body}");
            }

            using var statusDoc = JsonDocument.Parse(body);
            var root = statusDoc.RootElement;
            if (!root.TryGetProperty("status", out var statusElement))
            {
                return (true, null, "OCR 상태 필드를 찾을 수 없습니다.");
            }

            var status = statusElement.GetString();
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                var text = ExtractOcrText(root);
                return string.IsNullOrWhiteSpace(text)
                    ? (true, null, "OCR 텍스트가 비어 있습니다.")
                    : (true, text, null);
            }

            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return (true, null, "OCR 분석이 실패했습니다.");
            }
        }

        return (true, null, "OCR 응답 시간이 초과되었습니다.");
    }

    private static string? ExtractOcrText(JsonElement root)
    {
        if (!root.TryGetProperty("analyzeResult", out var analyzeResult) ||
            !analyzeResult.TryGetProperty("readResults", out var readResults) ||
            readResults.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var lines = new List<string>();
        foreach (var page in readResults.EnumerateArray())
        {
            if (!page.TryGetProperty("lines", out var pageLines) || pageLines.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var line in pageLines.EnumerateArray())
            {
                if (line.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(text.Trim());
                    }
                }
            }
        }

        if (lines.Count == 0)
        {
            return null;
        }

        return string.Join(" | ", lines.Take(3));
    }

    private static string GetImageContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
