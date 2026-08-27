[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$publishDirectory = Join-Path $root 'artifacts\publish'
$installerDirectory = Join-Path $root 'artifacts\installer'
$project = Join-Path $root 'Mergewell.App\Mergewell.App.csproj'
$wixSource = Join-Path $root 'Installer\Package.wxs'
$versionProperties = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props'))
$version = $versionProperties.Project.PropertyGroup.MergewellVersion
$outputMsi = Join-Path $installerDirectory "Mergewell-v$version-x64.msi"

Remove-Item -LiteralPath $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDirectory, $installerDirectory -Force | Out-Null

dotnet publish $project -c $Configuration -p:Platform=x64 -p:PublishProfile=Installer
if ($LASTEXITCODE -ne 0) {
    throw "Application publish failed with exit code $LASTEXITCODE."
}

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "Local tool restore failed with exit code $LASTEXITCODE."
    }

    dotnet wix build $wixSource -arch x64 -d "AppVersion=$version" -ext WixToolset.UI.wixext -bindpath "Publish=$publishDirectory" -o $outputMsi
    if ($LASTEXITCODE -ne 0) {
        throw "Installer build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Output $outputMsi