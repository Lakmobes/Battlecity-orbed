param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$toolPath = Join-Path $RepoRoot ".tools"
$mgcbExe = Join-Path $toolPath "mgcb.exe"

if (Test-Path $mgcbExe) {
    Write-Host "==> MGCB already installed at $mgcbExe"
    exit 0
}

Write-Host "==> Installing dotnet-mgcb to $toolPath"
New-Item -ItemType Directory -Force -Path $toolPath | Out-Null
dotnet tool install dotnet-mgcb --version 3.8.2.1105 --tool-path $toolPath

if (-not (Test-Path $mgcbExe)) {
    throw "MGCB install failed: $mgcbExe not found"
}

Write-Host "==> MGCB ready: $mgcbExe"
