$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'extensions\streamdeck\com.kiweave.streamdeck.sdPlugin'
$out = Join-Path $PSScriptRoot 'KiWeave-StreamDeck.sdPlugin.zip'
if (-not (Test-Path -LiteralPath (Join-Path $root 'manifest.json'))) { throw 'Stream Deck manifest is missing.' }
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }
Compress-Archive -Path (Join-Path $root '*') -DestinationPath $out
Write-Output "Packaged: $out"
