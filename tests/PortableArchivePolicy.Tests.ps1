$ErrorActionPreference = 'Stop'
$modulePath = Join-Path $PSScriptRoot '..\tools\PortableArchivePolicy.psm1'
Import-Module -Name $modulePath -Force

$workflowPath = Join-Path $PSScriptRoot '..\.github\workflows\release.yml'
$packageScriptPath = Join-Path $PSScriptRoot '..\tools\PackageBeta5.ps1'
$packageTokens = $null
$packageParseErrors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile($packageScriptPath, [ref]$packageTokens, [ref]$packageParseErrors)
if ($packageParseErrors.Count -gt 0) { throw "Beta 5 packaging script has a syntax error: $($packageParseErrors[0].Message)" }
$workflowLines = Get-Content -LiteralPath $workflowPath
$parsedRunBlocks = 0
for ($lineIndex = 0; $lineIndex -lt $workflowLines.Count; $lineIndex++) {
    if ($workflowLines[$lineIndex] -notmatch '^(?<indent> *)run:\s*\|\s*$') { continue }
    $indent = $Matches.indent.Length
    $bodyIndent = $indent + 2
    $scriptLines = [System.Collections.Generic.List[string]]::new()
    for ($bodyIndex = $lineIndex + 1; $bodyIndex -lt $workflowLines.Count; $bodyIndex++) {
        $line = $workflowLines[$bodyIndex]
        if ([String]::IsNullOrWhiteSpace($line)) { $scriptLines.Add(''); continue }
        $leadingSpaces = $line.Length - $line.TrimStart(' ').Length
        if ($leadingSpaces -le $indent) { break }
        if ($line.Length -lt $bodyIndent) { throw "Release workflow run block has invalid indentation at line $($bodyIndex + 1)." }
        $scriptLines.Add($line.Substring($bodyIndent))
    }
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput(($scriptLines -join "`n"), [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) { throw "Release workflow PowerShell block has a syntax error: $($parseErrors[0].Message)" }
    $parsedRunBlocks++
}
if ($parsedRunBlocks -lt 2) { throw 'Expected to validate the release workflow PowerShell here-string blocks.' }

Assert-KiWeavePortableArchivePaths -Paths @(
    'KiWeave.exe',
    'fr/KiWeave.resources.dll',
    'runtimes/win-x64/native/hostfxr.dll',
    'empty-folder/'
)

foreach ($unsafePath in @(
    '../outside.txt',
    '..\outside.txt',
    'folder/../../outside.txt',
    '/rooted/file.txt',
    '\rooted\file.txt',
    'C:/outside.txt',
    'C:\outside.txt',
    '\\server\share\outside.txt',
    'NUL.txt',
    'aux/',
    'file:alternate-stream',
    'trailing-dot.',
    'trailing-space ',
    'double//separator'
)) {
    $rejected = $false
    try { Assert-KiWeavePortableArchivePaths -Paths @($unsafePath) }
    catch { $rejected = $_.Exception.Message -match 'unsafe path|Windows-incompatible path|Windows device path' }
    if (-not $rejected) { throw "Unsafe portable archive path was accepted: $unsafePath" }
}

$emptyRejected = $false
try { Assert-KiWeavePortableArchivePaths -Paths @('') }
catch { $emptyRejected = $_.Exception.Message -match 'empty entry path' }
if (-not $emptyRejected) { throw 'An empty portable archive path was accepted.' }

$collisionRejected = $false
try { Assert-KiWeavePortableArchivePaths -Paths @('folder/file.txt', 'FOLDER/FILE.TXT') }
catch { $collisionRejected = $_.Exception.Message -match 'collide on Windows' }
if (-not $collisionRejected) { throw 'Paths that collide on a case-insensitive Windows filesystem were accepted.' }
$composedName = 'folder/caf' + [char]0x00e9 + '.txt'
$decomposedName = 'folder/cafe' + [char]0x0301 + '.txt'
$unicodeCollisionRejected = $false
try { Assert-KiWeavePortableArchivePaths -Paths @($composedName, $decomposedName) }
catch { $unicodeCollisionRejected = $_.Exception.Message -match 'collide on Windows' }
if (-not $unicodeCollisionRejected) { throw 'Unicode-normalized Windows path collisions were accepted.' }

$memory = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.Compression.ZipArchive($memory, [System.IO.Compression.ZipArchiveMode]::Create, $true)
$archive = $null
try {
    [void]$writer.CreateEntry('safe-folder/')
    [void]$writer.CreateEntry('../escape-folder/')
    $writer.Dispose()
    $memory.Position = 0
    $archive = New-Object System.IO.Compression.ZipArchive($memory, [System.IO.Compression.ZipArchiveMode]::Read, $true)
    $directoryPaths = @($archive.Entries | Where-Object { [String]::IsNullOrEmpty($_.Name) } | ForEach-Object FullName)
    if ($directoryPaths -notcontains 'safe-folder/' -or $directoryPaths -notcontains '../escape-folder/') {
        throw 'The ZIP fixture did not produce expected directory-entry paths.'
    }
    $directoryRejected = $false
    try { Assert-KiWeavePortableArchivePaths -Paths @($archive.Entries | ForEach-Object FullName) }
    catch { $directoryRejected = $_.Exception.Message -match 'unsafe path' }
    if (-not $directoryRejected) { throw 'A ZIP directory entry with traversal was accepted.' }
}
finally {
    if ($null -ne $archive) { $archive.Dispose() }
    if ($null -ne $writer) { $writer.Dispose() }
    $memory.Dispose()
}

Write-Output 'PASS release workflow PowerShell blocks parse and portable archive rejects unsafe file and directory paths'
