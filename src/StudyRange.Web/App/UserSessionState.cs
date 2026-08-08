namespace StudyRange.Web.App;

public sealed class UserSessionState
{
    public bool IsAuthenticated { get; private set; }
    public string DisplayName { get; private set; } = "학생";

    public event Action? Changed;

    public void SignIn(string displayName)
    {
        IsAuthenticated = true;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "학생" : displayName.Trim();
        Changed?.Invoke();
    }

    public void SignOut()
    {
        IsAuthenticated = false;
        DisplayName = "학생";
        Changed?.Invoke();
    }

    public void UpdateDisplayName(string displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "학생" : displayName.Trim();
        Changed?.Invoke();
    }
}
