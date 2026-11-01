[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BuildFolder,
    [Parameter(Mandatory = $true)][string]$BuildLabel,
    [Parameter(Mandatory = $true)][string]$InnoCompilerPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildPath = (Resolve-Path (Join-Path $repoRoot $BuildFolder)).Path
if ($BuildLabel -notmatch '^[A-Za-z0-9][A-Za-z0-9-]{0,39}$') { throw 'BuildLabel may contain only letters, numbers, and hyphens, and must be at most 40 characters.' }
if (-not (Test-Path -LiteralPath $InnoCompilerPath -PathType Leaf)) { throw "The Inno Setup compiler was not found: $InnoCompilerPath" }

$versionSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\KiWeaveBuildIdentity.cs') -Raw
if ($versionSource -notmatch 'CurrentVersion\s*=\s*"(?<version>[^"]+)"') { throw 'Could not read the KiWeave product version.' }
$version = $Matches.version
if ($version -notmatch '^1\.0\.0-beta\.5(?:[-+].*)?$') { throw "Expected the beta.5 product version, found $version." }

$artifactRoot = Join-Path $repoRoot ('release\package-beta5-' + $BuildLabel)
if (Test-Path -LiteralPath $artifactRoot) { throw "Refusing to overwrite existing package output: $artifactRoot" }
$stage = Join-Path $artifactRoot 'staging'
$setupBaseName = 'KiWeave-Setup-beta5-' + $BuildLabel
$portablePath = Join-Path $artifactRoot ('KiWeave-Portable-beta5-' + $BuildLabel + '.zip')
$setupPath = Join-Path $artifactRoot ($setupBaseName + '.exe')
New-Item -ItemType Directory -Path $stage | Out-Null

$buildHelpers = @('CreateAppIcon.exe', 'CreateUninstallerIcon.exe', 'KiWeave.Tests.exe', 'LICENSE', 'NOTICE')
$buildFiles = @(Get-ChildItem -LiteralPath $buildPath -File -Recurse | Where-Object { $_.Name -notin $buildHelpers })
if (-not ($buildFiles | Where-Object Name -ceq 'KiWeave.exe') -or
    -not ($buildFiles | Where-Object Name -ceq 'KiWeave.dll') -or
    -not ($buildFiles | Where-Object Name -ceq 'KiWeave.Runtime.dll') -or
    -not ($buildFiles | Where-Object Name -ceq 'KiWeave.runtimeconfig.json') -or
    -not ($buildFiles | Where-Object Name -ceq 'UninstallKiWeave.exe')) {
    throw 'The WPF build output is missing the app, runtime, runtime configuration, or uninstaller.'
}
foreach ($file in $buildFiles) {
    if ($file.Extension -in '.cs', '.xaml', '.ps1', '.pdb') { throw "Refusing to package source or debug symbols: $($file.FullName)" }
    $relative = $file.FullName.Substring($buildPath.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $destination = Join-Path $stage $relative
    $parent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
Copy-Item -LiteralPath @(
    (Join-Path $repoRoot 'LICENSE'),
    (Join-Path $repoRoot 'NOTICE'),
    (Join-Path $repoRoot 'README.md'),
    (Join-Path $repoRoot 'CHANGELOG.md')
) -Destination $stage -Force

$runtimeConfigPath = Join-Path $stage 'KiWeave.runtimeconfig.json'
$runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
$frameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
if (-not ($frameworks | Where-Object name -eq 'Microsoft.NETCore.App') -or
    -not ($frameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App')) {
    throw 'The WPF publish must bundle both .NET and Windows Desktop runtimes.'
}

$emptyRuntimeRoot = Join-Path $artifactRoot 'empty-runtime-root'
New-Item -ItemType Directory -Path $emptyRuntimeRoot | Out-Null
$savedRoots = @{ DOTNET_ROOT = $env:DOTNET_ROOT; DOTNET_ROOT_X64 = $env:DOTNET_ROOT_X64; DOTNET_MULTILEVEL_LOOKUP = $env:DOTNET_MULTILEVEL_LOOKUP }
try {
    $env:DOTNET_ROOT = $emptyRuntimeRoot
    $env:DOTNET_ROOT_X64 = $emptyRuntimeRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $beforeTest = Get-Date
    $app = Start-Process -FilePath (Join-Path $stage 'KiWeave.exe') -ArgumentList '--self-test','--no-tray','--no-engine' -Wait -PassThru
    $report = Get-ChildItem $env:TEMP -Filter 'KiWeave-wpf-self-test-*.txt' |
        Where-Object LastWriteTime -ge $beforeTest | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($app.ExitCode -ne 0 -or -not $report -or (Get-Content -LiteralPath $report.FullName -Raw) -notmatch '^WPF data smoke passed:') {
        throw "The isolated WPF package self-test failed (exit $($app.ExitCode))."
    }
}
finally {
    foreach ($name in $savedRoots.Keys) {
        if ($null -eq $savedRoots[$name]) { Remove-Item -LiteralPath ("Env:" + $name) -ErrorAction SilentlyContinue }
        else { Set-Item -Path ("Env:" + $name) -Value $savedRoots[$name] }
    }
}

$appVersion = (Get-Item -LiteralPath (Join-Path $stage 'KiWeave.exe')).VersionInfo.ProductVersion
if (-not $appVersion.StartsWith($version, [StringComparison]::OrdinalIgnoreCase)) { throw "Packaged product version mismatch: $appVersion" }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $portablePath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
Import-Module (Join-Path $PSScriptRoot 'PortableArchivePolicy.psm1') -Force
$archive = [IO.Compression.ZipFile]::OpenRead($portablePath)
try {
    $allEntries = @($archive.Entries)
    $entries = @($allEntries | Where-Object { -not [String]::IsNullOrEmpty($_.Name) })
    $entryNames = @($entries | ForEach-Object FullName)
    Assert-KiWeavePortableArchivePaths -Paths @($allEntries | ForEach-Object FullName)
    $unpackedBytes = ($entries | Measure-Object Length -Sum).Sum
    if ($entries.Count -gt 1000 -or $unpackedBytes -gt 1GB) { throw 'Portable ZIP exceeds the file-count or expanded-size limit.' }
    foreach ($entryName in $entryNames) {
        if ([IO.Path]::GetExtension($entryName) -in '.cs', '.xaml', '.ps1', '.pdb') { throw "Portable ZIP contains source or debug symbols: $entryName" }
    }
    foreach ($required in @('KiWeave.exe','KiWeave.dll','KiWeave.Runtime.dll','KiWeave.runtimeconfig.json','UninstallKiWeave.exe','LICENSE','NOTICE','README.md','CHANGELOG.md')) {
        if ($entryNames -notcontains $required) { throw "Portable ZIP is missing required file $required." }
    }
    $stagedFiles = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
    if ($entries.Count -ne $stagedFiles.Count) { throw 'Portable ZIP file count does not match the staged app.' }
    foreach ($entry in $entries) {
        $stagedPath = Join-Path $stage ($entry.FullName.Replace('/', '\\'))
        if (-not (Test-Path -LiteralPath $stagedPath -PathType Leaf)) { throw "Portable ZIP contains an unexpected file: $($entry.FullName)" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $archiveHash = [Convert]::ToHexString($sha.ComputeHash($stream)) }
        finally { $stream.Dispose(); $sha.Dispose() }
        $stagedHash = (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash
        if ($archiveHash -cne $stagedHash) { throw "Portable ZIP file hash differs from staging: $($entry.FullName)" }
    }
}
finally { $archive.Dispose() }

$portableRoot = Join-Path $artifactRoot 'portable-check'
[IO.Compression.ZipFile]::ExtractToDirectory($portablePath, $portableRoot)
$portableVersion = (Get-Item -LiteralPath (Join-Path $portableRoot 'KiWeave.exe')).VersionInfo.ProductVersion
if (-not $portableVersion.StartsWith($version, [StringComparison]::OrdinalIgnoreCase)) { throw "Portable app product version mismatch: $portableVersion" }

& $InnoCompilerPath "/DMyAppVersion=$version" "/DMyAppSourceDir=$stage" "/O$artifactRoot" "/F$setupBaseName" (Join-Path $repoRoot 'installer\KeyWeave.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$setupVersion = (Get-Item -LiteralPath $setupPath).VersionInfo.ProductVersion
if (-not $setupVersion.StartsWith($version, [StringComparison]::OrdinalIgnoreCase)) { throw "Installer product version mismatch: $setupVersion" }

@($setupPath, $portablePath) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_)
} | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding ascii

Write-Output "Verified beta.5 installer and portable package: $artifactRoot"
Write-Output "App self-test: $($report.FullName)"
Write-Output "Portable files: $($entries.Count); checksums: $artifactRoot\SHA256SUMS.txt"
