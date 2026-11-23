param([Parameter(Mandatory = $true)][string]$ArchivePath, [Parameter(Mandatory = $true)][string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$root = [IO.Path]::GetFullPath($Destination)
$prefix = $root + [IO.Path]::DirectorySeparatorChar
New-Item -ItemType Directory -Path $root -Force | Out-Null
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
try {
    foreach ($entry in $zip.Entries) {
        $name = $entry.FullName.Replace('\', '/')
        $target = [IO.Path]::GetFullPath((Join-Path $root $name))
        if (!$target.StartsWith($prefix, [StringComparison]::Ordinal) -or $name.StartsWith('/') -or $name.Contains(':')) { throw 'Archive entry escaped reference directory.' }
        if ($name.EndsWith('/')) { New-Item -ItemType Directory -Path $target -Force | Out-Null; continue }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
    }
} finally { $zip.Dispose() }
