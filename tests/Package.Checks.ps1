param([Parameter(Mandatory = $true)][string]$ArchivePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot 'dist')) + [IO.Path]::DirectorySeparatorChar
$testRoot = [IO.Path]::GetFullPath((Join-Path $distPrefix ('package-tests-' + [Guid]::NewGuid().ToString('N'))))
if (-not $testRoot.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test path escaped dist.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$cases = @{
    ExtraDll = 'Unexpected or duplicate ZIP entry'
    Duplicate = 'Unexpected or duplicate ZIP entry'
    MissingManifest = 'Missing ZIP entry'
    Version = 'Package name, author, or version'
    Description = 'Description must be'
    IconSize = 'Icon must be 256x256'
    Plugin = 'ZIP plugin differs'
    Artwork = 'README artwork must be pinned'
    Backslash = 'ZIP paths must use forward slashes'
}
try {
    foreach ($case in $cases.Keys) {
        $path = Join-Path $testRoot "$case.zip"
        $source = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
        $output = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($entry in $source.Entries) {
                $name = $entry.FullName.Replace('\', '/')
                if ($name.EndsWith('/') -or ($case -eq 'MissingManifest' -and $name -eq 'manifest.json')) { continue }
                $stream = $entry.Open(); $buffer = New-Object IO.MemoryStream
                try { $stream.CopyTo($buffer); $bytes = $buffer.ToArray() } finally { $stream.Dispose(); $buffer.Dispose() }
                if ($name -eq 'manifest.json' -and $case -in @('Version', 'Description')) {
                    $manifest = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
                    if ($case -eq 'Version') { $manifest.version_number = '99.99.99' } else { $manifest.description = 'x' * 251 }
                    $bytes = [Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Depth 8))
                }
                if ($name -eq 'icon.png' -and $case -eq 'IconSize') { $bytes[19] = 1 }
                if ($name -eq 'README.md' -and $case -eq 'Artwork') {
                    $content = [Text.Encoding]::UTF8.GetString($bytes) -replace '/v\d+\.\d+\.\d+(?:-experiment\.[1-9][0-9]*)?/assets/', '/main/assets/'
                    $bytes = [Text.Encoding]::UTF8.GetBytes($content)
                }
                if ($name.Contains('/') -and $case -eq 'Backslash') { $name = $name.Replace('/', '\') }
                if ($name -eq 'BepInEx/plugins/Bonehilda/Bonehilda.dll' -and $case -eq 'Plugin') { $bytes[0] = 0 }
                $destination = $output.CreateEntry($name).Open()
                try { $destination.Write($bytes, 0, $bytes.Length) } finally { $destination.Dispose() }
            }
            if ($case -eq 'ExtraDll') { $output.CreateEntry('BepInEx/plugins/assembly_valheim.dll') | Out-Null }
            if ($case -eq 'Duplicate') { $output.CreateEntry('README.md') | Out-Null }
        } finally { $source.Dispose(); $output.Dispose() }
        $rejected = $false
        try { & (Join-Path $projectRoot 'scripts/test-package.ps1') -ArchivePath $path | Out-Null }
        catch { if ($_.Exception.Message -notmatch $cases[$case]) { throw }; $rejected = $true }
        if (-not $rejected) { throw "Validator accepted invalid package: $case" }
    }
    Write-Output "$($cases.Count) invalid-package rejection checks passed."
} finally {
    if ($testRoot.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
