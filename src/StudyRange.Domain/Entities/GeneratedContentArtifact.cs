namespace StudyRange.Domain.Entities;

public sealed class GeneratedContentArtifact
{
    public Guid Id { get; }
    public Guid WorkspaceId { get; }
    public Guid ExamRangeId { get; }
    public string Subject { get; }
    public int StartPage { get; }
    public int EndPage { get; }
    public string ContentType { get; }
    public string Content { get; }
    public string Provider { get; }
    public string Model { get; }
    public DateTimeOffset GeneratedAtUtc { get; }

    public GeneratedContentArtifact(
        Guid id,
        Guid workspaceId,
        Guid examRangeId,
        string subject,
        int startPage,
        int endPage,
        string contentType,
        string content,
        string provider,
        string model,
        DateTimeOffset generatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider is required.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model is required.", nameof(model));
        }

        Id = id;
        WorkspaceId = workspaceId;
        ExamRangeId = examRangeId;
        Subject = subject.Trim();
        StartPage = startPage;
        EndPage = endPage;
        ContentType = contentType.Trim();
        Content = content;
        Provider = provider.Trim();
        Model = model.Trim();
        GeneratedAtUtc = generatedAtUtc;
    }
}
