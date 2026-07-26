namespace StudyRange.Domain.Entities;

public sealed class ProcessingJob
{
    public Guid Id { get; }
    public Guid WorkspaceId { get; }
    public Guid DocumentId { get; }
    public ProcessingStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public ProcessingJob(Guid id, Guid workspaceId, Guid documentId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        WorkspaceId = workspaceId;
        DocumentId = documentId;
        Status = ProcessingStatus.Queued;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkProcessing(DateTimeOffset startedAtUtc)
    {
        Status = ProcessingStatus.Processing;
        StartedAtUtc = startedAtUtc;
        ErrorMessage = null;
    }

    public void MarkCompleted(DateTimeOffset completedAtUtc)
    {
        Status = ProcessingStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset completedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message is required.", nameof(errorMessage));
        }

        Status = ProcessingStatus.Failed;
        CompletedAtUtc = completedAtUtc;
        ErrorMessage = errorMessage.Trim();
    }

    public static ProcessingJob Rehydrate(
        Guid id,
        Guid workspaceId,
        Guid documentId,
        ProcessingStatus status,
        string? errorMessage,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc)
    {
        var job = new ProcessingJob(id, workspaceId, documentId, createdAtUtc);
        job.Status = status;
        job.ErrorMessage = errorMessage;
        job.StartedAtUtc = startedAtUtc;
        job.CompletedAtUtc = completedAtUtc;
        return job;
    }
}
