using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;
using System.Text;
using System.Text.RegularExpressions;

namespace StudyRange.Infrastructure.Processing;

public sealed class DocumentProcessingWorker : BackgroundService
{
    private readonly IProcessingQueue _processingQueue;
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IProcessingQueue processingQueue,
        IProcessingJobRepository jobRepository,
        IWorkspaceRepository workspaceRepository,
        ILogger<DocumentProcessingWorker> logger)
    {
        _processingQueue = processingQueue;
        _jobRepository = jobRepository;
        _workspaceRepository = workspaceRepository;
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

    private static async Task<string> BuildProcessingSummaryAsync(DocumentAsset document, CancellationToken cancellationToken)
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
}
