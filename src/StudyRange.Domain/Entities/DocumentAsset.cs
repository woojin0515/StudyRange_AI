namespace StudyRange.Domain.Entities;

public sealed class DocumentAsset
{
    public Guid Id { get; }
    public Guid WorkspaceId { get; }
    public DocumentType DocumentType { get; }
    public string OriginalFileName { get; }
    public string StoredPath { get; }
    public long SizeInBytes { get; }
    public DateTimeOffset UploadedAtUtc { get; }
    public ProcessingStatus ProcessingStatus { get; private set; }
    public string? ProcessingSummary { get; private set; }

    public DocumentAsset(
        Guid id,
        Guid workspaceId,
        DocumentType documentType,
        string originalFileName,
        string storedPath,
        long sizeInBytes,
        DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Original file name is required.", nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(storedPath))
        {
            throw new ArgumentException("Stored path is required.", nameof(storedPath));
        }

        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Document size must be greater than 0.");
        }

        Id = id;
        WorkspaceId = workspaceId;
        DocumentType = documentType;
        OriginalFileName = originalFileName.Trim();
        StoredPath = storedPath;
        SizeInBytes = sizeInBytes;
        UploadedAtUtc = uploadedAtUtc;
        ProcessingStatus = ProcessingStatus.Queued;
    }

    public void UpdateProcessing(ProcessingStatus status, string? summary = null)
    {
        ProcessingStatus = status;
        ProcessingSummary = summary;
    }

    public static DocumentAsset Rehydrate(
        Guid id,
        Guid workspaceId,
        DocumentType documentType,
        string originalFileName,
        string storedPath,
        long sizeInBytes,
        DateTimeOffset uploadedAtUtc,
        ProcessingStatus processingStatus,
        string? processingSummary)
    {
        var document = new DocumentAsset(
            id,
            workspaceId,
            documentType,
            originalFileName,
            storedPath,
            sizeInBytes,
            uploadedAtUtc);

        document.UpdateProcessing(processingStatus, processingSummary);
        return document;
    }
}
