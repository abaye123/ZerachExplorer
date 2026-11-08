# Build script for ZerachExplorer Single-File Self-Contained Release
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "./publish-standalone"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing ZerachExplorer as a single-file self-contained executable ($Runtime)..." -ForegroundColor Cyan

$dotnetCmd = "dotnet"
if (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe") {
    $dotnetCmd = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
}

& $dotnetCmd publish LiteExplorer/LiteExplorer.csproj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded: $(Resolve-Path "$OutputDir\LiteExplorer.exe")" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
