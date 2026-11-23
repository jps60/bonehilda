param([string]$Version = '', [switch]$Patch, [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test-release-version.ps1') -ProjectRoot $ProjectRoot | Out-Null
[xml]$project = [IO.File]::ReadAllText((Join-Path $ProjectRoot 'src/Bonehilda/Bonehilda.csproj'))
$previous = [string]$project.Project.PropertyGroup.Version
if ($Patch -eq [bool]$Version) { throw 'Specify either -Patch or -Version MAJOR.MINOR.PATCH.' }
if ($Patch) { $parts = $previous.Split('.'); $Version = "$($parts[0]).$($parts[1]).$([int]$parts[2]+1)" }
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or @($Version.Split('.') | Where-Object { [long]$_ -gt 65534 }).Count) { throw 'Use MAJOR.MINOR.PATCH with components from 0 to 65534.' }
if ([version]$Version -le [version]$previous) { throw 'New version must be greater than the current version.' }
$escaped = [regex]::Escape($previous)
$changes = @{
    'src/Bonehilda/Bonehilda.csproj' = @(('<Version>' + $escaped + '</Version>'), ('<Version>' + $Version + '</Version>'))
    'src/Bonehilda/Plugin.cs' = @(('public const string Version = "' + $escaped + '";'), ('public const string Version = "' + $Version + '";'))
    'README.md' = @(('\*\*Version ' + $escaped + '(?= )'), ('**Version ' + $Version))
    'docs/DEVELOPMENT.md' = @(('\*\*Version ' + $escaped + '\.\*\*'), ('**Version ' + $Version + '.**'))
}
$pending = @{}
foreach ($path in $changes.Keys) {
    $source = [IO.File]::ReadAllText((Join-Path $ProjectRoot $path))
    $rule = $changes[$path]
    if ([regex]::Matches($source, $rule[0]).Count -ne 1) { throw "Expected one version field: $path. No files changed." }
    $pending[$path] = [regex]::Replace($source, $rule[0], $rule[1])
}
$changelog = [IO.File]::ReadAllText((Join-Path $ProjectRoot 'CHANGELOG.md'))
if ($changelog -notmatch '^# Changelog\r?\n' -or $changelog.Contains("## $Version")) { throw 'Unexpected changelog header. No files changed.' }
$pending['CHANGELOG.md'] = [regex]::Replace($changelog, '^# Changelog\r?\n', "# Changelog`n`n## $Version`n")
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($path in $pending.Keys) { [IO.File]::WriteAllText((Join-Path $ProjectRoot $path), $pending[$path], $utf8) }
Write-Output "Updated $previous -> $Version. Add release notes, then build and commit. No commit, tag or publication was performed."
