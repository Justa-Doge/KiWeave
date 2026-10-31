param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$BuildLabel
)

$ErrorActionPreference = 'Stop'
$expectedSetup = "KiWeave-Setup-beta5-$BuildLabel.exe"
$expectedPortable = "KiWeave-Portable-beta5-$BuildLabel.zip"
$expectedFiles = @($expectedSetup, $expectedPortable, 'SHA256SUMS.txt')
$packagePath = [System.IO.Path]::GetFullPath($PackageDirectory)
if (-not (Test-Path -LiteralPath $packagePath -PathType Container)) {
    throw 'Release artifact directory does not exist.'
}

$items = @(Get-ChildItem -LiteralPath $packagePath -Force)
if ($items.Count -ne $expectedFiles.Count -or @($items | Where-Object { $_.PSIsContainer -or $_.Name -notin $expectedFiles }).Count -gt 0) {
    throw 'Downloaded artifact contents do not exactly match the expected package set.'
}

$manifestPath = Join-Path $packagePath 'SHA256SUMS.txt'
$manifest = @(Get-Content -LiteralPath $manifestPath)
if ($manifest.Count -ne 2) {
    throw 'The checksum manifest must contain exactly the installer and portable archive.'
}

$seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($line in $manifest) {
    if ($line -notmatch '^(?<hash>[0-9a-fA-F]{64})\s+(?<name>.+)$') {
        throw 'The checksum manifest contains a malformed entry.'
    }
    $hash = $Matches.hash
    $name = $Matches.name.Trim()
    if ($name -notin @($expectedSetup, $expectedPortable) -or -not $seen.Add($name)) {
        throw 'The checksum manifest contains a duplicate or unexpected filename.'
    }
    $filePath = Join-Path $packagePath $name
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Checksum target is missing: $name"
    }
    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
    if ($actualHash -ine $hash) {
        throw "Checksum mismatch for $name."
    }
}

if ($seen.Count -ne 2 -or -not $seen.Contains($expectedSetup) -or -not $seen.Contains($expectedPortable)) {
    throw 'The checksum manifest must cover both the installer and portable archive.'
}

Write-Output 'Release artifact files and SHA-256 checksums verified.'

