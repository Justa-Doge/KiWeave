param(
    [string]$SourceExe = (Join-Path $PSScriptRoot 'bin\KiWeave.exe'),
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
function Assert-KiWeavePublishCopyPaths([string]$SourceDirectory, [string]$DestinationDirectory) {
    $sourceRoot = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    $destinationRoot = [IO.Path]::GetFullPath($DestinationDirectory).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    if ($destinationRoot.StartsWith($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or $sourceRoot.StartsWith($destinationRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The published build folder and install folder cannot contain one another.'
    }
}
function Copy-KiWeavePublishedFiles([string]$SourceDirectory, [string]$DestinationDirectory) {
    $sourceRoot = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    $destinationRoot = [IO.Path]::GetFullPath($DestinationDirectory).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "Published build folder was not found: $SourceDirectory" }
    Assert-KiWeavePublishCopyPaths -SourceDirectory $sourceRoot -DestinationDirectory $destinationRoot
    if (Test-Path -LiteralPath $destinationRoot -PathType Container) {
        if (([IO.File]::GetAttributes($destinationRoot) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Install folder is a reparse point: $destinationRoot" }
    }
    Get-ChildItem -LiteralPath $sourceRoot -File -Recurse -Force | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 } | ForEach-Object {
        $relativePath = $_.FullName.Substring($sourceRoot.Length)
        if ($relativePath -notin @('KiWeave.exe', 'UninstallKiWeave.exe', 'KiWeave.Tests.exe', 'CreateAppIcon.exe', 'CreateUninstallerIcon.exe')) {
            $destination = Join-Path $destinationRoot $relativePath
            $destinationFolder = [IO.Path]::GetFullPath((Split-Path -Parent $destination))
            $rootWithoutSeparator = $destinationRoot.TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
            $folderToCheck = $destinationFolder
            while ($folderToCheck -and ($folderToCheck.Equals($rootWithoutSeparator, [StringComparison]::OrdinalIgnoreCase) -or $folderToCheck.StartsWith($destinationRoot, [StringComparison]::OrdinalIgnoreCase))) {
                if (Test-Path -LiteralPath $folderToCheck) {
                    $folderAttributes = [IO.File]::GetAttributes($folderToCheck)
                    if (($folderAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Installed publish path contains a reparse point: $folderToCheck" }
                }
                if ($folderToCheck.Equals($rootWithoutSeparator, [StringComparison]::OrdinalIgnoreCase)) { break }
                $folderToCheck = Split-Path -Parent $folderToCheck
            }
            if (-not (Test-Path -LiteralPath $destinationFolder -PathType Container)) { New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null }
            if (Test-Path -LiteralPath $destination) {
                $destinationAttributes = [IO.File]::GetAttributes($destination)
                if (($destinationAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Installed publish file is a reparse point: $destination" }
            }
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
            if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Installed publish file verification failed: $relativePath" }
        }
    }
}
$defaultSourceExe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin\KiWeave.exe'))
$sourceDir = Split-Path -Parent $SourceExe
$sourceUninstaller = Join-Path $sourceDir 'UninstallKiWeave.exe'
if (-not (Test-Path -LiteralPath $SourceExe -PathType Leaf) -or -not (Test-Path -LiteralPath $sourceUninstaller -PathType Leaf)) {
    if ([IO.Path]::GetFullPath($SourceExe) -eq $defaultSourceExe) {
        Write-Output 'Build output was not found. Building KiWeave and its uninstaller now...'
        & (Join-Path $PSScriptRoot 'build.ps1') -OutputFolder 'bin' -DotnetPath $DotnetPath
        if ($LASTEXITCODE -ne 0) { throw 'The automatic build failed.' }
        $sourceDir = Split-Path -Parent $SourceExe
        $sourceUninstaller = Join-Path $sourceDir 'UninstallKiWeave.exe'
    } else { throw "The selected application build or uninstaller was not found: $SourceExe" }
}
if (-not (Test-Path -LiteralPath $sourceUninstaller -PathType Leaf)) { throw 'The build is missing UninstallKiWeave.exe. Rebuild the application first.' }
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\KiWeave'
$installedExe = Join-Path $installDir 'KiWeave.exe'
$installedUninstaller = Join-Path $installDir 'UninstallKiWeave.exe'
Assert-KiWeavePublishCopyPaths -SourceDirectory $sourceDir -DestinationDirectory $installDir
if (Test-Path -LiteralPath $installDir -PathType Container) {
    if (([IO.File]::GetAttributes($installDir) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Install folder is a reparse point; refusing to write through it: $installDir" }
}
$legacyInstallDir = Join-Path $env:LOCALAPPDATA 'Programs\FunctionRowRemapper'
$legacyInstalledExe = Join-Path $legacyInstallDir 'FunctionRowRemapper.exe'
function Invoke-KiWeaveChecker([string]$argument) {
    $stdout = [IO.Path]::GetTempFileName()
    $stderr = [IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $SourceExe -ArgumentList $argument -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        [PSCustomObject]@{
            ExitCode = $process.ExitCode
            Output = if (Test-Path -LiteralPath $stdout) { Get-Content -Raw -LiteralPath $stdout } else { '' }
            Error = if (Test-Path -LiteralPath $stderr) { Get-Content -Raw -LiteralPath $stderr } else { '' }
        }
    } finally {
        Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}
function Get-KiWeaveCheckerErrorText([object]$State) {
    if ($null -eq $State -or $null -eq $State.Error) { return '' }
    return ([string]$State.Error).Trim()
}
function Get-KiWeaveStateText {
    $processes = @(@(Get-Process -Name KiWeave -ErrorAction SilentlyContinue) + @(Get-Process -Name FunctionRowRemapper -ErrorAction SilentlyContinue))
    if ($processes.Count -eq 0) { return 'KiWeave is not running.' }
    if (-not ('KiWeaveInstaller.NativeWindow' -as [type])) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
namespace KiWeaveInstaller { public static class NativeWindow {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
} }
"@
    }
    $foreground = [KiWeaveInstaller.NativeWindow]::GetForegroundWindow(); [uint32]$foregroundPid = 0; [KiWeaveInstaller.NativeWindow]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid) | Out-Null
    $frontmost = $false
    foreach ($process in $processes) {
        if ($process.Id -eq $foregroundPid) { $frontmost = $true }
    }
    if ($frontmost) { return 'KiWeave is open in the foreground; its private update state is unavailable.' }
    return 'KiWeave is open in the background; its private update state is unavailable.'
}
$running = @(@(Get-Process -Name KiWeave -ErrorAction SilentlyContinue) + @(Get-Process -Name FunctionRowRemapper -ErrorAction SilentlyContinue))
if ($running.Count -gt 0) {
    $localCheckerMarker = Join-Path $sourceDir 'KiWeave.LocalOnly'
    if (-not (Test-Path -LiteralPath $localCheckerMarker -PathType Leaf)) { throw 'KiWeave is running. Exit the visible app or tray copy before installing a public build.' }
    $state = Invoke-KiWeaveChecker '--inspect-state'
    if ($state.ExitCode -ne 0) {
        $stateText = ([string]$state.Output).Trim()
        if ([String]::IsNullOrWhiteSpace($stateText)) { $stateText = Get-KiWeaveStateText }
        $stateError = Get-KiWeaveCheckerErrorText $state
        throw ($stateText + ' Update was not applied. ' + $stateError)
    }
    $close = Invoke-KiWeaveChecker '--exit-for-update'
    if ($close.ExitCode -ne 0) { $closeText = [string]$close.Output; $closeError = [string]$close.Error; throw ('KiWeave did not close cleanly for the update. ' + $closeText + ' ' + $closeError) }
    Start-Sleep -Milliseconds 250
    if (@(Get-Process -Name KiWeave -ErrorAction SilentlyContinue).Count -gt 0) { throw ((Get-KiWeaveStateText) + ' It remained running after the clean-close request.') }
}
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
if (Test-Path -LiteralPath $installedExe) { Copy-Item -LiteralPath $installedExe -Destination (Join-Path $installDir 'KiWeave.previous.exe') -Force }
elseif (Test-Path -LiteralPath $legacyInstalledExe) { Copy-Item -LiteralPath $legacyInstalledExe -Destination (Join-Path $installDir 'FunctionRowRemapper.previous.exe') -Force }
Copy-Item -LiteralPath $SourceExe -Destination $installedExe -Force
if ((Get-FileHash -LiteralPath $SourceExe).Hash -ne (Get-FileHash -LiteralPath $installedExe).Hash) { throw 'Installed executable verification failed.' }
$companionFiles = @('KiWeave.dll', 'KiWeave.Wpf.dll', 'KiWeave.deps.json', 'KiWeave.runtimeconfig.json', 'KiWeave.Runtime.dll')
foreach ($companion in $companionFiles) {
    $sourceCompanion = Join-Path $sourceDir $companion
    if (Test-Path -LiteralPath $sourceCompanion -PathType Leaf) {
        Copy-Item -LiteralPath $sourceCompanion -Destination (Join-Path $installDir $companion) -Force
        if ((Get-FileHash -LiteralPath $sourceCompanion).Hash -ne (Get-FileHash -LiteralPath (Join-Path $installDir $companion)).Hash) { throw "Installed companion verification failed: $companion" }
    }
}
Copy-KiWeavePublishedFiles -SourceDirectory $sourceDir -DestinationDirectory $installDir
Copy-Item -LiteralPath $sourceUninstaller -Destination $installedUninstaller -Force
if ((Get-FileHash -LiteralPath $sourceUninstaller).Hash -ne (Get-FileHash -LiteralPath $installedUninstaller).Hash) { throw 'Installed uninstaller verification failed.' }
foreach ($name in @('LICENSE', 'NOTICE')) {
    $sourceDocument = Join-Path $sourceDir $name
    if (-not (Test-Path -LiteralPath $sourceDocument -PathType Leaf)) { throw "The build is missing $name." }
    Copy-Item -LiteralPath $sourceDocument -Destination (Join-Path $installDir $name) -Force
}
$programsDir = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programsDir 'KiWeave.lnk'
$legacyShortcutPath = Join-Path $programsDir 'Function Row Remapper.lnk'
$safeShortcutPath = Join-Path $programsDir 'KiWeave Safe Mode.lnk'
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Arguments = ''
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Description = 'Customize function keys, media controls and DDC/CI monitor controls'
    $shortcut.Save()
    $verify = $shell.CreateShortcut($shortcutPath)
    if ($verify.TargetPath -ne $installedExe -or $verify.Arguments -ne '') { throw 'Start menu shortcut verification failed.' }
    $safeShortcut = $shell.CreateShortcut($safeShortcutPath)
    $safeShortcut.TargetPath = $installedExe
    $safeShortcut.WorkingDirectory = $installDir
    $safeShortcut.Arguments = '--safe-mode'
    $safeShortcut.IconLocation = "$installedExe,0"
    $safeShortcut.Description = 'Open KiWeave recovery tools without hooks, hotkeys, integrations, or network access'
    $safeShortcut.Save()
    $safeVerify = $shell.CreateShortcut($safeShortcutPath)
    if ($safeVerify.TargetPath -ne $installedExe -or $safeVerify.Arguments -ne '--safe-mode') { throw 'Safe Mode shortcut verification failed.' }
} finally { if ($null -ne $shell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) } }
if (Test-Path -LiteralPath $legacyShortcutPath) { Remove-Item -LiteralPath $legacyShortcutPath -Force }
$oldUninstallShortcutPath = Join-Path $programsDir 'KiWeave Uninstall.lnk'
if (Test-Path -LiteralPath $oldUninstallShortcutPath) { Remove-Item -LiteralPath $oldUninstallShortcutPath -Force }
$runPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$entry = Get-ItemProperty -LiteralPath $runPath -ErrorAction SilentlyContinue
if ($null -ne $entry -and (($entry.PSObject.Properties.Name -contains 'FunctionRowRemapper') -or ($entry.PSObject.Properties.Name -contains 'KiWeave'))) {
    # Preserve an existing opt-in while migrating this application's value to its real name.
    Set-ItemProperty -LiteralPath $runPath -Name 'KiWeave' -Value ('"' + $installedExe + '" --tray')
    Remove-ItemProperty -LiteralPath $runPath -Name 'FunctionRowRemapper' -ErrorAction SilentlyContinue
}
Write-Output "Installed: $installedExe"
Write-Output "Start menu shortcut: $shortcutPath"
Write-Output "Safe Mode shortcut: $safeShortcutPath"
Write-Output "Uninstaller: $installedUninstaller (kept out of Start menu search)"
Write-Output 'Search for KiWeave. Existing mappings and preferences were migrated and preserved.'
