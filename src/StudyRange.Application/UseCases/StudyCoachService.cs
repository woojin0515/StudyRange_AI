using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;

namespace StudyRange.Application.UseCases;

public sealed class StudyCoachService : IStudyCoachService
{
    private const long MaxUploadSizeBytes = 25L * 1024 * 1024;
    private static readonly HashSet<string> AllowedWorksheetAndNoteExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp"
    };
    private static readonly HashSet<string> AllowedWorksheetAndNoteContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg", "image/webp"
    };

    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProcessingJobRepository _processingJobRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IProcessingQueue _processingQueue;

    public StudyCoachService(
        IWorkspaceRepository workspaceRepository,
        IProcessingJobRepository processingJobRepository,
        IFileStorage fileStorage,
        IProcessingQueue processingQueue)
    {
        _workspaceRepository = workspaceRepository;
        _processingJobRepository = processingJobRepository;
        _fileStorage = fileStorage;
        _processingQueue = processingQueue;
    }

    public async Task<IReadOnlyList<WorkspaceModel>> GetWorkspacesAsync(CancellationToken cancellationToken)
    {
        var workspaces = await _workspaceRepository.ListAsync(cancellationToken);
        return workspaces
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(MapWorkspace)
            .ToList();
    }

    public async Task<WorkspaceModel> CreateWorkspaceAsync(string name, CancellationToken cancellationToken)
    {
        var workspace = new Workspace(Guid.NewGuid(), name, DateTimeOffset.UtcNow);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        return MapWorkspace(workspace);
    }

    public async Task<IReadOnlyList<ExamRangeModel>> GetExamRangesAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        return workspace.ExamRanges
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(MapExamRange)
            .ToList();
    }

    public async Task<ExamRangeModel> AddExamRangeAsync(
        Guid workspaceId,
        string subject,
        int startPage,
        int endPage,
        CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        var examRange = workspace.AddExamRange(subject, startPage, endPage, DateTimeOffset.UtcNow);
        return MapExamRange(examRange);
    }

    public async Task<IReadOnlyList<DocumentModel>> GetDocumentsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        return workspace.Documents
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(MapDocument)
            .ToList();
    }

    public async Task<DocumentModel> UploadDocumentAsync(
        Guid workspaceId,
        DocumentType documentType,
        string originalFileName,
        string contentType,
        long fileSize,
        Stream content,
        CancellationToken cancellationToken)
    {
        ValidateUpload(documentType, originalFileName, contentType, fileSize);

        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        var storedPath = await _fileStorage.SaveAsync(content, originalFileName, cancellationToken);

        var document = workspace.AddDocument(
            documentType: documentType,
            originalFileName: originalFileName,
            storedPath: storedPath,
            sizeInBytes: fileSize,
            uploadedAtUtc: DateTimeOffset.UtcNow);

        var job = new ProcessingJob(Guid.NewGuid(), workspaceId, document.Id, DateTimeOffset.UtcNow);
        await _processingJobRepository.AddAsync(job, cancellationToken);
        await _processingQueue.EnqueueAsync(job.Id, cancellationToken);

        return MapDocument(document);
    }

    public async Task<IReadOnlyList<ProcessingJobModel>> GetProcessingJobsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var jobs = await _processingJobRepository.ListByWorkspaceAsync(workspaceId, cancellationToken);
        return jobs
            .OrderByDescending(j => j.CreatedAtUtc)
            .Select(j => new ProcessingJobModel(
                j.Id,
                j.DocumentId,
                j.Status,
                j.ErrorMessage,
                j.CreatedAtUtc,
                j.StartedAtUtc,
                j.CompletedAtUtc))
            .ToList();
    }

    private async Task<Workspace> GetWorkspaceOrThrowAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        return workspace ?? throw new InvalidOperationException("Workspace not found.");
    }

    private static WorkspaceModel MapWorkspace(Workspace workspace)
    {
        return new WorkspaceModel(workspace.Id, workspace.Name, workspace.CreatedAtUtc);
    }

    private static ExamRangeModel MapExamRange(ExamRange range)
    {
        return new ExamRangeModel(range.Id, range.Subject, range.Range.StartPage, range.Range.EndPage, range.CreatedAtUtc);
    }

    private static DocumentModel MapDocument(DocumentAsset document)
    {
        return new DocumentModel(
            document.Id,
            document.DocumentType,
            document.OriginalFileName,
            document.StoredPath,
            document.SizeInBytes,
            document.ProcessingStatus,
            document.ProcessingSummary,
            document.UploadedAtUtc);
    }

    private static void ValidateUpload(
        DocumentType documentType,
        string originalFileName,
        string contentType,
        long fileSize)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new InvalidOperationException("파일명이 비어 있습니다.");
        }

        if (fileSize <= 0)
        {
            throw new InvalidOperationException("빈 파일은 업로드할 수 없습니다.");
        }

        if (fileSize > MaxUploadSizeBytes)
        {
            throw new InvalidOperationException("파일 크기는 25MB 이하여야 합니다.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new InvalidOperationException("확장자가 없는 파일은 업로드할 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvalidOperationException("파일 Content-Type을 확인할 수 없습니다.");
        }

        switch (documentType)
        {
            case DocumentType.TextbookPdf:
                if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("교과서 파일은 PDF만 업로드할 수 있습니다.");
                }

                if (!contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("교과서 파일 형식이 올바르지 않습니다. PDF만 허용됩니다.");
                }
                break;
            case DocumentType.Worksheet:
            case DocumentType.HandwrittenNotes:
                if (!AllowedWorksheetAndNoteExtensions.Contains(extension))
                {
                    throw new InvalidOperationException("학습지/노트는 PDF, PNG, JPG, JPEG, WEBP만 업로드할 수 있습니다.");
                }

                if (!AllowedWorksheetAndNoteContentTypes.Contains(contentType))
                {
                    throw new InvalidOperationException("학습지/노트 파일 형식이 올바르지 않습니다.");
                }
                break;
            default:
                throw new InvalidOperationException("지원하지 않는 문서 유형입니다.");
        }
    }
}
