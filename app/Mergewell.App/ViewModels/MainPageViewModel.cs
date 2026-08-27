using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Mergewell.Core.Models;
using Mergewell.Core.Services;

namespace Mergewell_App.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly AppStorageService _storage = new();
    private readonly AppDataStore _dataStore;
    private readonly JobRunner _jobRunner;
    private CancellationTokenSource? _cancellation;
    private MergeJobRowViewModel? _activeMerge;

    [ObservableProperty] private string _selectedInput = string.Empty;
    [ObservableProperty] private string _selectedInputName = "Drop a folder or archive to begin";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private string _currentFile = "No active merge";
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public MainPageViewModel()
    {
        _dataStore = new AppDataStore(_storage);
        _jobRunner = new JobRunner(_storage, _dataStore, new ImportService(), new TraversalService(),
            new WordConversionService(), new PdfCopyService(), new PdfMergeService());
    }

    public ObservableCollection<MergeJobRowViewModel> Merges { get; } = [];
    public bool CanStart => !IsBusy && !string.IsNullOrWhiteSpace(SelectedInput);
    public bool CanCancel => IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnSelectedInputChanged(string value) => OnPropertyChanged(nameof(CanStart));

    public async Task InitializeAsync()
    {
        await RefreshHistoryAsync();
    }

    public void SelectInput(string path)
    {
        SelectedInput = path;
        SelectedInputName = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileName(path);
        StatusMessage = "Input ready";
        HasError = false;
        ErrorMessage = string.Empty;
    }

    public async Task StartAsync()
    {
        if (!CanStart) return;

        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        ProgressValue = 0;
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<JobProgress>(update =>
        {
            StatusMessage = update.Message;
            CurrentFile = update.Item?.RelativePath ?? update.Message;
            ProgressValue = update.Total == 0 ? 0 : update.Completed * 100d / update.Total;
            _activeMerge?.Refresh();
        });

        try
        {
            await _jobRunner.RunAsync(SelectedInput, progress, _cancellation.Token, job =>
            {
                _activeMerge = new MergeJobRowViewModel(job, true);
                Merges.Insert(0, _activeMerge);
            });
            ProgressValue = 100;
            StatusMessage = "Merge completed";
            CurrentFile = "Output is ready";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Merge cancelled";
            CurrentFile = "No active merge";
        }
        catch (Exception exception)
        {
            HasError = true;
            ErrorMessage = exception.Message;
            StatusMessage = "Merge failed";
            CurrentFile = "Review the error below";
        }
        finally
        {
            _activeMerge?.SetCurrent(false);
            _activeMerge = null;
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
            await RefreshHistoryAsync();
        }
    }

    public void Cancel(MergeJobRowViewModel merge)
    {
        if (ReferenceEquals(merge, _activeMerge)) _cancellation?.Cancel();
    }

    public async Task DeleteAsync(MergeJobRowViewModel merge)
    {
        if (ReferenceEquals(merge, _activeMerge)) return;
        try
        {
            await _dataStore.DeleteJobAsync(merge.Job);
            Merges.Remove(merge);
        }
        catch (Exception exception)
        {
            HasError = true;
            ErrorMessage = exception.Message;
        }
    }

    public static void OpenOutput(MergeJobRowViewModel merge)
    {
        if (merge.CanOpenOutput) Process.Start(new ProcessStartInfo(merge.Job.MergedPdf) { UseShellExecute = true });
    }

    public static void OpenFolder(MergeJobRowViewModel merge)
    {
        if (merge.CanOpenFolder) Process.Start(new ProcessStartInfo(merge.Job.JobRoot) { UseShellExecute = true });
    }

    private async Task RefreshHistoryAsync()
    {
        Merges.Clear();
        foreach (var job in await _dataStore.LoadJobsAsync()) Merges.Add(new MergeJobRowViewModel(job));
    }
}