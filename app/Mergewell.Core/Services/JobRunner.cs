using System.Collections.Concurrent;
using Mergewell.Core.Models;

namespace Mergewell.Core.Services;

public sealed class JobRunner(
    AppStorageService storage,
    AppDataStore dataStore,
    ImportService importer,
    TraversalService traversal,
    WordConversionService wordConversion,
    PdfCopyService pdfCopy,
    PdfMergeService pdfMerge)
{
    private const int MaxProcessingWorkers = 2;
    private static readonly HashSet<string> WordExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".rtf"
    };

    public async Task<MergeJob> RunAsync(
        string sourcePath,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken,
        Action<MergeJob>? jobCreated = null)
    {
        var inputName = Directory.Exists(sourcePath)
            ? new DirectoryInfo(sourcePath).Name
            : Path.GetFileName(sourcePath);
        var paths = storage.CreateJobPaths(inputName);
        var job = new MergeJob
        {
            Id = Path.GetFileName(paths.JobRoot),
            CreatedAt = DateTimeOffset.UtcNow,
            OriginalInputName = inputName,
            OriginalInputKind = Directory.Exists(sourcePath) ? "Folder" : Path.GetExtension(sourcePath).TrimStart('.').ToUpperInvariant() + "Archive",
            JobRoot = paths.JobRoot,
            PdfTreeRoot = paths.PdfTreeRoot,
            MergedPdf = paths.MergedPdf,
            Status = MergeJobStatus.Importing
        };

        await dataStore.SaveJobAsync(job, cancellationToken);
        try
        {
            progress?.Report(new JobProgress(0, 0, "Reading input"));
            job.EffectiveInputPath = await importer.ImportAsync(sourcePath, paths, cancellationToken);
            var sourceFiles = traversal.GetInputFiles(job.EffectiveInputPath);
            if (sourceFiles.Count == 0)
            {
                throw new InvalidOperationException("No supported Word or PDF files were found in the input.");
            }

            jobCreated?.Invoke(job);
            job.Items = BuildItems(sourceFiles, job.EffectiveInputPath, paths.PdfTreeRoot);
            job.TotalItems = job.Items.Count;
            job.Status = MergeJobStatus.Processing;
            await dataStore.SaveJobAsync(job, cancellationToken);

            await ProcessItemsAsync(job, progress, cancellationToken);
            job.Status = MergeJobStatus.Merging;
            progress?.Report(new JobProgress(job.CompletedItems, job.TotalItems, "Merging PDFs"));
            job.OutputPageCount = await Task.Run(
                () => pdfMerge.Merge(job.Items.Select(item => item.TargetPdfPath).ToArray(), job.MergedPdf),
                cancellationToken);

            job.Status = MergeJobStatus.Completed;
            job.FinishedAt = DateTimeOffset.UtcNow;
            progress?.Report(new JobProgress(job.TotalItems, job.TotalItems, "Merge completed"));
            await PersistFinishedJobAsync(job);
            return job;
        }
        catch (OperationCanceledException)
        {
            job.Status = MergeJobStatus.Cancelled;
            job.Error = "Cancelled by user.";
            job.FinishedAt = DateTimeOffset.UtcNow;
            await PersistFinishedJobAsync(job);
            throw;
        }
        catch (Exception exception)
        {
            job.Status = MergeJobStatus.Failed;
            job.Error = exception.Message;
            job.FinishedAt = DateTimeOffset.UtcNow;
            await File.AppendAllTextAsync(paths.LogPath, $"{DateTimeOffset.Now:u} {exception}{Environment.NewLine}");
            await PersistFinishedJobAsync(job);
            throw;
        }
    }

    private static List<MergeJobItem> BuildItems(IReadOnlyList<string> sourceFiles, string inputRoot, string pdfTreeRoot)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<MergeJobItem>(sourceFiles.Count);
        for (var index = 0; index < sourceFiles.Count; index++)
        {
            var sourcePath = sourceFiles[index];
            var relativePath = Path.GetRelativePath(inputRoot, sourcePath);
            var targetPath = Path.Combine(pdfTreeRoot, Path.ChangeExtension(relativePath, ".pdf"));
            if (!targets.Add(targetPath))
            {
                throw new InvalidOperationException($"Multiple input files map to the same PDF: {relativePath}");
            }

            items.Add(new MergeJobItem
            {
                Order = index + 1,
                SourcePath = sourcePath,
                RelativePath = relativePath,
                Kind = WordExtensions.Contains(Path.GetExtension(sourcePath)) ? "Word" : "PDF",
                TargetPdfPath = targetPath
            });
        }

        return items;
    }

    private async Task ProcessItemsAsync(MergeJob job, IProgress<JobProgress>? progress, CancellationToken cancellationToken)
    {
        var pendingItems = new ConcurrentQueue<MergeJobItem>(job.Items);
        var completedLock = new object();
        var workerCount = Math.Min(MaxProcessingWorkers, job.Items.Count);
        var workers = Enumerable.Range(0, workerCount)
            .Select(_ => RunStaAsync(() => ProcessPendingItems(pendingItems, job, completedLock, progress, cancellationToken), cancellationToken))
            .ToArray();
        await Task.WhenAll(workers);
    }

    private void ProcessPendingItems(
        ConcurrentQueue<MergeJobItem> pendingItems,
        MergeJob job,
        object completedLock,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken)
    {
        WordConversionSession? wordSession = null;
        try
        {
            while (pendingItems.TryDequeue(out var item))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new JobProgress(job.CompletedItems, job.TotalItems, item.RelativePath, item));
                try
                {
                    if (item.Kind == "Word")
                    {
                        wordSession ??= wordConversion.CreateSession();
                        wordSession.ConvertToPdf(item.SourcePath, item.TargetPdfPath);
                    }
                    else
                    {
                        pdfCopy.Copy(item.SourcePath, item.TargetPdfPath);
                    }

                    item.Status = "Completed";
                    lock (completedLock)
                    {
                        job.CompletedItems++;
                    }
                }
                catch (Exception exception)
                {
                    item.Status = "Failed";
                    item.Error = exception.Message;
                    throw;
                }
            }
        }
        finally
        {
            wordSession?.Dispose();
        }
    }

    private async Task PersistFinishedJobAsync(MergeJob job)
    {
        await dataStore.SaveJobAsync(job, CancellationToken.None);
        await dataStore.AppendHistoryAsync(job, CancellationToken.None);
    }

    private static Task RunStaAsync(Action action, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Document processing is supported only on Windows.");
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Priority = ThreadPriority.BelowNormal;
        thread.Start();
        return completion.Task;
    }
}