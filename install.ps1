param([string]$SourceExe = (Join-Path $PSScriptRoot 'bin\KiWeave.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $SourceExe -PathType Leaf)) { throw 'Build the application first.' }
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\KiWeave'
$installedExe = Join-Path $installDir 'KiWeave.exe'
$legacyInstallDir = Join-Path $env:LOCALAPPDATA 'Programs\FunctionRowRemapper'
$legacyInstalledExe = Join-Path $legacyInstallDir 'FunctionRowRemapper.exe'
$running = @(@(Get-Process -Name KiWeave -ErrorAction SilentlyContinue) + @(Get-Process -Name FunctionRowRemapper -ErrorAction SilentlyContinue))
if ($running.Count -gt 0) { throw 'Exit every running copy of KiWeave, then run installation again. Your saved settings will be preserved.' }
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
if (Test-Path -LiteralPath $installedExe) { Copy-Item -LiteralPath $installedExe -Destination (Join-Path $installDir 'KiWeave.previous.exe') -Force }
elseif (Test-Path -LiteralPath $legacyInstalledExe) { Copy-Item -LiteralPath $legacyInstalledExe -Destination (Join-Path $installDir 'FunctionRowRemapper.previous.exe') -Force }
Copy-Item -LiteralPath $SourceExe -Destination $installedExe -Force
if ((Get-FileHash -LiteralPath $SourceExe).Hash -ne (Get-FileHash -LiteralPath $installedExe).Hash) { throw 'Installed executable verification failed.' }
$sourceDir = Split-Path -Parent $SourceExe
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
Write-Output 'Search for KiWeave. Existing mappings and preferences were migrated and preserved.'
