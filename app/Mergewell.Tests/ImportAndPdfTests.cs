using Mergewell.Core.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Mergewell.Tests;

public sealed class ImportAndPdfTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"MergewellPipelineTests-{Guid.NewGuid():N}");

    public ImportAndPdfTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ImportAsync_ReferencesFolderWithoutCopyingIt()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(Path.Combine(source, "A"));
        await File.WriteAllTextAsync(Path.Combine(source, "A", "document.pdf"), "pdf");
        var paths = new AppStorageService(Path.Combine(_root, "storage")).CreateJobPaths("source");

        var imported = await new ImportService().ImportAsync(source, paths, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(source), imported);
        Assert.Empty(Directory.EnumerateFileSystemEntries(paths.ImportedRoot));
    }

    [Fact]
    public void Merge_CreatesPdfWithAllPagesInOrder()
    {
        var first = CreatePdf("first.pdf", 1);
        var second = CreatePdf("second.pdf", 2);
        var output = Path.Combine(_root, "merged.pdf");

        var pageCount = new PdfMergeService().Merge([first, second], output);

        using var merged = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(3, pageCount);
        Assert.Equal(3, merged.PageCount);
    }

    private string CreatePdf(string name, int pages)
    {
        var path = Path.Combine(_root, name);
        using var document = new PdfDocument();
        for (var index = 0; index < pages; index++)
        {
            document.AddPage();
        }

        document.Save(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}