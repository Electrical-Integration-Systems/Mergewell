using SharpCompress.Archives;
using SharpCompress.Common;
using Mergewell.Core.Models;

namespace Mergewell.Core.Services;

public sealed class ImportService
{
    public async Task<string> ImportAsync(string sourcePath, JobPaths paths, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(fullSourcePath) && !File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("The dropped or uploaded input no longer exists.", fullSourcePath);
        }

        if (Directory.Exists(fullSourcePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return fullSourcePath;
        }

        var extension = Path.GetExtension(fullSourcePath);
        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Input must be a folder, .zip archive, or .rar archive.");
        }

        await Task.Run(() => ExtractArchive(fullSourcePath, paths.ImportedRoot, cancellationToken), cancellationToken);
        return paths.ImportedRoot;
    }

    private static void ExtractArchive(string archivePath, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        archive.WriteToDirectory(destinationPath, new ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true
        });
        cancellationToken.ThrowIfCancellationRequested();
    }

}