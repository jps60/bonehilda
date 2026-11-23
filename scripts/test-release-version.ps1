param([string]$Tag = '', [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
[xml]$project = [IO.File]::ReadAllText((Join-Path $ProjectRoot 'src/Bonehilda/Bonehilda.csproj'))
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or @($version.Split('.') | Where-Object { [long]$_ -gt 65534 }).Count) { throw 'Invalid release version.' }
$escaped = [regex]::Escape($version)
$contracts = @{
    'src/Bonehilda/Plugin.cs' = 'public const string Version = "' + $escaped + '";'
    'README.md' = '\*\*Version ' + $escaped + ' . Author: Swiftfingers'
    'docs/DEVELOPMENT.md' = '\*\*Version ' + $escaped + '\.\*\*'
    'CHANGELOG.md' = '(?m)^## ' + $escaped + '\r?$'
}
foreach ($path in $contracts.Keys) {
    if ([regex]::Matches([IO.File]::ReadAllText((Join-Path $ProjectRoot $path)), $contracts[$path]).Count -ne 1) { throw "Release version mismatch: $path" }
}
if ($Tag -and $Tag -cne "v$version") { throw "Release tag must be v$version." }
Write-Output "Release metadata aligned: $version"
