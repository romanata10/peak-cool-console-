param(
    [Parameter(Mandatory = $true)][string]$PeakDir,
    [string]$BepInExCore = (Join-Path $PeakDir 'BepInEx/core')
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$game = (Resolve-Path -LiteralPath $PeakDir).Path
$core = (Resolve-Path -LiteralPath $BepInExCore).Path
$project = Join-Path $root 'src/PeakCoolConsole.csproj'
$dll = Join-Path $root 'src/bin/Release/netstandard2.1/PeakCoolConsole.dll'
$zip = Join-Path $root 'PeakCoolConsoleHelper-0.7.0.zip'
& dotnet build $project -c Release "-p:PeakDir=$game" "-p:BepInExCore=$core"
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no package was created.' }
& python (Join-Path $root 'package/build_zip.py') $dll $zip
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
Write-Host "Package ready: $zip"
