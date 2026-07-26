param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$distRoot = Join-Path $RepoRoot "dist"
$outRoot = Join-Path $distRoot "BattleCity-$Runtime"
$clientOut = Join-Path $outRoot "Client"
$serverOut = Join-Path $outRoot "Server"
$zipPath = Join-Path $distRoot "BattleCity-$Runtime.zip"

$clientProj = Join-Path $RepoRoot "src\BattleCity.Client\BattleCity.Client.csproj"
$hostProj = Join-Path $RepoRoot "src\BattleCity.Server.Host\BattleCity.Server.Host.csproj"
$legacyData = Join-Path $RepoRoot "legacy\data"
$docsRoot = Join-Path $RepoRoot "docs"
$hostingDoc = Join-Path $docsRoot "HOSTING.md"
$statusDoc = Join-Path $docsRoot "PROJECT-STATUS.md"
$deltasDoc = Join-Path $docsRoot "LEGACY-DELTAS.md"
$contribDoc = Join-Path $docsRoot "CONTRIBUTING.md"

Write-Host "==> Cleaning $outRoot"
if (Test-Path $outRoot) {
    Remove-Item $outRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $clientOut | Out-Null
New-Item -ItemType Directory -Force -Path $serverOut | Out-Null

Write-Host "==> Publishing Client ($Runtime, self-contained)"
dotnet publish $clientProj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $clientOut

Write-Host "==> Publishing Server.Host ($Runtime, self-contained)"
dotnet publish $hostProj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $serverOut

Write-Host "==> Copying legacy data next to Server (map + cities)"
$legacyDest = Join-Path $serverOut "legacy\data"
New-Item -ItemType Directory -Force -Path $legacyDest | Out-Null
Copy-Item (Join-Path $legacyData "map.dat") $legacyDest -Force
Copy-Item (Join-Path $legacyData "cities") (Join-Path $legacyDest "cities") -Recurse -Force

Write-Host "==> Copying docs for hosts and contributors"
Copy-Item $hostingDoc (Join-Path $outRoot "HOSTING.md") -Force
Copy-Item $hostingDoc (Join-Path $serverOut "HOSTING.md") -Force
foreach ($doc in @($statusDoc, $deltasDoc, $contribDoc)) {
    if (Test-Path $doc) {
        Copy-Item $doc (Join-Path $outRoot (Split-Path $doc -Leaf)) -Force
    }
}

$readme = @"
Battle City — Windows $Runtime release
=====================================

PLAY WITH FRIENDS
1. Server: open Server\BattleCity.Server.Host.exe → Start → Copy Invite
2. Client: open Client\BattleCity.Client.exe → Play Online
3. Paste the invite address into the Server field on the login screen

See HOSTING.md for LAN, firewall, and Tailscale steps.

CONTINUE DEVELOPMENT
This zip is a binary share. For source, clone the git repo and read:
  PROJECT-STATUS.md  — where the rewrite stands
  LEGACY-DELTAS.md   — differences from the original C++ game
  CONTRIBUTING.md    — build / test / next tasks

Requires .NET 8 SDK to build from source.
"@
Set-Content -Path (Join-Path $outRoot "README.txt") -Value $readme -Encoding UTF8

Write-Host "==> Creating zip $zipPath"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $outRoot "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "Done."
Write-Host "  Folder: $outRoot"
Write-Host "  Zip:    $zipPath"
