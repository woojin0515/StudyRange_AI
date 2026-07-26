namespace StudyRange.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string RootDirectory { get; set; } = "App_Data/uploads";
}
