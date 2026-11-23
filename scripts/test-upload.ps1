param([Parameter(Mandatory = $true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = [IO.Path]::GetFullPath($Directory)
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/Bonehilda/Bonehilda.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$utf8 = New-Object Text.UTF8Encoding($false, $true)
function Read-UploadJson([string]$name) { [IO.File]::ReadAllText((Join-Path $root $name), $utf8) | ConvertFrom-Json }
$verification = Read-UploadJson 'verification.json'
$manifest = Read-UploadJson 'thunderstore/manifest.json'
$nexus = Read-UploadJson 'nexus/fields.json'
$thunderstore = Read-UploadJson 'thunderstore/fields.json'
$archiveName = "Bonehilda-$version.zip"
if ($verification.version -cne $version -or $verification.archive -cne $archiveName -or
    $manifest.version_number -cne $version -or $nexus.version -cne $version -or $thunderstore.version -cne $version) { throw 'Upload materials contain mixed release versions.' }
if ($nexus.author -cne 'Swiftfingers' -or $manifest.author -cne 'Swiftfingers' -or $thunderstore.team -cne 'Swiftfingers') { throw 'Upload author/team mismatch.' }
if (@(Compare-Object @('AI-Generated Content', 'AI Media') @($nexus.required_ai_classifications)).Count -ne 0) { throw 'Nexus AI classifications are incomplete.' }
if ($verification.submitted_to_thunderstore -or $verification.submitted_to_nexus) { throw 'Preparation must not claim a site submission.' }
$gallery = Read-UploadJson 'gallery/captions.json'
$expected = @($archiveName, 'checksums.sha256', 'README.txt', 'verification.json', 'gallery/captions.json', 'gallery/NOTES.txt',
    'nexus/description.bbcode', 'nexus/description.txt', 'nexus/fields.json', 'nexus/file-description.txt', 'nexus/summary.txt',
    'thunderstore/CHANGELOG.md', 'thunderstore/fields.json', 'thunderstore/icon.png', 'thunderstore/manifest.json', 'thunderstore/README.md', 'thunderstore/short-description.txt')
foreach ($image in $gallery) {
    if ([IO.Path]::GetFileName($image.file) -cne $image.file -or [string]::IsNullOrWhiteSpace($image.caption)) { throw 'Invalid gallery filename or missing caption.' }
    $expected += 'gallery/' + $image.file
    if ((Get-FileHash -LiteralPath (Join-Path $root "gallery/$($image.file)") -Algorithm SHA256).Hash -ne $image.sha256) { throw 'Gallery capture checksum mismatch.' }
}
$actual = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') })
if (@(Compare-Object $expected $actual -CaseSensitive).Count -ne 0) { throw 'Upload directory has missing or unexpected files.' }
$seen = @{}
foreach ($line in [IO.File]::ReadAllLines((Join-Path $root 'checksums.sha256'), $utf8)) {
    if ($line -notmatch '^([a-fA-F0-9]{64})  (.+)$') { throw 'Malformed upload checksum inventory.' }
    $hash = $matches[1]; $relative = $matches[2]
    if ($expected -cnotcontains $relative -or $relative -eq 'checksums.sha256' -or $seen.ContainsKey($relative)) { throw 'Unexpected checksum path.' }
    $seen[$relative] = $true
    if ((Get-FileHash -LiteralPath (Join-Path $root $relative) -Algorithm SHA256).Hash -ne $hash) { throw "Upload checksum mismatch: $relative" }
}
if ($seen.Count -ne $expected.Count - 1) { throw 'Upload checksum inventory is incomplete.' }
if ((Get-FileHash -LiteralPath (Join-Path $root $archiveName) -Algorithm SHA256).Hash -ne $verification.sha256) { throw 'Upload archive differs from its verification record.' }
foreach ($name in @('nexus/description.bbcode', 'nexus/description.txt', 'nexus/file-description.txt')) {
    $text = [IO.File]::ReadAllText((Join-Path $root $name), $utf8)
    if (-not $text.Contains($version) -or $text -match '\{\{[^}]+\}\}') { throw 'Unresolved or stale listing text.' }
}
$stack = New-Object 'System.Collections.Generic.Stack[string]'
$description = [IO.File]::ReadAllText((Join-Path $root 'nexus/description.bbcode'), $utf8)
foreach ($tag in [regex]::Matches($description, '\[(/?)(b|i|size|list|url)(?:=[^\]]+)?\]')) {
    if ($tag.Groups[1].Value -eq '/') {
        if ($stack.Count -eq 0 -or $stack.Pop() -ne $tag.Groups[2].Value) { throw 'Unbalanced Nexus BBCode.' }
    } else { $stack.Push($tag.Groups[2].Value) }
}
if ($stack.Count -ne 0) { throw 'Unclosed Nexus BBCode.' }
Write-Output "Upload materials validated: $version; $($seen.Count) checksums; $($gallery.Count) captioned images; consistent author and AI labels."
