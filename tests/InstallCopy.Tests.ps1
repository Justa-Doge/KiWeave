$ErrorActionPreference = 'Stop'
$innoScript = Join-Path $PSScriptRoot '..\installer\KeyWeave.iss'
$innoText = Get-Content -LiteralPath $innoScript -Raw
foreach ($requiredGuard in @('function PrepareToInstall', 'CheckForMutexes(Mutexes)', '--inspect-state', '--exit-for-update', 'KiWeaveCheckerError')) {
    if (-not $innoText.Contains($requiredGuard)) { throw "Inno Setup is missing its safe KiWeave update guard: $requiredGuard" }
}
if ($innoText -notmatch '(?m)^CloseApplications=yes\s*$' -or $innoText -notmatch '(?m)^RestartApplications=no\s*$') {
    throw 'Inno Setup must use the KiWeave checker before permitting Restart Manager to close it, and must not restart the old executable after replacement.'
}
if ($innoText -notmatch '(?m)^#ifndef MyAppSourceDir\s*\r?\n\s*#define MyAppSourceDir "\.\.\\release\\staging"\s*\r?\n#endif\s*$' -or
    $innoText -notmatch '(?m)^Source: "\{#MyAppSourceDir\}\\\*"; DestDir: "\{app\}"; Flags: ignoreversion recursesubdirs createallsubdirs\s*$') {
    throw 'Inno Setup must preserve the default release staging path while allowing isolated validation builds to supply a separate source folder.'
}
$installerScript = Join-Path $PSScriptRoot '..\install.ps1'
$tokens = $null
$parseErrors = $null
$installerAst = [System.Management.Automation.Language.Parser]::ParseFile($installerScript, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "install.ps1 does not parse: $($parseErrors[0].Message)" }
$installerText = Get-Content -LiteralPath $installerScript -Raw
$stateTextFunction = $installerAst.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-KiWeaveStateText' }, $true)
if (-not $stateTextFunction -or $stateTextFunction.Extent.Text -match 'MainWindowTitle|\$dirty') {
    throw 'The installer must not infer unsaved state from a window-title asterisk.'
}
if ($installerText -notmatch '\$stateText\s*=\s*\(\[string\]\$state\.Output\)\.Trim\(\)\s*if\s*\(\[String\]::IsNullOrWhiteSpace\(\$stateText\)\)\s*\{\s*\$stateText\s*=\s*Get-KiWeaveStateText\s*\}') {
    throw 'Install refusal diagnostics must prefer the authoritative private checker result.'
}
$checkerErrorFunction = $installerAst.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-KiWeaveCheckerErrorText' }, $true)
if (-not $checkerErrorFunction) { throw 'install.ps1 is missing its null-safe checker error formatter.' }
. ([scriptblock]::Create($checkerErrorFunction.Extent.Text))
if ((Get-KiWeaveCheckerErrorText ([pscustomobject]@{ Error = $null })) -ne '') { throw 'A missing checker stderr value must produce an empty diagnostic.' }
if ((Get-KiWeaveCheckerErrorText ([pscustomobject]@{ Error = "  checker failed  `r`n" })) -ne 'checker failed') { throw 'Checker stderr must be trimmed without changing its content.' }
$pathGuard = $installerAst.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-KiWeavePublishCopyPaths' }, $true)
if (-not $pathGuard) { throw 'install.ps1 is missing Assert-KiWeavePublishCopyPaths.' }
$installLines = Get-Content -LiteralPath $installerScript
$preflightLine = ($installLines | Select-String -SimpleMatch 'Assert-KiWeavePublishCopyPaths -SourceDirectory $sourceDir -DestinationDirectory $installDir' | Select-Object -First 1).LineNumber
$executableCopyLine = ($installLines | Select-String -SimpleMatch 'Copy-Item -LiteralPath $SourceExe -Destination $installedExe' | Select-Object -First 1).LineNumber
if (-not $preflightLine -or -not $executableCopyLine -or $preflightLine -ge $executableCopyLine) { throw 'Install source/destination validation must run before replacing the executable.' }
$copyHelper = $installerAst.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Copy-KiWeavePublishedFiles' }, $true)
if (-not $copyHelper) { throw 'install.ps1 is missing Copy-KiWeavePublishedFiles.' }
. ([scriptblock]::Create($pathGuard.Extent.Text))
. ([scriptblock]::Create($copyHelper.Extent.Text))

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
$testRoot = Join-Path $tempRoot ('KiWeave-install-copy-test-' + [guid]::NewGuid().ToString('N'))
if (-not $testRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installer copy test folder escaped the temp directory.' }
$source = Join-Path $testRoot 'published'
$destination = Join-Path $testRoot 'installed'
$junctionDestination = Join-Path $testRoot 'junction-installed'
$rootJunctionDestination = Join-Path $testRoot 'root-junction-installed'
$outside = Join-Path $testRoot 'outside'
$junctionPath = Join-Path $junctionDestination 'linked'
$rootJunctionPath = Join-Path $testRoot 'root-junction'
$encoding = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Path (Join-Path $source 'fr'), (Join-Path $source 'overlay-icons'), $destination -Force | Out-Null
try {
    [IO.File]::WriteAllText((Join-Path $source 'KiWeave.exe'), 'apphost', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'UninstallKiWeave.exe'), 'uninstaller', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'KiWeave.Tests.exe'), 'test harness', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'CreateAppIcon.exe'), 'build helper', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'CreateUninstallerIcon.exe'), 'build helper', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'KiWeave.runtimeconfig.json'), 'runtime', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'fr\KiWeave.resources.dll'), 'satellite resources', $encoding)
    [IO.File]::WriteAllText((Join-Path $source 'overlay-icons\Discord.ico'), 'overlay icon', $encoding)
    New-Item -ItemType Directory -Path (Join-Path $source 'linked'), $outside -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'linked\must-not-escape.txt'), 'outside write sentinel', $encoding)
    New-Item -ItemType Directory -Path (Join-Path $destination 'fr') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $destination 'fr\KiWeave.resources.dll'), 'stale copy', $encoding)

    Copy-KiWeavePublishedFiles -SourceDirectory $source -DestinationDirectory $destination
    foreach ($relativePath in @('KiWeave.runtimeconfig.json', 'fr\KiWeave.resources.dll', 'overlay-icons\Discord.ico')) {
        $from = Join-Path $source $relativePath
        $to = Join-Path $destination $relativePath
        if (-not (Test-Path -LiteralPath $to -PathType Leaf)) { throw "Published file was omitted: $relativePath" }
        if ((Get-FileHash -LiteralPath $from).Hash -ne (Get-FileHash -LiteralPath $to).Hash) { throw "Published file changed during copy: $relativePath" }
    }
    if ((Test-Path -LiteralPath (Join-Path $destination 'KiWeave.exe')) -or (Test-Path -LiteralPath (Join-Path $destination 'UninstallKiWeave.exe'))) {
        throw 'The apphost and uninstaller should remain on their separate hash-verified copy paths.'
    }
    foreach ($buildOnlyFile in @('KiWeave.Tests.exe', 'CreateAppIcon.exe', 'CreateUninstallerIcon.exe')) {
        if (Test-Path -LiteralPath (Join-Path $destination $buildOnlyFile)) { throw "A build-only tool was copied into the install folder: $buildOnlyFile" }
    }

    $nestedRejected = $false
    try { Copy-KiWeavePublishedFiles -SourceDirectory $source -DestinationDirectory (Join-Path $source 'child-install') }
    catch { $nestedRejected = $true }
    if (-not $nestedRejected) { throw 'Copying an install folder inside its own source was not rejected.' }
    $overlapRejected = $false
    try { Assert-KiWeavePublishCopyPaths -SourceDirectory $source -DestinationDirectory $testRoot }
    catch { $overlapRejected = $true }
    if (-not $overlapRejected) { throw 'A publish source nested within its install destination was not rejected.' }

    New-Item -ItemType Directory -Path $junctionDestination -Force | Out-Null
    New-Item -ItemType Junction -Path $junctionPath -Target $outside | Out-Null
    $junctionRejected = $false
    try { Copy-KiWeavePublishedFiles -SourceDirectory $source -DestinationDirectory $junctionDestination }
    catch { $junctionRejected = $_.Exception.Message -match 'reparse point' }
    if (-not $junctionRejected) { throw 'A junction inside the install folder was followed instead of rejected.' }
    if (Test-Path -LiteralPath (Join-Path $outside 'must-not-escape.txt')) { throw 'Installer copy wrote a publish file outside the destination through a junction.' }
    [IO.Directory]::Delete($junctionPath)

    New-Item -ItemType Junction -Path $rootJunctionPath -Target $outside | Out-Null
    $rootJunctionRejected = $false
    try { Copy-KiWeavePublishedFiles -SourceDirectory $source -DestinationDirectory $rootJunctionPath }
    catch { $rootJunctionRejected = $_.Exception.Message -match 'reparse point' }
    if (-not $rootJunctionRejected) { throw 'A junction used as the install root was not rejected.' }
    if (Test-Path -LiteralPath (Join-Path $outside 'KiWeave.runtimeconfig.json')) { throw 'Installer copy wrote a root file outside the destination through a junction.' }
    [IO.Directory]::Delete($rootJunctionPath)
    Write-Output 'PASS installer preserves nested publish resources, rejects path escapes, and validates the Inno Setup clean-exit guard'
}
finally {
    if (Test-Path -LiteralPath $junctionPath) {
        $junctionAttributes = [IO.File]::GetAttributes($junctionPath)
        if (($junctionAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { [IO.Directory]::Delete($junctionPath) }
    }
    if (Test-Path -LiteralPath $rootJunctionPath) {
        $rootAttributes = [IO.File]::GetAttributes($rootJunctionPath)
        if (($rootAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { [IO.Directory]::Delete($rootJunctionPath) }
    }
    if (Test-Path -LiteralPath $testRoot -PathType Container) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
