param([string]$NotesPath = '')
$ErrorActionPreference = 'Stop'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
if (git status --porcelain) { throw 'Commit the completed source changes before publishing a release.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/Bonehilda/Bonehilda.csproj')
$Version = [string]$project.Project.PropertyGroup.Version
$archive = Join-Path $projectRoot "dist/Bonehilda-$Version.zip"
if (-not (Test-Path -LiteralPath $archive)) { throw 'Build archive is missing.' }
& (Join-Path $PSScriptRoot 'test-package.ps1') -ArchivePath $archive
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$archiveSize = (Get-Item -LiteralPath $archive).Length
$notes = if ($NotesPath) { [IO.File]::ReadAllText([IO.Path]::GetFullPath($NotesPath)) } else {
    "Bonehilda $Version. Import the attached ZIP as a local mod in r2modman. Restart Valheim to load the update. Install the same version on every client and server."
}
$notes += "`r`n`r`nSHA-256: $archiveHash"
$head = (git rev-parse HEAD).Trim()
$remote = (git ls-remote origin refs/heads/main)
if ($LASTEXITCODE -ne 0 -or ($remote -split '\s+')[0] -ne $head) { throw 'Push the release commit to main before publishing.' }
$credentialResult = "protocol=https`nhost=github.com`n`n" | git -c credential.interactive=never credential fill 2>$null
if ($LASTEXITCODE -ne 0) { throw 'GitHub authentication is unavailable.' }
$credentialFields = @{}
foreach ($line in $credentialResult) {
    if ($line -match '^([^=]+)=(.*)$') { $credentialFields[$matches[1]] = $matches[2] }
}
if (-not $credentialFields.password) { throw 'GitHub authentication did not supply a credential.' }
$headers = @{ Authorization = ('Bearer ' + $credentialFields.password); Accept = 'application/vnd.github+json'; 'User-Agent' = 'Bonehilda-release'; 'X-GitHub-Api-Version' = '2022-11-28' }
$repoApi = 'https://api.github.com/repos/jps60/bonehilda'
$account = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers
if ($account.login -ne 'jps60') { throw 'Unexpected authenticated GitHub account.' }
$tag = 'v' + $Version
$release = $null
try { $release = Invoke-RestMethod -Uri ($repoApi + '/releases/tags/' + $tag) -Headers $headers }
catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
if (-not $release) {
    $existingReleases = Invoke-RestMethod -Uri ($repoApi + '/releases?per_page=100') -Headers $headers
    $release = @($existingReleases | Where-Object { $_.tag_name -eq $tag }) | Select-Object -First 1
}
if (-not $release) {
    $body = @{ tag_name = $tag; target_commitish = $head; name = "Bonehilda $Version"; body = $notes; draft = $true; prerelease = $false } | ConvertTo-Json -Depth 5
    $release = Invoke-RestMethod -Method Post -Uri ($repoApi + '/releases') -Headers $headers -ContentType 'application/json' -Body $body
}
$assetName = [IO.Path]::GetFileName($archive)
$asset = @($release.assets | Where-Object { $_.name -eq $assetName }) | Select-Object -First 1
if (-not $asset) {
    $uploadBase = $release.upload_url -replace '\{.*$', ''
    $uploadUri = $uploadBase + '?name=' + [Uri]::EscapeDataString($assetName)
    $asset = Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -ContentType 'application/zip' -InFile $archive
}
if ($asset.size -ne $archiveSize) { throw 'Uploaded archive size does not match the local build.' }
if ($asset.digest -and $asset.digest -ne ('sha256:' + $archiveHash.ToLowerInvariant())) { throw 'Uploaded archive digest does not match the local build.' }
if ($release.draft) {
    $release = Invoke-RestMethod -Method Patch -Uri ($repoApi + '/releases/' + $release.id) -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'
}
$publicHeaders = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'Bonehilda-public-release-check' }
$published = Invoke-RestMethod -Uri ($repoApi + '/releases/tags/' + $tag) -Headers $publicHeaders
$publicAsset = @($published.assets | Where-Object { $_.name -eq $assetName }) | Select-Object -First 1
if ($published.draft -or -not $publicAsset) { throw 'Published asset is not publicly visible.' }
$verifyRoot = Join-Path $projectRoot '.tools/release-verification'
New-Item -ItemType Directory -Force $verifyRoot | Out-Null
$downloadPath = Join-Path $verifyRoot ('verified-' + $assetName)
Invoke-WebRequest -UseBasicParsing -Uri $publicAsset.browser_download_url -OutFile $downloadPath
if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Public download checksum mismatch.' }
Write-Output ('Published release: ' + $published.html_url)
Write-Output ('Verified public ZIP: ' + $publicAsset.browser_download_url)
Write-Output ('SHA-256: ' + $archiveHash)
$credentialFields.Clear()
Remove-Variable credentialResult,headers -ErrorAction SilentlyContinue
