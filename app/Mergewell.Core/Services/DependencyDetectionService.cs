using Mergewell.Core.Models;

namespace Mergewell.Core.Services;

public sealed class DependencyDetectionService(AppStorageService storage)
{
    public DependencyStatus Detect()
    {
        var storageWritable = false;
        try
        {
            storage.EnsureCreated();
            var probe = Path.Combine(storage.StorageRoot, $".write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            storageWritable = true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return new DependencyStatus(WordConversionService.IsWordAvailable(), storageWritable);
    }
}