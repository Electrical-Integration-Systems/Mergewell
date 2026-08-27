using System.Text.Json;
using Mergewell.Core.Models;

namespace Mergewell.Core.Services;

public sealed class AppDataStore(AppStorageService storage)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task SaveJobAsync(MergeJob job, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(job.JobRoot, "job.json");
        Directory.CreateDirectory(job.JobRoot);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var temporaryPath = path + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(job, JsonOptions), cancellationToken);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AppendHistoryAsync(MergeJob job, CancellationToken cancellationToken = default)
    {
        storage.EnsureCreated();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var summary = new
            {
                job.Id,
                job.CreatedAt,
                job.FinishedAt,
                job.OriginalInputName,
                job.Status,
                job.TotalItems,
                job.CompletedItems,
                job.OutputPageCount,
                job.MergedPdf,
                job.Error
            };
            await File.AppendAllTextAsync(storage.HistoryPath, JsonSerializer.Serialize(summary) + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<MergeJob>> LoadJobsAsync(CancellationToken cancellationToken = default)
    {
        storage.EnsureCreated();
        var jobs = new List<MergeJob>();
        foreach (var path in Directory.EnumerateFiles(storage.JobsRoot, "job.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                var job = JsonSerializer.Deserialize<MergeJob>(json, JsonOptions);
                if (job is not null)
                {
                    if (job.OutputPageCount == 0 && job.Status == MergeJobStatus.Completed && File.Exists(job.MergedPdf))
                    {
                        job.OutputPageCount = PdfMergeService.GetPageCount(job.MergedPdf);
                        await SaveJobAsync(job, cancellationToken);
                    }
                    jobs.Add(job);
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return jobs.OrderByDescending(job => job.CreatedAt).ToArray();
    }

    public async Task DeleteJobAsync(MergeJob job, CancellationToken cancellationToken = default)
    {
        storage.EnsureCreated();
        var jobsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(storage.JobsRoot)) + Path.DirectorySeparatorChar;
        var jobRoot = Path.GetFullPath(job.JobRoot);
        if (!jobRoot.StartsWith(jobsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The merge is outside managed storage.");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(jobRoot))
            {
                await Task.Run(() => Directory.Delete(jobRoot, true), cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}