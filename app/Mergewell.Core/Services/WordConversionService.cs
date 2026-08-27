using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace Mergewell.Core.Services;

public sealed class WordConversionService
{
    public static bool IsWordAvailable() => OperatingSystem.IsWindows() && Type.GetTypeFromProgID("Word.Application") is not null;

    public WordConversionSession CreateSession() => new();
}

public sealed class WordConversionSession : IDisposable
{
    private dynamic? _wordApplication;

    public WordConversionSession()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Microsoft Word conversion is supported only on Windows.");
        }

        var wordType = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException("Microsoft Word is required to convert Word documents.");
        try
        {
            _wordApplication = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("Microsoft Word could not be started.");
            _wordApplication.Visible = false;
            _wordApplication.DisplayAlerts = 0;
            TrySetBelowNormalPriority();
        }
        catch
        {
            ReleaseWordApplication();
            throw;
        }
    }

    public void ConvertToPdf(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        dynamic? document = null;
        try
        {
            document = _wordApplication!.Documents.Open(sourcePath, ReadOnly: true, Visible: false);
            document.ExportAsFixedFormat(targetPath, 17);
        }
        finally
        {
            if (document is not null)
            {
                document.Close(false);
                Marshal.FinalReleaseComObject(document);
            }
        }
    }

    public void Dispose()
    {
        ReleaseWordApplication();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private void ReleaseWordApplication()
    {
        if (_wordApplication is null) return;

        try
        {
            _wordApplication.Quit();
        }
        catch (COMException)
        {
        }
        finally
        {
            Marshal.FinalReleaseComObject(_wordApplication);
            _wordApplication = null;
        }
    }

    private void TrySetBelowNormalPriority()
    {
        uint processId;
        try
        {
            _ = GetWindowThreadProcessId((nint)_wordApplication!.Hwnd, out processId);
        }
        catch (RuntimeBinderException)
        {
            return;
        }
        catch (COMException)
        {
            return;
        }

        if (processId == 0) return;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);
}