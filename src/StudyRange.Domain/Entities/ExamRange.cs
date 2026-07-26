using StudyRange.Domain.ValueObjects;

namespace StudyRange.Domain.Entities;

public sealed class ExamRange
{
    public Guid Id { get; }
    public string Subject { get; private set; }
    public PageRange Range { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }

    public ExamRange(Guid id, string subject, PageRange range, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required.", nameof(subject));
        }

        Id = id;
        Subject = subject.Trim();
        Range = range;
        CreatedAtUtc = createdAtUtc;
    }
}
