using StudyRange.Application.UseCases;

namespace StudyRange.Web.App;

public sealed class UserPreferencesState
{
    public string PreferredOutputStyle { get; set; } = "간단";
    public string QuizDifficulty { get; set; } = "중";
    public int QuizQuestionCount { get; set; } = 8;
    public bool EnableProcessingNotifications { get; set; } = true;
    public bool AutoOpenLatestResult { get; set; } = true;
    public SchoolLevel DefaultSchoolLevel { get; set; } = SchoolLevel.Middle;
    public int DefaultGrade { get; set; } = 1;
    public string DefaultSubject { get; set; } = string.Empty;
    public string DefaultCurriculumRevision { get; set; } = string.Empty;
    public string DefaultPublisher { get; set; } = string.Empty;
}
