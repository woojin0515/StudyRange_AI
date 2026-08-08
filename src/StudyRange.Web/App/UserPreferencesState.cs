namespace StudyRange.Web.App;

public sealed class UserPreferencesState
{
    public string PreferredOutputStyle { get; set; } = "간단";
    public string QuizDifficulty { get; set; } = "중";
    public int QuizQuestionCount { get; set; } = 8;
    public bool EnableProcessingNotifications { get; set; } = true;
    public bool AutoOpenLatestResult { get; set; } = true;
}
