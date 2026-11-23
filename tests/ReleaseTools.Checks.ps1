$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$fixture = [IO.Path]::GetFullPath((Join-Path $projectRoot ('dist/version-tests-' + [Guid]::NewGuid().ToString('N'))))
$distPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot 'dist')) + [IO.Path]::DirectorySeparatorChar
if (!$fixture.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid fixture path.' }
$paths = @('src/Bonehilda/Bonehilda.csproj', 'src/Bonehilda/Plugin.cs', 'README.md', 'docs/DEVELOPMENT.md', 'CHANGELOG.md')
$bump = Join-Path $projectRoot 'scripts/bump-version.ps1'
$validate = Join-Path $projectRoot 'scripts/test-release-version.ps1'
[xml]$project = [IO.File]::ReadAllText((Join-Path $projectRoot $paths[0]))
$previous = [string]$project.Project.PropertyGroup.Version
$v = [version]$previous
$next = "$($v.Major).$($v.Minor).$($v.Build + 1)"
$checks = 0
try {
    foreach ($case in @('Explicit', 'Patch', 'Invalid', 'Backwards', 'Mismatch', 'Duplicate', 'Tag')) {
        $root = Join-Path $fixture $case
        foreach ($path in $paths) {
            $target = Join-Path $root $path
            New-Item (Split-Path -Parent $target) -ItemType Directory -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $projectRoot $path) -Destination $target
        }
        $plugin = Join-Path $root $paths[1]
        if ($case -eq 'Mismatch') { [IO.File]::WriteAllText($plugin, [IO.File]::ReadAllText($plugin).Replace("Version = `"$previous`"", 'Version = "0.0.0"')) }
        if ($case -eq 'Duplicate') { [IO.File]::AppendAllText($plugin, "`npublic const string Version = `"$previous`";`n") }
        $before = @($paths | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $root $_)).Hash })
        $failed = $false
        try {
            switch ($case) {
                Explicit { & $bump -ProjectRoot $root -Version $next | Out-Null }
                Patch { & $bump -ProjectRoot $root -Patch | Out-Null }
                Invalid { & $bump -ProjectRoot $root -Version '1.2.3-beta' | Out-Null }
                Backwards { & $bump -ProjectRoot $root -Version $previous | Out-Null }
                Tag { & $validate -ProjectRoot $root -Tag 'v99.99.99' | Out-Null }
                default { & $bump -ProjectRoot $root -Version $next | Out-Null }
            }
        } catch { $failed = $true }
        if ($case -in @('Explicit', 'Patch')) {
            if ($failed) { throw "Valid bump failed: $case" }
            & $validate -ProjectRoot $root -Tag "v$next" | Out-Null
            $readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'))
            if (!$readme.Contains('Swiftfingers') -or !$readme.Contains("Version $next")) { throw 'Bump damaged README metadata.' }
        } else {
            if (!$failed) { throw "Invalid operation accepted: $case" }
            $after = @($paths | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $root $_)).Hash })
            if (@(Compare-Object $before $after).Count) { throw "Rejected operation changed files: $case" }
        }
        $checks++
    }
    Write-Output "$checks release-tool regression checks passed."
} finally {
    if ($fixture.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $fixture)) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
