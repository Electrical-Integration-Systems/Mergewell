using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Mergewell_App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        SizeAndCenterWindow();
        RootFrame.Navigate(typeof(MainPage));
    }

    private void SizeAndCenterWindow()
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96d;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)Math.Round(1280 * scale), workArea.Width - 64);
        var height = Math.Min((int)Math.Round(960 * scale), workArea.Height - 64);
        var x = workArea.X + (workArea.Width - width) / 2;
        var y = workArea.Y + (workArea.Height - height) / 2;
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}
