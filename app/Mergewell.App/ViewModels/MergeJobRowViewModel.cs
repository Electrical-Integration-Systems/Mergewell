using CommunityToolkit.Mvvm.ComponentModel;
using Mergewell.Core.Models;

namespace Mergewell_App.ViewModels;

public sealed class MergeJobRowViewModel(MergeJob job, bool isCurrent = false) : ObservableObject
{
    public MergeJob Job { get; } = job;
    public string OriginalInputName => Job.OriginalInputName;
    public string CreatedAtText => Job.CreatedAtText;
    public string StatusText => Job.StatusText;
    public string ProgressText => Job.TotalItems == 0 ? "Preparing" : $"{Job.CompletedItems} of {Job.TotalItems} files";
    public string PageCountText => Job.OutputPageCount switch
    {
        1 => "1 page",
        > 1 => $"{Job.OutputPageCount} pages",
        _ => "No output"
    };
    public bool CanCancel => isCurrent;
    public bool CanDelete => !isCurrent;
    public bool CanOpenOutput => Job.CanOpenOutput;
    public bool CanOpenFolder => Directory.Exists(Job.JobRoot);

    public void SetCurrent(bool value)
    {
        isCurrent = value;
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(PageCountText));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanOpenOutput));
        OnPropertyChanged(nameof(CanOpenFolder));
    }
}
