param(
    [string]$Dotnet = 'dotnet',
    [switch]$SkipChecks
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'test-release-version.ps1') -Tag $env:RELEASE_TAG
    & $Dotnet restore src/Bonehilda/Bonehilda.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $Dotnet build src/Bonehilda/Bonehilda.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not $SkipChecks) {
        & (Join-Path $projectRoot 'tests/ReleaseTools.Checks.ps1')
        & $Dotnet run --project tests/Bonehilda.Checks/Bonehilda.Checks.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Maintenance policy checks failed.' }
    }
    $dist = Join-Path $projectRoot 'dist'
    $staging = Join-Path $dist ('staging-' + [Guid]::NewGuid().ToString('N'))
    $pluginDirectory = Join-Path $staging 'BepInEx/plugins/Bonehilda'
    New-Item -ItemType Directory -Force $pluginDirectory | Out-Null
    Copy-Item -LiteralPath 'src/Bonehilda/bin/Release/net462/Bonehilda.dll' -Destination $pluginDirectory -Force
    Copy-Item -LiteralPath 'README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md' -Destination $staging -Force
    Copy-Item -LiteralPath 'assets/icon.png' -Destination $staging -Force
    Copy-Item -LiteralPath 'assets/icon-prompt.txt' -Destination (Join-Path $staging 'ARTWORK.txt') -Force
    [xml]$project = Get-Content 'src/Bonehilda/Bonehilda.csproj'
    $version = $project.Project.PropertyGroup.Version
    $readme = [IO.File]::ReadAllText((Join-Path $projectRoot 'README.md')).Replace(
        'https://raw.githubusercontent.com/jps60/bonehilda/main/assets/',
        "https://raw.githubusercontent.com/jps60/bonehilda/v$version/assets/")
    [IO.File]::WriteAllText((Join-Path $staging 'README.md'), $readme, (New-Object Text.UTF8Encoding($false)))
    $jotunnVersion = [string]($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'JotunnLib').Version
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $pluginDirectory 'Bonehilda.dll')).Version
    if ($assemblyVersion.ToString(3) -ne $version) { throw 'Assembly and package versions do not match.' }
    $manifest = [ordered]@{
        name = 'Bonehilda'
        author = [string]$project.Project.PropertyGroup.Authors
        version_number = $version
        website_url = 'https://github.com/jps60/bonehilda'
        description = "Makin' Magic: three coffin tiers summon skeletal maids with 2/4/8 supply slots, animated repairs, machine supply, and free will. Dismiss to rest; open the coffin to recall."
        dependencies = @('denikson-BepInExPack_Valheim-5.4.2360', "ValheimModding-Jotunn-$jotunnVersion")
    }
    $utf8 = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $staging 'manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
    # Never package the stripped reference DLLs from the build directory.
    $packageFiles = @(Get-ChildItem -LiteralPath $staging -Recurse -File)
    $unexpected = @($packageFiles | Where-Object { $_.Name -notin @('Bonehilda.dll', 'README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'manifest.json', 'icon.png', 'ARTWORK.txt') })
    if ($unexpected.Count -gt 0) { throw 'Unexpected files in dist/package. Clear that directory before packaging.' }
    $archive = Join-Path $projectRoot "dist/Bonehilda-$version.zip"
    # Stable timestamps and portable forward-slash names on Windows and Linux.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.IO.Compression
    $stream = [IO.File]::Open($archive, [IO.FileMode]::Create)
    $zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in ($packageFiles | Sort-Object FullName)) {
            $name = $file.FullName.Substring($staging.Length + 1).Replace('\', '/')
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $inputFile = [IO.File]::OpenRead($file.FullName)
            $outputFile = $entry.Open()
            try { $inputFile.CopyTo($outputFile) } finally { $outputFile.Dispose(); $inputFile.Dispose() }
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
    & (Join-Path $PSScriptRoot 'test-package.ps1') -ArchivePath $archive
    if (-not $SkipChecks) { & (Join-Path $projectRoot 'tests/Package.Checks.ps1') -ArchivePath $archive }
    # Promote only a validated package. Resolve and check both recursive cleanup
    # targets against this checkout's dist directory before touching them.
    $package = [IO.Path]::GetFullPath((Join-Path $dist 'package'))
    $distPrefix = [IO.Path]::GetFullPath($dist) + [IO.Path]::DirectorySeparatorChar
    if (-not $package.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package path escaped dist.' }
    if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Recurse -Force }
    Move-Item -LiteralPath $staging -Destination $package
    Write-Output "Package: $archive"
}
finally {
    if ($staging -and (Test-Path -LiteralPath $staging)) {
        $cleanup = [IO.Path]::GetFullPath($staging)
        $safeRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'dist')) + [IO.Path]::DirectorySeparatorChar
        if ($cleanup.StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $cleanup -Recurse -Force }
    }
    Pop-Location
}
