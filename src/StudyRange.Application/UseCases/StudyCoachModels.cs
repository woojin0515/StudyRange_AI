using StudyRange.Domain.Entities;

namespace StudyRange.Application.UseCases;

public sealed record WorkspaceModel(Guid Id, string Name, DateTimeOffset CreatedAtUtc);

public sealed record ExamRangeModel(Guid Id, string Subject, int StartPage, int EndPage, DateTimeOffset CreatedAtUtc);

public sealed record DocumentModel(
    Guid Id,
    DocumentType DocumentType,
    string OriginalFileName,
    string StoredPath,
    long SizeInBytes,
    ProcessingStatus ProcessingStatus,
    string? ProcessingSummary,
    DateTimeOffset UploadedAtUtc);

public sealed record ProcessingJobModel(
    Guid Id,
    Guid DocumentId,
    ProcessingStatus Status,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);
