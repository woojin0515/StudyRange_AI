using StudyRange.Application.Contracts;

namespace StudyRange.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootDirectory;

    public LocalFileStorage(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("File name is required.", nameof(originalFileName));
        }

        Directory.CreateDirectory(_rootDirectory);

        var safeName = Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(safeName);
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_rootDirectory, uniqueName);

        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(output, cancellationToken);
        return path;
    }
}
