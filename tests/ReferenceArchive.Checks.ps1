$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$project = Split-Path -Parent $PSScriptRoot
$prefix = [IO.Path]::GetFullPath((Join-Path $project 'dist')) + [IO.Path]::DirectorySeparatorChar
$fixture = [IO.Path]::GetFullPath((Join-Path $prefix ('reference-tests-' + [Guid]::NewGuid().ToString('N'))))
if (!$fixture.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid reference test directory.' }
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$cases = @{
    Backslash = 'plugins\Jotunn.dll'
    Forward = 'plugins/Jotunn.dll'
    Traversal = '../escaped.dll'
    Rooted = '/escaped.dll'
}
try {
    foreach ($case in $cases.Keys) {
        $archive = Join-Path $fixture "$case.zip"
        $destination = Join-Path $fixture $case
        $zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $stream = $zip.CreateEntry($cases[$case]).Open()
            try { $stream.WriteByte(42) } finally { $stream.Dispose() }
        } finally { $zip.Dispose() }
        $rejected = $false
        try { & (Join-Path $project 'scripts/extract-reference-zip.ps1') -ArchivePath $archive -Destination $destination }
        catch { $rejected = $true }
        if ($case -in @('Backslash', 'Forward')) {
            if ($rejected -or [IO.File]::ReadAllBytes((Join-Path $destination 'plugins/Jotunn.dll'))[0] -ne 42) { throw "Reference extraction failed: $case" }
        } elseif (!$rejected) { throw "Unsafe reference path accepted: $case" }
    }
    if (Test-Path (Join-Path $fixture 'escaped.dll')) { throw 'Unsafe archive wrote outside its destination.' }
    Write-Output '4 reference-archive checks passed.'
} finally {
    if ($fixture.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
