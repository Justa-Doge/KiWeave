param([string]$SourceExe = (Join-Path $PSScriptRoot 'bin\FunctionRowRemapper.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $SourceExe -PathType Leaf)) { throw 'Build the application first.' }
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\FunctionRowRemapper'
$installedExe = Join-Path $installDir 'FunctionRowRemapper.exe'
$running = @(Get-Process -Name FunctionRowRemapper -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) { throw 'Exit every running copy of Function Row Remapper, then run installation again. Your saved settings will be preserved.' }
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
if (Test-Path -LiteralPath $installedExe) { Copy-Item -LiteralPath $installedExe -Destination (Join-Path $installDir 'FunctionRowRemapper.previous.exe') -Force }
Copy-Item -LiteralPath $SourceExe -Destination $installedExe -Force
if ((Get-FileHash -LiteralPath $SourceExe).Hash -ne (Get-FileHash -LiteralPath $installedExe).Hash) { throw 'Installed executable verification failed.' }
$programsDir = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programsDir 'Function Row Remapper.lnk'
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
} finally { if ($null -ne $shell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) } }
$runPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$entry = Get-ItemProperty -LiteralPath $runPath -ErrorAction SilentlyContinue
if ($null -ne $entry -and $entry.PSObject.Properties.Name -contains 'FunctionRowRemapper') {
    # Preserve an existing opt-in, updating only this application's value.
    Set-ItemProperty -LiteralPath $runPath -Name 'FunctionRowRemapper' -Value ('"' + $installedExe + '" --tray')
}
Write-Output "Installed: $installedExe"
Write-Output "Start menu shortcut: $shortcutPath"
Write-Output 'Search for Function Row Remapper. Existing mappings and preferences were preserved.'
