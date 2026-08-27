using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Mergewell.Core.Services;

public sealed class PdfCopyService
{
    public void Copy(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(sourcePath, targetPath, true);
    }
}

public sealed class PdfMergeService
{
    public int Merge(IReadOnlyList<string> sourcePaths, string outputPath)
    {
        if (sourcePaths.Count == 0)
        {
            throw new InvalidOperationException("No PDF files were supplied for merging.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var output = new PdfDocument();
        foreach (var sourcePath in sourcePaths)
        {
            using var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
            for (var pageIndex = 0; pageIndex < input.PageCount; pageIndex++)
            {
                output.AddPage(input.Pages[pageIndex]);
            }
        }

        var pageCount = output.PageCount;
        output.Save(outputPath);
        return pageCount;
    }

    public static int GetPageCount(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }
}