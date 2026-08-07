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

public enum GeneratedContentType
{
    Summary = 1,
    Quiz = 2
}

public sealed record GeneratedStudyContentModel(
    GeneratedContentType ContentType,
    string Content,
    string Provider,
    string Model,
    DateTimeOffset GeneratedAtUtc);

public sealed record GeneratedContentContextModel(
    string Subject,
    SchoolLevel SchoolLevel,
    int Grade,
    int? BirthYear,
    string? SchoolName,
    string? CurriculumRevision,
    string? TextbookPublisher);

public sealed record GeneratedContentHistoryModel(
    Guid Id,
    Guid ExamRangeId,
    string Subject,
    int StartPage,
    int EndPage,
    GeneratedContentType ContentType,
    string Content,
    string Provider,
    string Model,
    DateTimeOffset GeneratedAtUtc);

public enum SchoolLevel
{
    Elementary = 1,
    Middle = 2,
    High = 3
}

public sealed record EducationMetadataQueryModel(
    string Subject,
    SchoolLevel SchoolLevel,
    int Grade,
    int? BirthYear,
    string? SchoolName);

public sealed record SourceStatusModel(
    string Source,
    bool Success,
    string Message);

public sealed record CurriculumRevisionModel(
    string RevisionCode,
    string DisplayName,
    string Source,
    string? Description);

public sealed record TextbookCatalogModel(
    string Subject,
    string Publisher,
    string Title,
    string? Isbn,
    string CurriculumRevision,
    string Source);

public sealed record SchoolContextFactModel(
    string Key,
    string Value,
    string Source);

public sealed record EducationMetadataBundleModel(
    IReadOnlyList<CurriculumRevisionModel> Curriculums,
    IReadOnlyList<TextbookCatalogModel> Textbooks,
    IReadOnlyList<SchoolContextFactModel> SchoolFacts,
    IReadOnlyList<SourceStatusModel> SourceStatuses);
