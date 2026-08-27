namespace Mergewell.Core.Models;

public enum MergeJobStatus
{
    Pending,
    Importing,
    Processing,
    Merging,
    Completed,
    Failed,
    Cancelled
}

public sealed class MergeJob
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string OriginalInputName { get; set; } = string.Empty;
    public string OriginalInputKind { get; set; } = string.Empty;
    public string JobRoot { get; set; } = string.Empty;
    public string EffectiveInputPath { get; set; } = string.Empty;
    public string PdfTreeRoot { get; set; } = string.Empty;
    public string MergedPdf { get; set; } = string.Empty;
    public MergeJobStatus Status { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int OutputPageCount { get; set; }
    public string? Error { get; set; }
    public List<MergeJobItem> Items { get; set; } = [];

    public string StatusText => Status.ToString();
    public string CreatedAtText => CreatedAt.LocalDateTime.ToString("g");
    public bool CanOpenOutput => Status == MergeJobStatus.Completed && File.Exists(MergedPdf);
}

public sealed class MergeJobItem
{
    public int Order { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string TargetPdfPath { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
}

public sealed record JobProgress(int Completed, int Total, string Message, MergeJobItem? Item = null);

public sealed record JobPaths(
    string JobRoot,
    string OriginalRoot,
    string ImportedRoot,
    string PdfTreeRoot,
    string OutputRoot,
    string MergedPdf,
    string LogPath);