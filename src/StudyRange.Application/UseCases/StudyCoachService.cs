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
    private readonly IStudyContentGenerator _studyContentGenerator;
    private readonly IEducationMetadataService _educationMetadataService;
    private readonly IReadOnlyList<ITextbookPdfProvider> _textbookPdfProviders;

    public StudyCoachService(
        IWorkspaceRepository workspaceRepository,
        IProcessingJobRepository processingJobRepository,
        IFileStorage fileStorage,
        IProcessingQueue processingQueue,
        IStudyContentGenerator studyContentGenerator,
        IEducationMetadataService educationMetadataService,
        IEnumerable<ITextbookPdfProvider> textbookPdfProviders)
    {
        _workspaceRepository = workspaceRepository;
        _processingJobRepository = processingJobRepository;
        _fileStorage = fileStorage;
        _processingQueue = processingQueue;
        _studyContentGenerator = studyContentGenerator;
        _educationMetadataService = educationMetadataService;
        _textbookPdfProviders = textbookPdfProviders.ToList();
    }

    public async Task<IReadOnlyList<WorkspaceModel>> GetWorkspacesAsync(CancellationToken cancellationToken)
    {
        var workspaces = await _workspaceRepository.ListAsync(cancellationToken);
        return workspaces
            .OrderByDescending(w => w.CreatedAtUtc)
            .Select(MapWorkspace)
            .ToList();
    }

    public async Task<DashboardSummaryModel> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _workspaceRepository.GetDashboardSnapshotAsync(recentCount: 5, cancellationToken);
        var recent = snapshot.RecentWorkspaces
            .Select(MapWorkspace)
            .ToList();

        return new DashboardSummaryModel(
            WorkspaceCount: snapshot.WorkspaceCount,
            ExamRangeCount: snapshot.ExamRangeCount,
            DocumentCount: snapshot.DocumentCount,
            GeneratedCount: snapshot.GeneratedCount,
            RecentWorkspaces: recent);
    }

    public async Task<WorkspaceDataBundleModel> GetWorkspaceDataAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        var jobs = await _processingJobRepository.ListByWorkspaceAsync(workspaceId, cancellationToken);

        var examRanges = workspace.ExamRanges
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(MapExamRange)
            .ToList();

        var documents = workspace.Documents
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(MapDocument)
            .ToList();

        var generatedContents = workspace.GeneratedContents
            .OrderByDescending(x => x.GeneratedAtUtc)
            .Select(x => new GeneratedContentHistoryModel(
                Id: x.Id,
                ExamRangeId: x.ExamRangeId,
                Subject: x.Subject,
                StartPage: x.StartPage,
                EndPage: x.EndPage,
                ContentType: ParseContentType(x.ContentType),
                Content: x.Content,
                Provider: x.Provider,
                Model: x.Model,
                GeneratedAtUtc: x.GeneratedAtUtc))
            .ToList();

        var processingJobs = jobs
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

        return new WorkspaceDataBundleModel(
            ExamRanges: examRanges,
            Documents: documents,
            ProcessingJobs: processingJobs,
            GeneratedContents: generatedContents);
    }

    public async Task<WorkspaceModel> CreateWorkspaceAsync(string name, CancellationToken cancellationToken)
    {
        var workspace = new Workspace(Guid.NewGuid(), name, DateTimeOffset.UtcNow);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        return MapWorkspace(workspace);
    }

    public async Task DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        foreach (var path in workspace.Documents.Select(x => x.StoredPath).Distinct(StringComparer.Ordinal))
        {
            _ = await _fileStorage.DeleteAsync(path, cancellationToken);
        }

        await _workspaceRepository.DeleteAsync(workspaceId, cancellationToken);
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
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
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
        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        ValidateUpload(documentType, originalFileName, contentType, fileSize, buffered);
        buffered.Position = 0;

        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        var storedPath = await _fileStorage.SaveAsync(buffered, originalFileName, cancellationToken);

        var document = workspace.AddDocument(
            documentType: documentType,
            originalFileName: originalFileName,
            storedPath: storedPath,
            sizeInBytes: fileSize,
            uploadedAtUtc: DateTimeOffset.UtcNow);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

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

    public async Task RetryProcessingJobAsync(Guid workspaceId, Guid jobId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        var targetJob = await _processingJobRepository.GetByIdAsync(jobId, cancellationToken);
        if (targetJob is null || targetJob.WorkspaceId != workspaceId)
        {
            throw new InvalidOperationException("재처리 대상 작업을 찾을 수 없습니다.");
        }

        if (targetJob.Status != ProcessingStatus.Failed)
        {
            throw new InvalidOperationException("실패 상태의 작업만 재처리할 수 있습니다.");
        }

        var document = workspace.GetDocument(targetJob.DocumentId);
        document.UpdateProcessing(ProcessingStatus.Queued, "재처리 대기 중");
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

        var retryJob = new ProcessingJob(Guid.NewGuid(), workspaceId, targetJob.DocumentId, DateTimeOffset.UtcNow);
        await _processingJobRepository.AddAsync(retryJob, cancellationToken);
        await _processingQueue.EnqueueAsync(retryJob.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedContentHistoryModel>> GetGeneratedContentsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        return workspace.GeneratedContents
            .OrderByDescending(x => x.GeneratedAtUtc)
            .Select(x => new GeneratedContentHistoryModel(
                Id: x.Id,
                ExamRangeId: x.ExamRangeId,
                Subject: x.Subject,
                StartPage: x.StartPage,
                EndPage: x.EndPage,
                ContentType: ParseContentType(x.ContentType),
                Content: x.Content,
                Provider: x.Provider,
                Model: x.Model,
                GeneratedAtUtc: x.GeneratedAtUtc))
            .ToList();
    }

    public async Task DeleteGeneratedContentAsync(Guid workspaceId, Guid generatedContentId, CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        workspace.RemoveGeneratedContent(generatedContentId);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
    }

    public async Task<GeneratedStudyContentModel> GenerateContentAsync(
        Guid workspaceId,
        Guid examRangeId,
        GeneratedContentType contentType,
        GeneratedContentContextModel context,
        CancellationToken cancellationToken)
    {
        var workspace = await GetWorkspaceOrThrowAsync(workspaceId, cancellationToken);
        var examRange = workspace.ExamRanges.FirstOrDefault(x => x.Id == examRangeId);
        if (examRange is null)
        {
            throw new InvalidOperationException("선택한 시험 범위를 찾을 수 없습니다.");
        }

        await EnsureTextbookPdfReadyAsync(workspace, context, cancellationToken);

        ValidateGenerationContext(context);
        var generated = await _studyContentGenerator.GenerateAsync(workspace, examRange, contentType, context, cancellationToken);
        workspace.AddGeneratedContent(
            examRangeId: examRange.Id,
            subject: examRange.Subject,
            startPage: examRange.Range.StartPage,
            endPage: examRange.Range.EndPage,
            contentType: contentType.ToString(),
            content: generated.Content,
            provider: generated.Provider,
            model: generated.Model,
            generatedAtUtc: generated.GeneratedAtUtc);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

        return generated;
    }

    public Task<EducationMetadataBundleModel> CollectEducationMetadataAsync(
        EducationMetadataQueryModel query,
        CancellationToken cancellationToken)
    {
        ValidateEducationMetadataQuery(query);
        return _educationMetadataService.CollectAsync(query, cancellationToken);
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
        long fileSize,
        Stream content)
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

        if (content.Length == 0)
        {
            throw new InvalidOperationException("빈 파일은 업로드할 수 없습니다.");
        }

        if (content.Length > MaxUploadSizeBytes)
        {
            throw new InvalidOperationException("파일 크기는 25MB 이하여야 합니다.");
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

                if (!IsPdf(content))
                {
                    throw new InvalidOperationException("교과서 파일 시그니처가 올바르지 않습니다.");
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

                if (!HasAllowedBinarySignature(content))
                {
                    throw new InvalidOperationException("학습지/노트 파일 시그니처가 올바르지 않습니다.");
                }
                break;
            default:
                throw new InvalidOperationException("지원하지 않는 문서 유형입니다.");
        }
    }

    private static void ValidateEducationMetadataQuery(EducationMetadataQueryModel query)
    {
        if (string.IsNullOrWhiteSpace(query.Subject))
        {
            throw new InvalidOperationException("과목은 비어 있을 수 없습니다.");
        }

        if (query.Grade < 1 || query.Grade > GetMaxGradeBySchoolLevel(query.SchoolLevel))
        {
            throw new InvalidOperationException("학교급에 맞는 학년을 입력하세요.");
        }
    }

    private static int GetMaxGradeBySchoolLevel(SchoolLevel schoolLevel)
    {
        return schoolLevel switch
        {
            SchoolLevel.Elementary => 6,
            SchoolLevel.Middle or SchoolLevel.High => 3,
            _ => 3
        };
    }

    private static void ValidateGenerationContext(GeneratedContentContextModel context)
    {
        if (string.IsNullOrWhiteSpace(context.Subject))
        {
            throw new InvalidOperationException("생성 컨텍스트 과목이 비어 있습니다.");
        }

        if (context.Grade < 1 || context.Grade > GetMaxGradeBySchoolLevel(context.SchoolLevel))
        {
            throw new InvalidOperationException("학교급에 맞는 학년을 설정한 뒤 다시 시도하세요.");
        }

        if (string.IsNullOrWhiteSpace(context.TextbookPublisher))
        {
            throw new InvalidOperationException("출판사를 확정한 뒤 다시 시도하세요.");
        }
    }

    private async Task EnsureTextbookPdfReadyAsync(
        Workspace workspace,
        GeneratedContentContextModel context,
        CancellationToken cancellationToken)
    {
        var hasExistingTextbookPdf = workspace.Documents.Any(x =>
            x.DocumentType == DocumentType.TextbookPdf &&
            File.Exists(x.StoredPath));
        if (hasExistingTextbookPdf)
        {
            return;
        }

        if (_textbookPdfProviders.Count == 0)
        {
            throw new InvalidOperationException("교과서 PDF 자동 확보 공급자가 설정되지 않았습니다.");
        }

        var failures = new List<string>();
        foreach (var provider in _textbookPdfProviders)
        {
            var fetchResult = await provider.FetchTextbookPdfAsync(context, cancellationToken);
            if (!fetchResult.Success || fetchResult.Items.Count == 0)
            {
                failures.Add($"{fetchResult.Source}: {fetchResult.Message}");
                continue;
            }

            var asset = fetchResult.Items[0];
            if (!LooksLikePdf(asset.Content))
            {
                failures.Add($"{fetchResult.Source}: PDF 시그니처가 올바르지 않습니다.");
                continue;
            }

            await using var content = new MemoryStream(asset.Content);
            var storedPath = await _fileStorage.SaveAsync(content, asset.FileName, cancellationToken);
            var document = workspace.AddDocument(
                documentType: DocumentType.TextbookPdf,
                originalFileName: asset.FileName,
                storedPath: storedPath,
                sizeInBytes: asset.Content.LongLength,
                uploadedAtUtc: DateTimeOffset.UtcNow);
            document.UpdateProcessing(
                ProcessingStatus.Completed,
                $"자동 확보 완료({fetchResult.Source})");
            await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
            return;
        }

        var reason = failures.Count == 0
            ? "자동 확보 가능한 교과서 PDF를 찾지 못했습니다."
            : string.Join(" | ", failures);
        throw new InvalidOperationException(
            $"교과서 PDF 자동 확보 실패: {reason}. `App_Data/textbook-catalog/(개정년도)_(과목)_(출판사).pdf` 저장 또는 TextbookPdf API 설정이 필요합니다.");
    }

    private static bool LooksLikePdf(byte[] content)
    {
        return content.Length >= 4 &&
               content[0] == 0x25 &&
               content[1] == 0x50 &&
               content[2] == 0x44 &&
               content[3] == 0x46;
    }

    private static bool HasAllowedBinarySignature(Stream content)
    {
        return IsPdf(content) || IsPng(content) || IsJpeg(content) || IsWebp(content);
    }

    private static bool IsPdf(Stream content)
    {
        return HasPrefix(content, [0x25, 0x50, 0x44, 0x46]); // %PDF
    }

    private static bool IsPng(Stream content)
    {
        return HasPrefix(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    private static bool IsJpeg(Stream content)
    {
        return HasPrefix(content, [0xFF, 0xD8, 0xFF]);
    }

    private static bool IsWebp(Stream content)
    {
        const int requiredLength = 12;
        if (content.Length < requiredLength)
        {
            return false;
        }

        var buffer = new byte[requiredLength];
        var originalPosition = content.Position;
        try
        {
            content.Position = 0;
            _ = content.Read(buffer, 0, buffer.Length);
            var riff = buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46; // RIFF
            var webp = buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50; // WEBP
            return riff && webp;
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private static bool HasPrefix(Stream content, ReadOnlySpan<byte> prefix)
    {
        if (content.Length < prefix.Length)
        {
            return false;
        }

        var buffer = new byte[prefix.Length];
        var originalPosition = content.Position;
        try
        {
            content.Position = 0;
            _ = content.Read(buffer, 0, buffer.Length);
            return buffer.AsSpan().SequenceEqual(prefix);
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private static GeneratedContentType ParseContentType(string contentType)
    {
        if (Enum.TryParse<GeneratedContentType>(contentType, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return GeneratedContentType.Summary;
    }
}
