param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$AllSprites
)

$ErrorActionPreference = "Stop"

$legacyData = Join-Path $RepoRoot "legacy\data"
$spriteOutput = Join-Path $RepoRoot "src\BattleCity.Client\Content\Sprites"
$mapOutput = Join-Path $RepoRoot "src\BattleCity.Client\Content\Data"
$audioOutput = Join-Path $RepoRoot "src\BattleCity.Client\Content\Audio"
$bmpToPngProject = Join-Path $RepoRoot "tools\BmpToPng\BmpToPng.csproj"
$mapImporterProject = Join-Path $RepoRoot "tools\MapDatImporter\MapDatImporter.csproj"
$bmpToPngDll = Join-Path $RepoRoot "tools\BmpToPng\bin\Release\net8.0\BmpToPng.dll"
$mapImporterDll = Join-Path $RepoRoot "tools\MapDatImporter\bin\Release\net8.0\MapDatImporter.dll"

Write-Host "==> Converting terrain BMPs to PNG"
dotnet build $bmpToPngProject -c Release | Out-Null
$bmpArgs = @(
    "exec", $bmpToPngDll, "--",
    "--input", $legacyData,
    "--output", $spriteOutput
)
if ($AllSprites) {
    $bmpArgs += "--all"
}
dotnet @bmpArgs

Write-Host "==> Copying legacy WAV effects"
New-Item -ItemType Directory -Force -Path $audioOutput | Out-Null
Copy-Item (Join-Path $legacyData "wav\*.wav") $audioOutput -Force
Copy-Item (Join-Path $legacyData "cloak.wav") $audioOutput -Force
Copy-Item (Join-Path $legacyData "flare.wav") $audioOutput -Force

Write-Host "==> Importing map.dat to JSON"
dotnet build $mapImporterProject -c Release | Out-Null
dotnet exec $mapImporterDll -- `
    --input (Join-Path $legacyData "map.dat") `
    --output $mapOutput

Write-Host "==> Building MonoGame content"
dotnet tool restore | Out-Null
dotnet build (Join-Path $RepoRoot "src\BattleCity.Client\BattleCity.Client.csproj") -c Release

Write-Host "Content pipeline complete."
