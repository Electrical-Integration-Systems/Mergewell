namespace Mergewell.Core.Services;

public sealed class TraversalService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".rtf", ".pdf"
    };

    public IReadOnlyList<string> GetInputFiles(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Input folder does not exist: {rootPath}");
        }

        var files = new List<string>();
        Visit(rootPath, files);
        return files;
    }

    private static void Visit(string directoryPath, List<string> files)
    {
        foreach (var directory in Directory.EnumerateDirectories(directoryPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            Visit(directory, files);
        }

        foreach (var file in Directory.EnumerateFiles(directoryPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file);
            if (!fileName.StartsWith("~$", StringComparison.Ordinal) && SupportedExtensions.Contains(Path.GetExtension(file)))
            {
                files.Add(file);
            }
        }
    }
}