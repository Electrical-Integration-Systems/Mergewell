# Mergewell

Mergewell is a Windows desktop application that converts Word documents to PDF, preserves existing PDFs, and merges them in deterministic folder order. It accepts folders, ZIP archives, and RAR archives through a WinUI 3 interface.

## Repository

- `app/` contains the .NET 8 application, tests, and WiX installer.
- `demo/` contains the original PowerShell prototype used as a behavior reference.
- `IDEAS.md` collects possible future improvements.

## Build

```powershell
dotnet test .\app\Mergewell.Tests\Mergewell.Tests.csproj -c Release
dotnet build .\app\Mergewell.App\Mergewell.App.csproj -c Release -p:Platform=x64
```

Build the MSI with:

```powershell
.\app\build-installer.ps1
```

See `app/README.md` for requirements, architecture, storage, and release details.

## Versioning

Edit `app/Directory.Build.props` to change the application version. The application and installer build both read the `MergewellVersion` property.

## License

Mergewell is available under the MIT License. See `LICENSE`.