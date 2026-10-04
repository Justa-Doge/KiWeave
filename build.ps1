param([string]$OutputFolder = 'bin')
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework 4.x C# compiler is required.' }
$binDir = Join-Path $projectDir $OutputFolder
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
$iconPath = Join-Path $projectDir 'app.ico'
$iconGenerator = Join-Path $binDir 'CreateAppIcon.exe'
& $compiler /nologo /target:exe /r:System.Drawing.dll "/out:$iconGenerator" "$projectDir\tools\CreateAppIcon.cs"
if ($LASTEXITCODE -ne 0) { throw 'Icon generator build failed.' }
& $iconGenerator $projectDir
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
$uninstallerIconGenerator = Join-Path $binDir 'CreateUninstallerIcon.exe'
& $compiler /nologo /target:exe /r:System.Drawing.dll "/out:$uninstallerIconGenerator" "$projectDir\tools\CreateUninstallerIcon.cs"
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller icon generator build failed.' }
& $uninstallerIconGenerator $projectDir
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller icon generation failed.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectDir 'src') -Filter '*.cs' | ForEach-Object FullName)
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 "/win32manifest:$projectDir\app.manifest" "/win32icon:$iconPath" "/resource:$iconPath,KiWeave.AppIcon" "/out:$binDir\KiWeave.exe" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
$uninstallerReferences = @('/r:System.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 "/win32manifest:$projectDir\app.manifest" "/win32icon:$projectDir\uninstall.ico" "/out:$binDir\UninstallKiWeave.exe" $uninstallerReferences "$projectDir\tools\UninstallKiWeave.cs"
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
Copy-Item -LiteralPath (Join-Path $projectDir 'LICENSE') -Destination (Join-Path $binDir 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $projectDir 'NOTICE') -Destination (Join-Path $binDir 'NOTICE') -Force
$tests = Join-Path $projectDir 'tests\Tests.cs'
if (Test-Path -LiteralPath $tests) {
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /main:FunctionRowRemapper.Tests "/resource:$iconPath,KiWeave.AppIcon" "/out:$binDir\KiWeave.Tests.exe" $references $sources $tests
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & "$binDir\KiWeave.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Output "Built: $binDir\KiWeave.exe"
Write-Output "Built: $binDir\UninstallKiWeave.exe"
