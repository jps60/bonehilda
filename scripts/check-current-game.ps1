param(
    [Parameter(Mandatory = $true)][string]$PluginPath,
    [Parameter(Mandatory = $true)][string]$ManagedDirectory,
    [Parameter(Mandatory = $true)][string]$BepInExDirectory,
    [Parameter(Mandatory = $true)][string]$JotunnDirectory,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$checker = Join-Path $PSScriptRoot '../tools/CompatibilityCheck'
& $Dotnet restore $checker --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Checker restore failed.' }
& $Dotnet run --project $checker -c Release --no-restore -- $PluginPath $ManagedDirectory $BepInExDirectory $JotunnDirectory
if ($LASTEXITCODE -ne 0) { throw 'Current-game API compatibility check failed.' }
