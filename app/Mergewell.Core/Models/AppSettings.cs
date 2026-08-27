namespace Mergewell.Core.Models;

public sealed class AppSettings
{
    public string StorageRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Mergewell");
}

public sealed record DependencyStatus(bool WordAvailable, bool StorageWritable)
{
    public string WordText => WordAvailable ? "Word ready" : "Word not installed";
    public string StorageText => StorageWritable ? "Storage ready" : "Storage unavailable";
}