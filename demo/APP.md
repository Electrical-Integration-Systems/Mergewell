# Mergewell PowerShell Prototype

The script in this directory is the original prototype and behavior reference for Mergewell. The production application now lives under `app/` and implements the workflow directly in C# rather than invoking this script.

## Shared Behavior

Both implementations:

- Accept a folder, ZIP archive, or RAR archive.
- Process `.doc`, `.docx`, `.docm`, `.rtf`, and `.pdf` files.
- Traverse depth-first, visiting directories before files and sorting names alphabetically.
- Preserve the relative directory structure in a PDF tree.
- Merge PDFs in the same order used during traversal.
- Reject duplicate output paths such as `Report.docx` and `Report.pdf` in one folder.

## Important Differences

The desktop application owns its job storage under `%USERPROFILE%\Documents\Mergewell`, references folder inputs in place, extracts archives into isolated job folders, and records metadata and history. It uses `SharpCompress` for archives and `PDFsharp` for merging.

The prototype requires explicit input, PDF-tree, and merged-output paths. RAR input requires 7-Zip, UnRAR, or WinRAR, and merging requires `pdfunite`, `qpdf`, or Ghostscript. It remains useful for comparing traversal and output order, but it is not part of the application runtime or installer.

## Prototype Usage

```powershell
.\Convert-WordDocumentsToPdfAndMerge.ps1 ".\input" ".\pdf-tree" ".\merged.pdf" -Overwrite
```

Microsoft Word is required only when the input contains Word documents. PDF-only input does not require Word.

See `app/README.md` for the current application documentation.