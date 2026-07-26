using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;

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
                document.UpdateProcessing(
                    status: ProcessingStatus.Completed,
                    summary: $"MVP placeholder extraction complete for {document.OriginalFileName}");
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
}
