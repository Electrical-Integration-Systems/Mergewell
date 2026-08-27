using Mergewell.Core.Models;

namespace Mergewell.Core.Services;

public sealed class AppStorageService
{
    public AppStorageService(string? storageRoot = null)
    {
        StorageRoot = string.IsNullOrWhiteSpace(storageRoot)
            ? GetDefaultStorageRoot()
            : Path.GetFullPath(storageRoot);
    }

    public string StorageRoot { get; }
    public string JobsRoot => Path.Combine(StorageRoot, "jobs");
    public string SettingsPath => Path.Combine(StorageRoot, "settings.json");
    public string HistoryPath => Path.Combine(StorageRoot, "history.jsonl");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(StorageRoot);
        Directory.CreateDirectory(JobsRoot);
    }

    public JobPaths CreateJobPaths(string inputName, DateTimeOffset? now = null)
    {
        EnsureCreated();
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToString("yyyyMMdd-HHmmss");
        var safeName = SanitizeName(Path.GetFileNameWithoutExtension(inputName));
        var id = $"{timestamp}-{Guid.NewGuid():N}"[..26];
        var jobRoot = Path.Combine(JobsRoot, $"{safeName}-{id}");
        var outputRoot = Path.Combine(jobRoot, "output");
        var paths = new JobPaths(
            jobRoot,
            Path.Combine(jobRoot, "original"),
            Path.Combine(jobRoot, "imported"),
            Path.Combine(jobRoot, "pdf-tree"),
            outputRoot,
            Path.Combine(outputRoot, $"{safeName}-merged.pdf"),
            Path.Combine(jobRoot, "logs", "job.log"));

        Directory.CreateDirectory(paths.OriginalRoot);
        Directory.CreateDirectory(paths.ImportedRoot);
        Directory.CreateDirectory(paths.PdfTreeRoot);
        Directory.CreateDirectory(paths.OutputRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.LogPath)!);
        return paths;
    }

    private static string SanitizeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "merge" : cleaned;
    }

    private static string GetDefaultStorageRoot()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "Mergewell");
    }
}