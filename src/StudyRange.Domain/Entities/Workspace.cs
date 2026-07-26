using StudyRange.Domain.ValueObjects;

namespace StudyRange.Domain.Entities;

public sealed class Workspace
{
    private readonly List<ExamRange> _examRanges = [];
    private readonly List<DocumentAsset> _documents = [];

    public Guid Id { get; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<ExamRange> ExamRanges => _examRanges;
    public IReadOnlyList<DocumentAsset> Documents => _documents;

    public Workspace(Guid id, string name, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Workspace name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public ExamRange AddExamRange(string subject, int startPage, int endPage, DateTimeOffset createdAtUtc)
    {
        var pageRange = new PageRange(startPage, endPage);
        var examRange = new ExamRange(Guid.NewGuid(), subject, pageRange, createdAtUtc);
        _examRanges.Add(examRange);
        return examRange;
    }

    public DocumentAsset AddDocument(
        DocumentType documentType,
        string originalFileName,
        string storedPath,
        long sizeInBytes,
        DateTimeOffset uploadedAtUtc)
    {
        var document = new DocumentAsset(
            id: Guid.NewGuid(),
            workspaceId: Id,
            documentType: documentType,
            originalFileName: originalFileName,
            storedPath: storedPath,
            sizeInBytes: sizeInBytes,
            uploadedAtUtc: uploadedAtUtc);

        _documents.Add(document);
        return document;
    }

    public DocumentAsset GetDocument(Guid documentId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);
        return document ?? throw new InvalidOperationException("Document not found in workspace.");
    }
}
