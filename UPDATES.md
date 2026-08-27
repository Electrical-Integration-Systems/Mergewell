# Implementing Application Updates

This document describes how Mergewell can notify users about a new GitHub Release and install its MSI from inside the current WinUI 3 application.

## Current Constraints

Mergewell is distributed as a per-machine WiX MSI. The installer uses a stable `UpgradeCode` and WiX `MajorUpgrade`, so a newer MSI can replace an older installation while preserving app-owned data under `%USERPROFILE%\Documents\Mergewell`.

The running application cannot replace its own installed files. Because the MSI installs under `Program Files`, Windows will also request administrator approval. The update flow must therefore download the installer, start it as a separate elevated process, and close Mergewell.

GitHub automatically exposes source archives on every release. The updater must select the `.msi` release asset and ignore those archives.

## User Experience

1. Check for an update shortly after the main page loads, without delaying startup.
2. Query the latest non-draft, non-prerelease GitHub Release.
3. Compare its tag with the installed assembly version.
4. Show a compact info bar when a newer version exists.
5. Let the user choose `Update` or `Later`.
6. On `Update`, download the MSI while showing progress.
7. Verify the download before launching it.
8. Start the MSI with Windows elevation and close Mergewell.
9. Let WiX upgrade the installed application.
10. Optionally restart Mergewell after installation.

Update installation must be disabled while a merge is running. The user should be able to defer an update without interrupting normal work.

## Release Contract

Each production release should contain:

- A tag matching `v<MergewellVersion>`, such as `v0.0.2`.
- One asset named `Mergewell-v<version>-x64.msi`.
- One SHA-256 checksum file named `Mergewell-v<version>-x64.msi.sha256`.
- Release notes suitable for display or opening in a browser.

The workflow should calculate the checksum after building the MSI and upload both files:

```powershell
$msi = Get-Item .\app\artifacts\installer\Mergewell-v*-x64.msi
$hash = (Get-FileHash $msi.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $($msi.Name)" | Set-Content "$($msi.FullName).sha256" -Encoding ascii

gh release create "${{ github.ref_name }}" `
  $msi.FullName `
  "$($msi.FullName).sha256" `
  --generate-notes `
  --title "Mergewell ${{ github.ref_name }}"
```

Code signing should be added before enabling unattended update installation. Sign the application and MSI with a trusted Authenticode certificate in CI, then verify the signer in the updater. A checksum protects against accidental corruption; a trusted signature establishes who published the installer.

## GitHub Release Check

Add an `UpdateService` to `Mergewell.Core`. For this public repository it can call GitHub without a token:

```text
GET https://api.github.com/repos/Electrical-Integration-Systems/Mergewell/releases/latest
Accept: application/vnd.github+json
User-Agent: Mergewell/<installed-version>
X-GitHub-Api-Version: 2022-11-28
```

The response fields needed are:

- `tag_name`: available version, with the leading `v` removed before parsing.
- `html_url`: release-notes page.
- `assets[].name`: locate the exact MSI and checksum names.
- `assets[].browser_download_url`: download URLs.
- `draft` and `prerelease`: reject either unless a future preview channel is enabled.

Use `System.Net.Http.Json` and typed response models rather than parsing JSON manually. Configure a short timeout, such as ten seconds. Update checks should fail silently into a diagnostic log because unavailable internet access must never block the application.

GitHub's unauthenticated API rate limit is sufficient for one check per launch. Cache the last check time and result in `settings.json` and avoid checking more than once every 24 hours unless the user explicitly requests it.

## Version Comparison

Read the installed version from the running assembly:

```csharp
var currentVersion = typeof(UpdateService).Assembly.GetName().Version
    ?? throw new InvalidOperationException("The installed version is unavailable.");
```

Parse the release tag after requiring the format `vMAJOR.MINOR.PATCH`. Compare numeric `Version` values, not strings. For example, `0.0.10` is newer than `0.0.9`, which is not true under lexical comparison.

The existing `app/Directory.Build.props` remains the version source. Before publishing, increment `MergewellVersion`, commit it, and create the matching tag. Keep the WiX `UpgradeCode` unchanged across releases.

## Proposed Core Types

```text
Mergewell.Core/
  Models/
    UpdateInfo.cs
  Services/
    UpdateService.cs
    UpdateInstaller.cs
```

`UpdateInfo` should contain the available version, release-notes URL, MSI URL, checksum URL, and asset filename.

`UpdateService` should:

- Read and compare versions.
- Query and validate the latest GitHub Release.
- Select assets by exact expected filename.
- Download the MSI and checksum to `%LOCALAPPDATA%\Mergewell\Updates\<version>`.
- Stream downloads to disk and report progress through `IProgress<double>`.
- Delete incomplete downloads after cancellation or failure.

`UpdateInstaller` should:

- Verify the MSI SHA-256 hash using `SHA256.HashDataAsync` and fixed-time comparison.
- Verify the Authenticode signature and expected publisher once releases are signed.
- Reject paths outside the app-owned update directory.
- Start `msiexec.exe` with elevation.
- Return only after the installer process was started successfully.

## Starting The Installer

The first implementation should use an attended upgrade so the user sees Windows Installer progress and errors:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = "msiexec.exe",
    Arguments = $"/i \"{msiPath}\" /passive /norestart",
    UseShellExecute = true,
    Verb = "runas"
};

Process.Start(startInfo);
```

After the process starts, persist a small `pending-update.json` containing the target version, request application shutdown, and allow the MSI to continue independently. Catch the exception produced when the user declines the elevation prompt and leave the current app running.

Do not use `/quiet` initially. A visible installer gives users feedback and exposes installation failures. A later updater helper can wait for `msiexec`, capture its exit code, and restart Mergewell only after success.

## Restart Strategy

For a complete one-click experience, add a small separately published `Mergewell.Updater.exe` helper. The application would launch it with the current process ID, MSI path, expected hash, and application executable path, then exit. The helper would:

1. Wait for Mergewell to exit.
2. Verify the installer again.
3. Launch the elevated MSI and wait for completion.
4. Treat Windows Installer exit codes `0` and `3010` as success.
5. Start the newly installed `Mergewell.exe`.
6. Remove downloaded update files when possible.

Arguments must be treated as untrusted input even though the helper is local. Prefer a small JSON instruction file inside the protected update directory over many command-line arguments, and validate all paths and values.

## WinUI Integration

Call the check from `MainPageViewModel.InitializeAsync` after history loads. Expose these observable properties:

```text
IsUpdateAvailable
AvailableVersion
UpdateStatus
UpdateProgress
IsDownloadingUpdate
CanInstallUpdate
ReleaseNotesUrl
```

Add an `InfoBar` near the top of `MainPage.xaml` with text such as `Mergewell 0.0.2 is available`, an `Update` button, a `Later` button, and a release-notes link. Disable the update action while `IsBusy` is true. Bind download progress to a determinate progress bar after the user starts the update.

The notification should not be a modal dialog on startup. Reserve a modal confirmation for the point where the app must close and Windows will request elevation.

## Failure Handling

- No network: hide the notification and log the check failure.
- GitHub rate limit: honor the reset time and use the cached result.
- Invalid release tag: ignore the release and log a warning.
- Missing or duplicate MSI asset: reject the release.
- Checksum mismatch: delete the MSI and show an error; never launch it.
- Invalid signature: delete the MSI and show an error; never launch it.
- Download interruption: keep the installed version and permit retry.
- Elevation declined: keep Mergewell open and permit retry.
- Merge in progress: defer installation until processing finishes.
- Installer failure: retain logs and continue using the existing installation where Windows Installer rollback succeeds.

## Testing

Unit tests should cover version parsing and ordering, draft/prerelease rejection, exact asset selection, malformed API responses, cache expiry, checksum verification, and download cancellation.

Integration tests should use a fake HTTP handler and temporary update directory. Do not call GitHub from automated tests. Test the installer command construction separately; do not launch elevation or install an MSI during the normal test suite.

Before shipping, test upgrades from at least the previous release on Windows 10 and Windows 11. Verify application files are replaced, user job history remains intact, cancellation works, declined elevation is handled, and the application restarts at the new version.

## Recommended Delivery Order

1. Add checksum generation and upload to the release workflow.
2. Add code signing for the application and MSI.
3. Implement release checking and a non-blocking update notification.
4. Implement verified MSI download and attended installation.
5. Add the updater helper for wait, result handling, cleanup, and restart.
6. Add a manual `Check for updates` command and update-channel settings if needed.

The notification-only portion can ship before automatic installation. Automatic installation should not ship until checksum and publisher verification are enforced.