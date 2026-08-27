using Mergewell.Core.Models;
using Mergewell_App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Mergewell_App;

public sealed partial class MainPage : Page
{
    private MergeJobRowViewModel? _pendingDelete;

    public MainPageViewModel ViewModel { get; } = new();
    public MainPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await ViewModel.InitializeAsync();
    private void Page_DragOver(object sender, DragEventArgs e) { e.AcceptedOperation = DataPackageOperation.Link; e.DragUIOverride.Caption = "Use this path in Mergewell"; }
    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var item = (await e.DataView.GetStorageItemsAsync()).FirstOrDefault();
        if (item is not null && IsSupportedInput(item)) ViewModel.SelectInput(item.Path);
    }
    private async void UploadFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var folder = await picker.PickSingleFolderAsync(); if (folder is not null) ViewModel.SelectInput(folder.Path);
    }
    private async void UploadArchive_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip"); picker.FileTypeFilter.Add(".rar");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var file = await picker.PickSingleFileAsync(); if (file is not null) ViewModel.SelectInput(file.Path);
    }
    private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.StartAsync();
    private void CancelMerge_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: MergeJobRowViewModel merge }) ViewModel.Cancel(merge); }
    private void OpenOutput_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: MergeJobRowViewModel merge }) MainPageViewModel.OpenOutput(merge); }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: MergeJobRowViewModel merge }) MainPageViewModel.OpenFolder(merge); }
    private void DeleteMerge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MergeJobRowViewModel merge }) return;
        _pendingDelete = merge;
        DialogMessage.Text = $"This permanently removes {merge.OriginalInputName} and its generated files.";
        DialogOverlay.Visibility = Visibility.Visible;
        KeepDialogButton.Focus(FocusState.Programmatic);
    }
    private void CloseDialog_Click(object sender, RoutedEventArgs e) => CloseDialog();
    private async void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        var merge = _pendingDelete;
        CloseDialog();
        if (merge is not null) await ViewModel.DeleteAsync(merge);
    }
    private void Page_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && DialogOverlay.Visibility == Visibility.Visible)
        {
            CloseDialog();
            e.Handled = true;
        }
    }
    private void CloseDialog()
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
        _pendingDelete = null;
    }
    private static bool IsSupportedInput(IStorageItem item) => item is StorageFolder || Path.GetExtension(item.Name) is var extension && (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) || extension.Equals(".rar", StringComparison.OrdinalIgnoreCase));
}