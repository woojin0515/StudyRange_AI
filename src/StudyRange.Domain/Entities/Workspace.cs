using StudyRange.Domain.ValueObjects;

namespace StudyRange.Domain.Entities;

public sealed class Workspace
{
    private readonly List<ExamRange> _examRanges = [];
    private readonly List<DocumentAsset> _documents = [];
    private readonly List<GeneratedContentArtifact> _generatedContents = [];

    public Guid Id { get; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<ExamRange> ExamRanges => _examRanges;
    public IReadOnlyList<DocumentAsset> Documents => _documents;
    public IReadOnlyList<GeneratedContentArtifact> GeneratedContents => _generatedContents;

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

    public void AttachExamRange(ExamRange examRange)
    {
        if (_examRanges.Any(r => r.Id == examRange.Id))
        {
            throw new InvalidOperationException("Exam range with same id already exists.");
        }

        _examRanges.Add(examRange);
    }

    public void AttachDocument(DocumentAsset document)
    {
        if (_documents.Any(d => d.Id == document.Id))
        {
            throw new InvalidOperationException("Document with same id already exists.");
        }

        _documents.Add(document);
    }

    public GeneratedContentArtifact AddGeneratedContent(
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
        var artifact = new GeneratedContentArtifact(
            id: Guid.NewGuid(),
            workspaceId: Id,
            examRangeId: examRangeId,
            subject: subject,
            startPage: startPage,
            endPage: endPage,
            contentType: contentType,
            content: content,
            provider: provider,
            model: model,
            generatedAtUtc: generatedAtUtc);

        _generatedContents.Add(artifact);
        return artifact;
    }

    public void AttachGeneratedContent(GeneratedContentArtifact artifact)
    {
        if (_generatedContents.Any(x => x.Id == artifact.Id))
        {
            throw new InvalidOperationException("Generated content with same id already exists.");
        }

        _generatedContents.Add(artifact);
    }

    public void RemoveGeneratedContent(Guid generatedContentId)
    {
        var removed = _generatedContents.RemoveAll(x => x.Id == generatedContentId);
        if (removed == 0)
        {
            throw new InvalidOperationException("Generated content not found in workspace.");
        }
    }
}
