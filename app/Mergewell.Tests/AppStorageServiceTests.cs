using Mergewell.Core.Models;
using Mergewell.Core.Services;

namespace Mergewell.Tests;

public sealed class AppStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"MergewellStorageTests-{Guid.NewGuid():N}");

    [Fact]
    public void CreateJobPaths_CreatesManagedJobTree()
    {
        var paths = new AppStorageService(_root).CreateJobPaths("Client Documents.zip", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));

        Assert.True(Directory.Exists(paths.OriginalRoot));
        Assert.True(Directory.Exists(paths.ImportedRoot));
        Assert.True(Directory.Exists(paths.PdfTreeRoot));
        Assert.True(Directory.Exists(paths.OutputRoot));
        Assert.EndsWith("Client Documents-merged.pdf", paths.MergedPdf);
    }

    [Fact]
    public async Task DeleteJobAsync_RemovesManagedJob()
    {
        var storage = new AppStorageService(_root);
        var paths = storage.CreateJobPaths("Client Documents");
        var store = new AppDataStore(storage);
        var job = new MergeJob { Id = "job-1", JobRoot = paths.JobRoot, OriginalInputName = "Client Documents" };
        await store.SaveJobAsync(job);

        await store.DeleteJobAsync(job);

        Assert.False(Directory.Exists(paths.JobRoot));
        Assert.Empty(await store.LoadJobsAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}