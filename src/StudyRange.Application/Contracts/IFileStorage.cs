namespace StudyRange.Application.Contracts;

public interface IFileStorage
{
    Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(
        string storedPath,
        CancellationToken cancellationToken);
}
