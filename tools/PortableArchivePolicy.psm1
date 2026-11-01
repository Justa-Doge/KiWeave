Set-StrictMode -Version Latest

function Assert-KiWeavePortableArchivePaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$Paths
    )

    $seenWindowsPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entryPath in $Paths) {
        if ([String]::IsNullOrWhiteSpace($entryPath)) {
            throw 'Portable ZIP contains an empty entry path.'
        }

        $normalized = $entryPath.Replace([string][char]92, '/')
        if ($normalized.StartsWith('/') -or
            $normalized -match '(^|/)\.\.(?:/|$)' -or
            $normalized -match '^[A-Za-z]:') {
            throw "Portable ZIP contains an unsafe path: $entryPath"
        }

        $segments = $normalized.Split('/')
        for ($index = 0; $index -lt $segments.Length; $index++) {
            $segment = $segments[$index]
            if ($segment.Length -eq 0 -and $index -eq $segments.Length - 1 -and $normalized.EndsWith('/')) { continue }
            if ($segment.Length -eq 0 -or $segment.Length -gt 255 -or
                $segment -match '[\x00-\x1f<>:"|?*]' -or
                $segment.EndsWith('.') -or $segment.EndsWith(' ')) {
                throw "Portable ZIP contains a Windows-incompatible path: $entryPath"
            }

            $deviceName = ($segment.Split('.')[0]).TrimEnd([char[]]@(' ', '.'))
            if ($deviceName -match '^(?i:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|CLOCK\$|COM[1-9¹²³]|LPT[1-9¹²³])$') {
                throw "Portable ZIP contains a Windows device path: $entryPath"
            }
        }

        $windowsPath = $normalized.TrimEnd('/').Normalize([Text.NormalizationForm]::FormC)
        if (-not $seenWindowsPaths.Add($windowsPath)) {
            throw "Portable ZIP contains paths that collide on Windows: $entryPath"
        }
    }
}

Export-ModuleMember -Function Assert-KiWeavePortableArchivePaths
