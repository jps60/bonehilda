param(
    [string]$Dotnet = 'dotnet',
    [switch]$SkipChecks
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    & $Dotnet restore src/Bonehilda/Bonehilda.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $Dotnet build src/Bonehilda/Bonehilda.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not $SkipChecks) {
        & $Dotnet run --project tests/Bonehilda.Checks/Bonehilda.Checks.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Maintenance policy checks failed.' }
    }
    $staging = Join-Path $projectRoot 'dist/package'
    $pluginDirectory = Join-Path $staging 'BepInEx/plugins/Bonehilda'
    New-Item -ItemType Directory -Force $pluginDirectory | Out-Null
    Copy-Item -LiteralPath 'src/Bonehilda/bin/Release/net462/Bonehilda.dll' -Destination $pluginDirectory -Force
    Copy-Item -LiteralPath 'README.md', 'LICENSE' -Destination $staging -Force
    Copy-Item -LiteralPath 'assets/icon.png' -Destination $staging -Force
    Copy-Item -LiteralPath 'assets/icon-prompt.txt' -Destination (Join-Path $staging 'ARTWORK.txt') -Force
    $packageReadme = [IO.File]::ReadAllText((Join-Path $projectRoot 'README.md')).Replace('assets/icon.png', 'icon.png').Replace('assets/bonehilda-artwork.png', 'icon.png').Replace('assets/icon-prompt.txt', 'ARTWORK.txt')
    [IO.File]::WriteAllText((Join-Path $staging 'README.md'), $packageReadme, (New-Object Text.UTF8Encoding($false)))
    [xml]$project = Get-Content 'src/Bonehilda/Bonehilda.csproj'
    $version = $project.Project.PropertyGroup.Version
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $pluginDirectory 'Bonehilda.dll')).Version
    if ($assemblyVersion.ToString(3) -ne $version) { throw 'Assembly and package versions do not match.' }
    $manifest = [ordered]@{
        name = 'Bonehilda'
        version_number = $version
        website_url = 'https://github.com/jps60/bonehilda'
        description = "Makin' Magic: three coffin tiers summon skeletal maids with 2/4/8 supply slots, animated repairs, machine supply, and free will. Dismiss to rest; open the coffin to recall."
        dependencies = @('denikson-BepInExPack_Valheim-5.4.2360', 'ValheimModding-Jotunn-2.30.2')
    }
    $utf8 = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $staging 'manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
    # Never package the stripped reference DLLs from the build directory.
    $packageFiles = @(Get-ChildItem -LiteralPath $staging -Recurse -File)
    $unexpected = @($packageFiles | Where-Object { $_.Name -notin @('Bonehilda.dll', 'README.md', 'LICENSE', 'manifest.json', 'icon.png', 'ARTWORK.txt') })
    if ($unexpected.Count -gt 0) { throw 'Unexpected files in dist/package. Clear that directory before packaging.' }
    $archive = Join-Path $projectRoot "dist/Bonehilda-$version.zip"
    Compress-Archive -Path "$staging/BepInEx", "$staging/README.md", "$staging/LICENSE", "$staging/manifest.json", "$staging/icon.png", "$staging/ARTWORK.txt" -DestinationPath $archive -Force
    Write-Output "Package: $archive"
}
finally {
    Pop-Location
}
