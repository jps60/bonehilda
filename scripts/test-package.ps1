param([Parameter(Mandatory = $true)][string]$ArchivePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/Bonehilda/Bonehilda.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$author = [string]$project.Project.PropertyGroup.Authors
$jotunnVersion = [string]($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'JotunnLib').Version
$pluginPath = Join-Path $projectRoot 'src/Bonehilda/bin/Release/net462/Bonehilda.dll'
$pluginSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'src/Bonehilda/Plugin.cs'))
if ($pluginSource -notmatch ('public const string Version = "' + [regex]::Escape($version) + '";')) { throw 'Plugin and project versions differ.' }
if ($author -cne 'Swiftfingers') { throw 'Project author must be Swiftfingers.' }
if ([Reflection.AssemblyName]::GetAssemblyName($pluginPath).Version.ToString(3) -ne $version) { throw 'Built assembly version differs from project.' }
$expected = @('manifest.json', 'README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'icon.png', 'ARTWORK.txt', 'BepInEx/plugins/Bonehilda/Bonehilda.dll')
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
try {
    $files = @{}
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName.Contains('\')) { throw 'ZIP paths must use forward slashes.' }
        $name = $entry.FullName.Replace('\', '/')
        if ($name.EndsWith('/')) { continue }
        if ($expected -cnotcontains $name -or $files.ContainsKey($name)) { throw "Unexpected or duplicate ZIP entry: $name" }
        $files[$name] = $entry
    }
    foreach ($name in $expected) { if (-not $files.ContainsKey($name)) { throw "Missing ZIP entry: $name" } }
    $strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
    function Read-Bytes($entry) {
        $stream = $entry.Open()
        $buffer = New-Object IO.MemoryStream
        try { $stream.CopyTo($buffer); return ,$buffer.ToArray() }
        finally { $stream.Dispose(); $buffer.Dispose() }
    }
    $manifest = $strictUtf8.GetString((Read-Bytes $files['manifest.json'])) | ConvertFrom-Json
    if ($manifest.name -cne 'Bonehilda' -or $manifest.version_number -cne $version -or $manifest.author -cne $author) { throw 'Package name, author, or version is incorrect.' }
    if ([string]::IsNullOrWhiteSpace($manifest.description) -or $manifest.description.Length -gt 250) { throw 'Description must be 1-250 characters.' }
    if ($manifest.website_url -cne 'https://github.com/jps60/bonehilda') { throw 'Incorrect package website.' }
    $dependencies = @('denikson-BepInExPack_Valheim-5.4.2360', "ValheimModding-Jotunn-$jotunnVersion")
    if (@(Compare-Object $dependencies @($manifest.dependencies)).Count -ne 0) { throw 'Package dependency versions differ from supported dependencies.' }
    foreach ($name in @('README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'ARTWORK.txt')) {
        $content = $strictUtf8.GetString((Read-Bytes $files[$name]))
        if ([string]::IsNullOrWhiteSpace($content)) { throw "Empty package document: $name" }
        if ($name -eq 'README.md' -and -not $content.Contains("Version $version")) { throw 'README version is out of date.' }
        if ($name -eq 'README.md' -and ($content.Contains('https://raw.githubusercontent.com/jps60/bonehilda/main/assets/') -or
            -not $content.Contains("https://raw.githubusercontent.com/jps60/bonehilda/v$version/assets/icon.png"))) { throw 'README artwork must be pinned to the release tag.' }
        if ($name -eq 'CHANGELOG.md' -and -not $content.Contains("## $version")) { throw 'Changelog lacks this release.' }
    }
    $icon = Read-Bytes $files['icon.png']
    if ($icon.Length -lt 24 -or [BitConverter]::ToString($icon[0..7]) -cne '89-50-4E-47-0D-0A-1A-0A' -or
        [Text.Encoding]::ASCII.GetString($icon, 12, 4) -cne 'IHDR') { throw 'Icon must be a PNG.' }
    $width = [int64]$icon[16]*16777216 + [int64]$icon[17]*65536 + [int64]$icon[18]*256 + $icon[19]
    $height = [int64]$icon[20]*16777216 + [int64]$icon[21]*65536 + [int64]$icon[22]*256 + $icon[23]
    if ($width -ne 256 -or $height -ne 256) { throw 'Icon must be 256x256.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $archivedHash = [BitConverter]::ToString($sha.ComputeHash((Read-Bytes $files['BepInEx/plugins/Bonehilda/Bonehilda.dll']))).Replace('-', '')
        if ($archivedHash -ne (Get-FileHash -LiteralPath $pluginPath -Algorithm SHA256).Hash) { throw 'ZIP plugin differs from the checked build.' }
    } finally { $sha.Dispose() }
    Write-Output "Package validated: Bonehilda $version; $($files.Count) files; Swiftfingers; 256x256 icon; matching DLL."
} finally { $zip.Dispose() }
