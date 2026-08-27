[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$InputPath,

    [Parameter(Mandatory = $true, Position = 1)]
    [ValidateNotNullOrEmpty()]
    [string]$PdfTreeRoot,

    [Parameter(Mandatory = $true, Position = 2)]
    [ValidateNotNullOrEmpty()]
    [string]$MergedPdf,

    [string]$PdfMergeUtility,

    [string]$ArchiveExtractor,

    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SupportedDocumentExtensions = @('.doc', '.docx', '.docm', '.rtf')
$SupportedPdfExtensions = @('.pdf')
$SupportedInputExtensions = $SupportedDocumentExtensions + $SupportedPdfExtensions
$SupportedArchiveExtensions = @('.zip', '.rar')
$TemporaryExtractionRoot = $null
$WdExportFormatPdf = 17

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Get-DepthFirstInputFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$MergedPdfPath,

        [Parameter(Mandatory = $true)]
        [string]$PdfTreePath
    )

    $items = Get-ChildItem -LiteralPath $RootPath -Force | Sort-Object -Property @{ Expression = { -not $_.PSIsContainer } }, Name

    foreach ($item in $items) {
        if ($item.PSIsContainer) {
            Get-DepthFirstInputFiles -RootPath $item.FullName -MergedPdfPath $MergedPdfPath -PdfTreePath $PdfTreePath
            continue
        }

        $extension = $item.Extension.ToLowerInvariant()
        if ($SupportedInputExtensions -notcontains $extension) {
            continue
        }

        if ($item.Name.StartsWith('~$')) {
            continue
        }

        if ($item.FullName -ieq $MergedPdfPath -or $item.FullName.StartsWith($PdfTreePath, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $item.FullName
    }
}

function Get-InputFolder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedInputPath,

        [string]$ExtractorPath
    )

    if (Test-Path -LiteralPath $ResolvedInputPath -PathType Container) {
        return $ResolvedInputPath
    }

    if (-not (Test-Path -LiteralPath $ResolvedInputPath -PathType Leaf)) {
        throw "Input path does not exist: $ResolvedInputPath"
    }

    $archiveExtension = [System.IO.Path]::GetExtension($ResolvedInputPath).ToLowerInvariant()
    if ($SupportedArchiveExtensions -notcontains $archiveExtension) {
        throw "Input path must be a folder, .zip archive, or .rar archive: $ResolvedInputPath"
    }

    $script:TemporaryExtractionRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Mergewell_" + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $script:TemporaryExtractionRoot | Out-Null

    if ($archiveExtension -eq '.zip') {
        Expand-Archive -LiteralPath $ResolvedInputPath -DestinationPath $script:TemporaryExtractionRoot -Force
    }
    else {
        $resolvedArchiveExtractor = Resolve-ArchiveExtractor -ExtractorPath $ExtractorPath
        Expand-RarArchive -ArchivePath $ResolvedInputPath -DestinationPath $script:TemporaryExtractionRoot -ExtractorPath $resolvedArchiveExtractor
    }

    $script:TemporaryExtractionRoot
}

function Resolve-ArchiveExtractor {
    param(
        [string]$ExtractorPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExtractorPath)) {
        $command = Get-Command $ExtractorPath -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }

        $resolvedExtractorPath = Resolve-FullPath -Path $ExtractorPath
        if (Test-Path -LiteralPath $resolvedExtractorPath -PathType Leaf) {
            return $resolvedExtractorPath
        }

        throw "Archive extractor does not exist or is not on PATH: $ExtractorPath"
    }

    foreach ($extractorName in @('7z', '7za', '7zz', 'unrar', 'winrar')) {
        $command = Get-Command $extractorName -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    $programRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $commonPaths = foreach ($programRoot in $programRoots) {
        Join-Path $programRoot '7-Zip\7z.exe'
        Join-Path $programRoot 'WinRAR\UnRAR.exe'
        Join-Path $programRoot 'WinRAR\WinRAR.exe'
    }

    foreach ($commonPath in $commonPaths) {
        if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
            return $commonPath
        }
    }

    throw "RAR input requires 7-Zip, UnRAR, or WinRAR. Install one of them, add it to PATH, or pass -ArchiveExtractor with the full executable path."
}

function Expand-RarArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [string]$ExtractorPath
    )

    $extractorName = [System.IO.Path]::GetFileNameWithoutExtension($ExtractorPath).ToLowerInvariant()
    switch -Regex ($extractorName) {
        '^(7z|7za|7zz)$' {
            $extractOutput = & $ExtractorPath 'x' '-y' "-o$DestinationPath" $ArchivePath 2>&1
            break
        }
        '^unrar$' {
            $extractOutput = & $ExtractorPath 'x' '-y' $ArchivePath $DestinationPath 2>&1
            break
        }
        '^winrar$' {
            $extractOutput = & $ExtractorPath 'x' '-ibck' '-y' $ArchivePath $DestinationPath 2>&1
            break
        }
        default {
            throw "Unsupported archive extractor: $ExtractorPath. Use 7-Zip, UnRAR, or WinRAR for .rar input."
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Archive extractor failed with exit code $LASTEXITCODE. Output: $($extractOutput -join [Environment]::NewLine)"
    }
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath
    )

    $rootUri = [System.Uri]::new((Join-Path $RootPath '.'))
    $childUri = [System.Uri]::new($ChildPath)
    [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($childUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Convert-WordDocumentToPdf {
    param(
        [Parameter(Mandatory = $true)]
        [object]$WordApplication,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$PdfPath
    )

    $sourceDocument = $null

    try {
        $pdfDirectory = Split-Path -Parent $PdfPath
        if (-not (Test-Path -LiteralPath $pdfDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $pdfDirectory | Out-Null
        }

        if (Test-Path -LiteralPath $PdfPath -PathType Leaf) {
            Remove-Item -LiteralPath $PdfPath -Force
        }

        $readOnly = $true
        $visible = $false
        $sourceDocument = $WordApplication.Documents.Open([ref]$SourcePath, [ref]$false, [ref]$readOnly, [ref]$false, [ref]'', [ref]'', [ref]$false, [ref]'', [ref]'', [ref]0, [ref]$false, [ref]$visible)
        $sourceDocument.ExportAsFixedFormat($PdfPath, $WdExportFormatPdf)
    }
    finally {
        if ($null -ne $sourceDocument) {
            $sourceDocument.Close([ref]$false)
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($sourceDocument) | Out-Null
        }
    }
}

function Copy-PdfFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$PdfPath
    )

    $pdfDirectory = Split-Path -Parent $PdfPath
    if (-not (Test-Path -LiteralPath $pdfDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $pdfDirectory | Out-Null
    }

    if ($SourcePath -ieq $PdfPath) {
        return
    }

    if (Test-Path -LiteralPath $PdfPath -PathType Leaf) {
        Remove-Item -LiteralPath $PdfPath -Force
    }

    Copy-Item -LiteralPath $SourcePath -Destination $PdfPath
}

function Resolve-PdfMergeUtility {
    param(
        [string]$UtilityPath
    )

    if (-not [string]::IsNullOrWhiteSpace($UtilityPath)) {
        $resolvedUtilityPath = Resolve-FullPath -Path $UtilityPath
        if (-not (Test-Path -LiteralPath $resolvedUtilityPath -PathType Leaf)) {
            throw "PDF merge utility does not exist: $resolvedUtilityPath"
        }

        return $resolvedUtilityPath
    }

    foreach ($utilityName in @('pdfunite', 'qpdf', 'gswin64c', 'gswin32c', 'gs')) {
        $command = Get-Command $utilityName -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    throw "No PDF merge utility was found. Install pdfunite, qpdf, or Ghostscript, or pass -PdfMergeUtility with the full path to one."
}

function Merge-PdfFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$SourcePdfPaths,

        [Parameter(Mandatory = $true)]
        [string]$OutputPdfPath,

        [Parameter(Mandatory = $true)]
        [string]$UtilityPath
    )

    if ($SourcePdfPaths.Count -eq 0) {
        throw "No generated PDFs were available to merge."
    }

    if (Test-Path -LiteralPath $OutputPdfPath -PathType Leaf) {
        Remove-Item -LiteralPath $OutputPdfPath -Force
    }

    $utilityName = [System.IO.Path]::GetFileNameWithoutExtension($UtilityPath).ToLowerInvariant()
    switch -Regex ($utilityName) {
        '^pdfunite$' {
            $mergeOutput = & $UtilityPath @SourcePdfPaths $OutputPdfPath 2>&1
            break
        }
        '^qpdf$' {
            $mergeOutput = & $UtilityPath '--empty' '--pages' @SourcePdfPaths '--' $OutputPdfPath 2>&1
            break
        }
        '^(gs|gswin32c|gswin64c)$' {
            $mergeOutput = & $UtilityPath '-dBATCH' '-dNOPAUSE' '-q' '-sDEVICE=pdfwrite' "-sOutputFile=$OutputPdfPath" @SourcePdfPaths 2>&1
            break
        }
        default {
            throw "Unsupported PDF merge utility: $UtilityPath. Use pdfunite, qpdf, or Ghostscript."
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "PDF merge utility failed with exit code $LASTEXITCODE. Output: $($mergeOutput -join [Environment]::NewLine)"
    }

    if (-not (Test-Path -LiteralPath $OutputPdfPath -PathType Leaf)) {
        throw "PDF merge utility completed but did not create: $OutputPdfPath"
    }
}

$resolvedInputPath = Resolve-FullPath -Path $InputPath
$resolvedPdfTreeRoot = Resolve-FullPath -Path $PdfTreeRoot
$resolvedMergedPdf = Resolve-FullPath -Path $MergedPdf

if ([System.IO.Path]::GetExtension($resolvedMergedPdf).ToLowerInvariant() -ne '.pdf') {
    throw "Merged output path must end with .pdf: $resolvedMergedPdf"
}

$pdfTreeParent = Split-Path -Parent $resolvedPdfTreeRoot
if ([string]::IsNullOrWhiteSpace($pdfTreeParent)) {
    $pdfTreeParent = (Get-Location).ProviderPath
}

$mergedPdfDirectory = Split-Path -Parent $resolvedMergedPdf
if ([string]::IsNullOrWhiteSpace($mergedPdfDirectory)) {
    $mergedPdfDirectory = (Get-Location).ProviderPath
}

if (-not (Test-Path -LiteralPath $pdfTreeParent -PathType Container)) {
    New-Item -ItemType Directory -Path $pdfTreeParent | Out-Null
}

if (-not (Test-Path -LiteralPath $mergedPdfDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $mergedPdfDirectory | Out-Null
}

if ((Test-Path -LiteralPath $resolvedPdfTreeRoot -PathType Container) -and -not $Overwrite) {
    $existingPdf = Get-ChildItem -LiteralPath $resolvedPdfTreeRoot -Filter '*.pdf' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $existingPdf) {
        throw "PDF tree already contains PDFs. Use -Overwrite to replace generated PDFs under: $resolvedPdfTreeRoot"
    }
}

if ((Test-Path -LiteralPath $resolvedMergedPdf -PathType Leaf) -and -not $Overwrite) {
    throw "Merged PDF already exists. Use -Overwrite to replace it: $resolvedMergedPdf"
}

$wordApplication = $null

try {
    $inputFolder = Get-InputFolder -ResolvedInputPath $resolvedInputPath -ExtractorPath $ArchiveExtractor
    $inputFilesToMerge = @(Get-DepthFirstInputFiles -RootPath $inputFolder -MergedPdfPath $resolvedMergedPdf -PdfTreePath $resolvedPdfTreeRoot)

    if ($inputFilesToMerge.Count -eq 0) {
        throw "No supported Word or PDF files were found under: $resolvedInputPath"
    }

    $pdfTargets = @{}
    foreach ($sourceFile in $inputFilesToMerge) {
        $relativePath = Get-RelativePath -RootPath $inputFolder -ChildPath $sourceFile
        $relativePdfPath = [System.IO.Path]::ChangeExtension($relativePath, '.pdf')
        $targetPdfPath = Join-Path $resolvedPdfTreeRoot $relativePdfPath

        if ($targetPdfPath -ieq $resolvedMergedPdf) {
            throw "Merged PDF path conflicts with an individual converted PDF: $targetPdfPath"
        }

        if ($pdfTargets.ContainsKey($targetPdfPath)) {
            throw "Two source documents map to the same PDF path: $targetPdfPath"
        }

        $pdfTargets[$targetPdfPath] = $sourceFile
    }

    if (-not (Test-Path -LiteralPath $resolvedPdfTreeRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $resolvedPdfTreeRoot | Out-Null
    }

    $requiresWord = $false
    foreach ($sourceFile in $inputFilesToMerge) {
        if ($SupportedDocumentExtensions -contains ([System.IO.Path]::GetExtension($sourceFile).ToLowerInvariant())) {
            $requiresWord = $true
            break
        }
    }

    if ($requiresWord) {
        $wordApplication = New-Object -ComObject Word.Application
        $wordApplication.Visible = $false
        $wordApplication.DisplayAlerts = 0
    }

    $pdfFilesToMerge = New-Object System.Collections.Generic.List[string]

    for ($inputFileIndex = 0; $inputFileIndex -lt $inputFilesToMerge.Count; $inputFileIndex++) {
        $sourceFile = $inputFilesToMerge[$inputFileIndex]
        $relativePath = Get-RelativePath -RootPath $inputFolder -ChildPath $sourceFile
        $targetPdfPath = Join-Path $resolvedPdfTreeRoot ([System.IO.Path]::ChangeExtension($relativePath, '.pdf'))

        Write-Output $sourceFile

        if ($SupportedDocumentExtensions -contains ([System.IO.Path]::GetExtension($sourceFile).ToLowerInvariant())) {
            Convert-WordDocumentToPdf -WordApplication $wordApplication -SourcePath $sourceFile -PdfPath $targetPdfPath
        }
        else {
            Copy-PdfFile -SourcePath $sourceFile -PdfPath $targetPdfPath
        }

        $pdfFilesToMerge.Add($targetPdfPath)
    }

    $resolvedPdfMergeUtility = Resolve-PdfMergeUtility -UtilityPath $PdfMergeUtility
    Merge-PdfFiles -SourcePdfPaths $pdfFilesToMerge.ToArray() -OutputPdfPath $resolvedMergedPdf -UtilityPath $resolvedPdfMergeUtility
}
finally {
    if ($null -ne $wordApplication) {
        $wordApplication.Quit()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($wordApplication) | Out-Null
    }

    if ($null -ne $TemporaryExtractionRoot -and (Test-Path -LiteralPath $TemporaryExtractionRoot)) {
        Remove-Item -LiteralPath $TemporaryExtractionRoot -Recurse -Force
    }

    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}