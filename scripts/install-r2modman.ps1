param(
    [Parameter(Mandatory = $true)][string]$ProfilePath,
    [string]$PackageDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($PackageDirectory)) { $PackageDirectory = Join-Path $PSScriptRoot '../dist/package' }
$profileRoot = [IO.Path]::GetFullPath($ProfilePath)
$packageRoot = [IO.Path]::GetFullPath($PackageDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Raw | ConvertFrom-Json
$version = [version]$manifest.version_number
$yamlPath = Join-Path $profileRoot 'mods.yml'
$yaml = [IO.File]::ReadAllText($yamlPath)
$blocks = [regex]::Matches($yaml, '(?ms)^- manifestVersion: [^\r\n]*\r?\n.*?(?=^- manifestVersion:|\z)')
$modEntries = @($blocks | Where-Object { $_.Value -match '(?m)^  displayName: Bonehilda\r?$' })
if ($modEntries.Count -ne 1) { throw 'Expected exactly one installed Bonehilda entry. Import the ZIP first if this is a new profile.' }
$entryMatch = $modEntries[0]
$entry = $entryMatch.Value
$packageName = [regex]::Match($entry, '(?m)^  name: ([^\r\n]+)').Groups[1].Value
$disabled = $entry -match '(?m)^  enabled: false\r?$'
$suffix = if ($disabled) { '.old' } else { '' }
$modRoot = [IO.Path]::GetFullPath((Join-Path $profileRoot "BepInEx/plugins/$packageName"))
$pluginsRoot = [IO.Path]::GetFullPath((Join-Path $profileRoot 'BepInEx/plugins'))
if (-not $modRoot.StartsWith($pluginsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed mod path is outside the profile plugin directory.'
}
$dlls = @(Get-ChildItem -LiteralPath $modRoot -Recurse -File | Where-Object { $_.Name -eq "Bonehilda.dll$suffix" })
if ($dlls.Count -ne 1) { throw 'Expected exactly one installed Bonehilda DLL matching the enabled/disabled state.' }
$sourceDll = Join-Path $packageRoot 'BepInEx/plugins/Bonehilda/Bonehilda.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($sourceDll).Version.ToString(3) -ne $manifest.version_number) {
    throw 'Package manifest and assembly versions do not match.'
}
$backup = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))) ('.tools/backups/r2-release-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Force $backup | Out-Null
Copy-Item -LiteralPath $yamlPath, $dlls[0].FullName -Destination $backup
foreach ($name in @('manifest.json', 'mm_v2_manifest.json', 'README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'icon.png', 'ARTWORK.txt')) {
    $target = Join-Path $modRoot "$name$suffix"
    if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination $backup }
}
$utf8 = New-Object Text.UTF8Encoding($false)
$newline = if ($yaml.Contains("`r`n")) { "`r`n" } else { "`n" }
$updatedEntry = [regex]::Replace($entry, '(?ms)^  description:[^\r\n]*(?:\r?\n    [^\r\n]*)*',
    ('  description: ' + ($manifest.description | ConvertTo-Json -Compress)))
$author = [string]$manifest.author
if (-not [string]::IsNullOrWhiteSpace($author)) {
    $updatedEntry = [regex]::Replace($updatedEntry, '(?m)^  authorName:[^\r\n]*',
        ('  authorName: ' + ($author | ConvertTo-Json -Compress)))
}
$updatedEntry = [regex]::Replace($updatedEntry, '(?m)^    major: \d+', "    major: $($version.Major)")
$updatedEntry = [regex]::Replace($updatedEntry, '(?m)^    minor: \d+', "    minor: $($version.Minor)")
$updatedEntry = [regex]::Replace($updatedEntry, '(?m)^    patch: \d+', "    patch: $($version.Build)")
$dependencies = '  dependencies:' + $newline + (($manifest.dependencies | ForEach-Object { "    - $_" }) -join $newline)
$updatedEntry = [regex]::Replace($updatedEntry, '(?m)^  dependencies:[^\r\n]*(?:\r?\n    - [^\r\n]*)*', $dependencies)
$updatedYaml = $yaml.Substring(0, $entryMatch.Index) + $updatedEntry + $yaml.Substring($entryMatch.Index + $entryMatch.Length)
if ([IO.File]::ReadAllText($yamlPath) -ne $yaml) { throw 'r2modman changed the profile during the update. Try again.' }
# Mono can keep the running DLL memory-mapped: Windows permits renaming it,
# but refuses overwriting it. Stage the replacement and retain the loaded file
# in the backup directory so the current game can finish with its old assembly.
$pendingDll = $dlls[0].FullName + '.update'
$loadedBackup = Join-Path $backup 'loaded-Bonehilda.dll'
Copy-Item -LiteralPath $sourceDll -Destination $pendingDll -Force
Move-Item -LiteralPath $dlls[0].FullName -Destination $loadedBackup
try {
    Move-Item -LiteralPath $pendingDll -Destination $dlls[0].FullName
}
catch {
    Move-Item -LiteralPath $loadedBackup -Destination $dlls[0].FullName
    throw
}
foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'icon.png', 'ARTWORK.txt')) {
    Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination (Join-Path $modRoot "$name$suffix") -Force
}
$gameRoot = [IO.Directory]::GetParent([IO.Directory]::GetParent($profileRoot).FullName).FullName
$cache = Join-Path $gameRoot "cache/$packageName/$($manifest.version_number)"
New-Item -ItemType Directory -Force $cache | Out-Null
Copy-Item -LiteralPath (Join-Path $packageRoot 'icon.png') -Destination $cache -Force
$mmPath = Join-Path $modRoot "mm_v2_manifest.json$suffix"
if (Test-Path -LiteralPath $mmPath) {
    $mm = Get-Content -LiteralPath $mmPath -Raw | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($author)) { $mm.authorName = $author }
    $mm.description = $manifest.description
    $mm.versionNumber.major = $version.Major
    $mm.versionNumber.minor = $version.Minor
    $mm.versionNumber.patch = $version.Build
    $mm.dependencies = @($manifest.dependencies)
    $mm.icon = Join-Path $cache 'icon.png'
    [IO.File]::WriteAllText($mmPath, ($mm | ConvertTo-Json -Depth 20), $utf8)
}
[IO.File]::WriteAllText($yamlPath, $updatedYaml, $utf8)
if ((Get-FileHash -LiteralPath $sourceDll).Hash -ne (Get-FileHash -LiteralPath $dlls[0].FullName).Hash) {
    throw 'Installed DLL checksum does not match.'
}
Write-Output "Installed Bonehilda $($manifest.version_number) in $profileRoot"
Write-Output "Preserved enabled state: $(-not $disabled)"
Write-Output "Previous version backed up to $backup"
Write-Output 'Restart r2modman to refresh its cached version, image, and description.'
Write-Output 'Restart Valheim to load the updated DLL.'
