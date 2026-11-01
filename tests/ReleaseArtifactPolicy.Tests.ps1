$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '..\tools\VerifyReleaseArtifact.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) ('KiWeave-ReleaseArtifact-' + [Guid]::NewGuid().ToString('N'))
$label = 'test-build'
$setupName = "KiWeave-Setup-beta5-$label.exe"
$portableName = "KiWeave-Portable-beta5-$label.zip"

function Write-TestArtifact([string]$directory, [string]$manifestNames = $null) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $directory $setupName), [byte[]](1, 2, 3, 4))
    [System.IO.File]::WriteAllBytes((Join-Path $directory $portableName), [byte[]](5, 6, 7, 8))
    $setupHash = (Get-FileHash -LiteralPath (Join-Path $directory $setupName) -Algorithm SHA256).Hash.ToLowerInvariant()
    $portableHash = (Get-FileHash -LiteralPath (Join-Path $directory $portableName) -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines = @("$setupHash  $setupName", "$portableHash  $portableName")
    if ($manifestNames) { $lines = @($manifestNames -split ',') | ForEach-Object { if ($_ -eq 'setup') { $lines[0] } else { $lines[1] } } }
    [System.IO.File]::WriteAllLines((Join-Path $directory 'SHA256SUMS.txt'), [string[]]$lines, [System.Text.Encoding]::ASCII)
}

function Assert-Rejected([scriptblock]$operation, [string]$message) {
    $rejected = $false
    try { & $operation } catch { $rejected = $true }
    if (-not $rejected) { throw $message }
}

try {
    Write-TestArtifact $root
    & $helper -PackageDirectory $root -BuildLabel $label | Out-Null

    $tampered = Join-Path $root 'tampered'
    Write-TestArtifact $tampered
    [System.IO.File]::AppendAllText((Join-Path $tampered $setupName), 'changed')
    Assert-Rejected { & $helper -PackageDirectory $tampered -BuildLabel $label } 'Tampered installer was accepted.'

    $duplicate = Join-Path $root 'duplicate'
    Write-TestArtifact $duplicate -manifestNames 'setup,setup'
    Assert-Rejected { & $helper -PackageDirectory $duplicate -BuildLabel $label } 'Duplicate checksum entries were accepted.'

    $extra = Join-Path $root 'extra'
    Write-TestArtifact $extra
    [System.IO.File]::WriteAllText((Join-Path $extra 'unexpected.txt'), 'unexpected')
    Assert-Rejected { & $helper -PackageDirectory $extra -BuildLabel $label } 'Unexpected artifact file was accepted.'

    Write-Output 'Release artifact policy checks passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
