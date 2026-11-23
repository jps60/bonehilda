param([switch]$VerifyPublicDownload)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/Bonehilda/Bonehilda.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$archiveName = "Bonehilda-$version.zip"
$archive = Join-Path $projectRoot "dist/$archiveName"
& (Join-Path $PSScriptRoot 'test-package.ps1') -ArchivePath $archive
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$repositoryUrl = 'https://github.com/jps60/bonehilda'
$releaseUrl = "$repositoryUrl/releases/tag/v$version"
$downloadUrl = "$repositoryUrl/releases/download/v$version/$archiveName"
$utf8 = New-Object Text.UTF8Encoding($false)
$strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
$headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'Bonehilda-upload-preparation' }
$publicVerified = $false
if ($VerifyPublicDownload) {
    # Fetch public data only; no access tokens or publishing credentials are needed.
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/jps60/bonehilda/releases/tags/v$version" -Headers $headers
    $asset = @($release.assets | Where-Object name -eq $archiveName)
    if ($release.draft -or $asset.Count -ne 1 -or $asset[0].browser_download_url -ne $downloadUrl) { throw 'Matching public release asset was not found.' }
    $temporaryDownload = Join-Path ([IO.Path]::GetTempPath()) ('Bonehilda-upload-' + [Guid]::NewGuid().ToString('N') + '.zip')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $temporaryDownload
        if ((Get-FileHash -LiteralPath $temporaryDownload -Algorithm SHA256).Hash -ne $hash) { throw 'Public archive does not match the local upload archive.' }
        $publicVerified = $true
    } finally { if (Test-Path -LiteralPath $temporaryDownload) { Remove-Item -LiteralPath $temporaryDownload -Force } }
}
$uploadParent = [IO.Path]::GetFullPath((Join-Path $projectRoot 'dist/upload'))
$root = [IO.Path]::GetFullPath((Join-Path $uploadParent "Bonehilda-$version"))
$staging = [IO.Path]::GetFullPath((Join-Path $uploadParent ('staging-' + [Guid]::NewGuid().ToString('N'))))
$safePrefix = $uploadParent + [IO.Path]::DirectorySeparatorChar
foreach ($path in @($root, $staging)) {
    if (-not $path.StartsWith($safePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Upload path escaped dist/upload.' }
}
function Write-UploadText([string]$relative, [string]$content) {
    [IO.File]::WriteAllText((Join-Path $staging $relative), $content, $utf8)
}
function Read-Template([string]$name) {
    $content = [IO.File]::ReadAllText((Join-Path $projectRoot "publishing/$name"), $strictUtf8)
    $content = $content.Replace('{{VERSION}}', $version).Replace('{{REPOSITORY_URL}}', $repositoryUrl).Replace('{{RELEASE_URL}}', $releaseUrl)
    if ($content -match '\{\{[^}]+\}\}') { throw "Unresolved listing placeholder in $name" }
    return $content
}
try {
    foreach ($directory in @('', 'thunderstore', 'nexus', 'gallery')) { New-Item -ItemType Directory -Force (Join-Path $staging $directory) | Out-Null }
    Copy-Item -LiteralPath $archive -Destination (Join-Path $staging $archiveName)
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
            $entry = $zip.Entries | Where-Object { $_.FullName -ceq $name } | Select-Object -First 1
            if (-not $entry) { throw "Missing package entry: $name" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $staging "thunderstore/$name"))
        }
    } finally { $zip.Dispose() }
    $manifest = [IO.File]::ReadAllText((Join-Path $staging 'thunderstore/manifest.json'), $strictUtf8) | ConvertFrom-Json
    $summary = (Read-Template 'nexus-summary.txt').Trim()
    if ($summary.Length -gt 250) { throw 'Keep the shared short listing summary within 250 characters.' }
    $nexusDescription = Read-Template 'nexus-description.bbcode'
    if (-not $nexusDescription.Contains('not a server-only mod') -or -not $nexusDescription.Contains('AI-generated code')) { throw 'Nexus description lost its compatibility or AI disclosure.' }
    Write-UploadText 'nexus/description.bbcode' $nexusDescription
    $plainDescription = $nexusDescription -replace '\[url=([^\]]+)\]([^\[]*)\[/url\]', '$2 ($1)' -replace '\[\*\]', '- ' -replace '\[/?(?:size(?:=\d+)?|b|i|list(?:=1)?)\]', ''
    Write-UploadText 'nexus/description.txt' $plainDescription
    Write-UploadText 'nexus/summary.txt' $summary
    Write-UploadText 'nexus/file-description.txt' (Read-Template 'nexus-file-description.txt')
    Write-UploadText 'thunderstore/short-description.txt' $manifest.description
    $thunderstore = [ordered]@{
        game = 'Valheim'; community = 'valheim'; team = 'Swiftfingers'; package = 'Bonehilda'; version = $version
        intended_identifier = "Swiftfingers-Bonehilda-$version"; archive = "../$archiveName"; website = $repositoryUrl
        description = $manifest.description; dependencies = @($manifest.dependencies); nsfw = $false
        listing_text = 'README.md is already embedded in the archive; the adjacent copy is for review.'
        category_guidance = 'Choose current applicable mod/building/NPC/utility categories. Do not describe this as server-only or tested dedicated-server compatible.'
        account_status = 'Select or create the Swiftfingers team in the upload account. Availability and ownership have not been verified.'
        disclosure = 'Substantial code and listing text generated with OpenAI Codex; illustrated icon generated with OpenAI imagegen. Use applicable AI labels offered by the site.'
    }
    Write-UploadText 'thunderstore/fields.json' ($thunderstore | ConvertTo-Json -Depth 6)
    $nexus = [ordered]@{
        game = 'Valheim'; name = 'Bonehilda'; author = 'Swiftfingers'; version = $version
        summary = $summary; archive = "../$archiveName"; file_title = "Bonehilda $version"; file_description = 'file-description.txt'
        file_group = 'Main files'; release_label = 'Work in progress; experimental multiplayer'; adult_content = $false
        description = 'description.bbcode'; cover_image = '../gallery/01-coffins-and-maids.png'
        website = $repositoryUrl; issue_tracker = "$repositoryUrl/issues"
        category_guidance = 'Choose the closest existing gameplay/NPC/building category; the exact current category ID is selected in the form.'
        required_ai_classifications = @('AI-Generated Content', 'AI Media')
        requirements = @(
            @{ name = 'BepInExPack_Valheim'; version = '5.4.2360'; url = 'https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/'; type = 'External requirement' },
            @{ name = 'Jotunn'; version = '2.30.2 or newer'; url = 'https://www.nexusmods.com/valheim/mods/1138'; upstream = 'https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/'; type = 'Nexus requirement; use upstream if mirror is older' }
        )
        license = 'MIT for project-owned content; see LICENSE and THIRD_PARTY_NOTICES.md in the ZIP.'
        permissions_guidance = 'Keep reuse/fork settings consistent with the published MIT license. AI-training and other new account preferences have not been selected; review these fields before submitting.'
        account_status = 'Choose the intended Nexus account; use Swiftfingers in the Author field. No account authentication has been verified.'
        install_support = 'Manual and r2modman local ZIP layouts documented. Vortex automatic deployment has not been validated.'
    }
    Write-UploadText 'nexus/fields.json' ($nexus | ConvertTo-Json -Depth 6)
    $gallery = [IO.File]::ReadAllText((Join-Path $projectRoot 'publishing/gallery.json'), $strictUtf8) | ConvertFrom-Json
    $galleryRecords = @()
    foreach ($image in $gallery) {
        $source = [IO.Path]::GetFullPath((Join-Path $projectRoot $image.source))
        $assetPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot 'assets/models')) + [IO.Path]::DirectorySeparatorChar
        if (-not $source.StartsWith($assetPrefix, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($image.filename) -cne $image.filename) { throw 'Invalid gallery path.' }
        Copy-Item -LiteralPath $source -Destination (Join-Path $staging "gallery/$($image.filename)")
        $galleryRecords += [ordered]@{ file = $image.filename; caption = $image.caption; role = $image.role; source = $image.source; sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash }
    }
    Write-UploadText 'gallery/captions.json' (ConvertTo-Json -InputObject $galleryRecords -Depth 5)
    Write-UploadText 'gallery/NOTES.txt' "Existing development captures, copied without alteration. These illustrate the models and game appearance; they are not evidence of a complete $version server qualification run. The separate illustrative icon in thunderstore/icon.png is AI-generated. Upload gallery images individually; do not bundle them inside the mod archive."
    $verification = [ordered]@{ version = $version; archive = $archiveName; sha256 = $hash; bytes = (Get-Item -LiteralPath $archive).Length; public_download = $downloadUrl; public_download_verified = $publicVerified; submitted_to_thunderstore = $false; submitted_to_nexus = $false }
    Write-UploadText 'verification.json' ($verification | ConvertTo-Json -Depth 4)
    Write-UploadText 'README.txt' @"
Bonehilda $version - upload materials for Swiftfingers

UPLOAD THIS FILE TO BOTH HOSTS: $archiveName
It is the existing game-mod ZIP, unchanged. Do not upload this entire directory as a mod.
SHA-256: $hash
Public release downloaded and verified during preparation: $publicVerified

Thunderstore: select Valheim and the Swiftfingers team; upload $archiveName.
Its README, icon, version and dependencies are already inside. Review thunderstore/fields.json.
The team/account has not been authenticated or created by this script.

Nexus: select Valheim, title Bonehilda, Author Swiftfingers, version $version.
Paste nexus/summary.txt into the summary field. Use nexus/description.bbcode in a BBCode/source
editor, or nexus/description.txt if the editor accepts only plain/rich text. Do not paste BBCode
as literal visible text in a rich-text editor.
Review nexus/fields.json for requirements, AI labels, permissions and cover image.
Upload $archiveName under Main files with nexus/file-description.txt.
Add gallery PNGs individually in the order and with the captions in gallery/captions.json.

Keep the same version on clients and server. Dedicated-server gameplay remains experimental.
Review account-specific permission choices before submitting. No listing has been submitted.
For the handoff and migration steps see docs/RELEASING.md in the source repository.
"@
    # Generate an inventory so transferred listing files and captures can be checked too.
    $lines = @(Get-ChildItem -LiteralPath $staging -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
    })
    Write-UploadText 'checksums.sha256' (($lines -join "`n") + "`n")
    if ((Get-FileHash -LiteralPath (Join-Path $staging $archiveName) -Algorithm SHA256).Hash -ne $hash) { throw 'Staged upload archive changed.' }
    # Both recursive targets were resolved and verified against dist/upload above.
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    Move-Item -LiteralPath $staging -Destination $root
    Write-Output "Prepared upload directory: $root"
    Write-Output "Archive unchanged: $hash"
    Write-Output "Public download verified: $publicVerified"
    Write-Output 'No account sign-in, external draft, or mod-host submission was performed.'
} finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
