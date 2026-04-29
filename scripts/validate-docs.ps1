[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path -LiteralPath $Root).Path
$RequiredDocs = @(
    "docs/requirements.md",
    "docs/technical.md",
    "docs/api.md",
    "docs/deployment.md",
    "docs/testing.md",
    "docs/user_guide.md",
    "docs/feature_guides.md",
    "docs/changelog.md",
    "docs/contributing.md",
    "docs/license.md",
    "docs/faq.md"
)

$Failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([string]$Message)
    [void]$script:Failures.Add($Message)
}

function Get-RepoRelativePath {
    param([string]$FullName)

    $basePath = $script:RepoRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)

    if ($FullName.StartsWith($basePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullName.Substring($basePath.Length).TrimStart(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar).Replace("\", "/")
    }

    return $FullName.Replace("\", "/")
}

function Test-IgnoredPath {
    param([string]$RelativePath)

    return $RelativePath -match '(^|/)(\.git|\.vs|bin|obj|node_modules)(/|$)'
}

function Get-MarkdownFiles {
    Get-ChildItem -LiteralPath $script:RepoRoot -Recurse -File -Filter "*.md" |
        Where-Object { -not (Test-IgnoredPath (Get-RepoRelativePath $_.FullName)) } |
        Sort-Object FullName
}

function Get-LinkTargetPath {
    param([string]$RawTarget)

    $target = $RawTarget.Trim()

    if ($target.StartsWith("<") -and $target.Contains(">")) {
        $target = $target.Substring(1, $target.IndexOf(">") - 1)
    }
    elseif ($target -match '^(?<path>\S+)\s+["'']') {
        $target = $Matches.path
    }

    return $target
}

function Test-ExternalOrAnchorTarget {
    param([string]$Target)

    return [string]::IsNullOrWhiteSpace($Target) -or
        $Target.StartsWith("#") -or
        ($Target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:')
}

foreach ($relativePath in $RequiredDocs) {
    $absolutePath = Join-Path $RepoRoot $relativePath

    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        Add-Failure "Required documentation file is missing: $relativePath"
        continue
    }

    $content = Get-Content -LiteralPath $absolutePath -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        Add-Failure "Required documentation file is empty: $relativePath"
        continue
    }

    $firstHeading = ($content -split "`r?`n" | Where-Object { $_ -match '^\s{0,3}#{1,6}\s+\S' } | Select-Object -First 1)
    if ($null -eq $firstHeading -or $firstHeading -notmatch '^\s{0,3}#\s+\S') {
        Add-Failure "Required documentation file must contain a level 1 heading: $relativePath"
    }
}

$markdownFiles = @(Get-MarkdownFiles)

if ($markdownFiles.Count -eq 0) {
    Add-Failure "No Markdown files were found."
}

foreach ($file in $markdownFiles) {
    $relativePath = Get-RepoRelativePath $file.FullName
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $fenceCount = 0

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $lineNumber = $index + 1
        $line = $lines[$index]

        if ($line -match '[ \t]+$') {
            Add-Failure "${relativePath}:$lineNumber has trailing whitespace."
        }

        if ($line -match '^\s{0,3}(```+|~~~+)') {
            $fenceCount++
        }

        foreach ($match in [regex]::Matches($line, '!\[[^\]]*\]\((?<target>[^)]+)\)|(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)')) {
            $target = Get-LinkTargetPath $match.Groups["target"].Value

            if (Test-ExternalOrAnchorTarget $target) {
                continue
            }

            $targetWithoutAnchor = ($target -split "#", 2)[0]
            if ([string]::IsNullOrWhiteSpace($targetWithoutAnchor)) {
                continue
            }

            $normalizedTarget = $targetWithoutAnchor.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
            $targetPath = Join-Path $file.DirectoryName $normalizedTarget

            if (-not (Test-Path -LiteralPath $targetPath)) {
                Add-Failure "${relativePath}:$lineNumber links to missing local target: $target"
            }
        }
    }

    if (($fenceCount % 2) -ne 0) {
        Add-Failure "$relativePath has an unclosed fenced code block."
    }
}

if ($Failures.Count -gt 0) {
    Write-Host "Documentation validation failed:" -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Documentation validation passed for $($markdownFiles.Count) Markdown files and $($RequiredDocs.Count) required docs."
