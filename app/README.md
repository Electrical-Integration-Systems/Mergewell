# Mergewell Application

Mergewell is a WinUI 3 desktop application for turning a folder, ZIP archive, or RAR archive of Word documents and PDFs into one ordered PDF.

## Behavior

For each merge, the application:

1. Accepts a folder or archive from drag and drop or a picker.
2. Extracts archives into an isolated job folder while referencing folder inputs in place.
3. Traverses directories depth-first, with directories before files and alphabetical ordering at each level.
4. Converts `.doc`, `.docx`, `.docm`, and `.rtf` files through Microsoft Word automation.
5. Copies existing PDFs into a mirrored PDF tree.
6. Merges the resulting PDFs in traversal order.
7. Stores job metadata, logs, outputs, and history under the app-managed storage root.

The interface shows dependency status, progress, the ordered file list, errors, and previous merge results. Completed PDFs can be opened from the result list.

## Requirements

- Windows 10 version 1809 or newer.
- Microsoft Word for jobs containing Word documents.
- .NET 8 SDK and Windows development tools for local development.

PDF-only jobs do not require Word. Archive extraction and PDF merging use `SharpCompress` and `PDFsharp`, so users do not need separate archive or PDF command-line tools.

## Projects

- `Mergewell.App` contains the WinUI 3 interface and view models.
- `Mergewell.Core` contains storage, traversal, import, conversion, PDF, and job services.
- `Mergewell.Tests` contains behavior-focused xUnit tests.
- `Installer` contains the WiX MSI definition and assets.

The UI remains thin; long-running work and persistence are owned by the core services.

## Storage

Application data is stored under:

```text
%USERPROFILE%\Documents\Mergewell
```

Each job has isolated `original`, `imported`, `pdf-tree`, `output`, and `logs` directories. Folder inputs are referenced directly. Archive inputs are extracted under the job directory. Settings use JSON and history uses JSON Lines.

## Development

Run tests:

```powershell
dotnet test .\Mergewell.Tests\Mergewell.Tests.csproj -c Release
```

Build or run the x64 application:

```powershell
dotnet build .\Mergewell.App\Mergewell.App.csproj -c Release -p:Platform=x64
dotnet run --project .\Mergewell.App\Mergewell.App.csproj -p:Platform=x64
```

## Installer

The repository uses a local WiX tool manifest. Build a self-contained x64 MSI with:

```powershell
.\build-installer.ps1
```

The MSI is written to `artifacts\installer\Mergewell-v<version>-x64.msi`. Microsoft Word is not redistributed because it requires a separate Office license.

## Version And Releases

The public version source is `Directory.Build.props`. Change `MergewellVersion` there before creating a release tag.

Pushes build and test the application through GitHub Actions. A tag matching `v*` additionally builds the MSI and creates a GitHub release with the installer attached. The tag should match the configured version, for example `v0.0.1`.