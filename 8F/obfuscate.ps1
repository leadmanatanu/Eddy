param (
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot
$releaseDir = Join-Path $projectDir "bin\Release\net8.0-windows"
$obfuscatedDir = Join-Path $releaseDir "Obfuscated"
$configFile = Join-Path $projectDir "Obfuscar.xml"

Write-Host "=== 8F Assembly Obfuscation Pipeline ===" -ForegroundColor Cyan

# 1. Build project in Release mode if not skipped
if (-not $SkipBuild) {
    Write-Host "[1/4] Building 8F in Release mode..." -ForegroundColor Yellow
    dotnet build "$projectDir\8F.csproj" -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
} else {
    Write-Host "[1/4] Skipping build as requested..." -ForegroundColor DarkGray
}

# 2. Verify prerequisites
if (-not (Test-Path (Join-Path $releaseDir "8F.dll"))) {
    throw "Build output not found at: $releaseDir\8F.dll"
}

if (-not (Get-Command obfuscar.console -ErrorAction SilentlyContinue)) {
    throw "obfuscar.console was not found in PATH. Install via 'dotnet tool install --global Obfuscar.GlobalTool'."
}

# 3. Run Obfuscar
Write-Host "[2/4] Running Obfuscar..." -ForegroundColor Yellow
Push-Location $projectDir
try {
    obfuscar.console "$configFile"
    if ($LASTEXITCODE -ne 0) {
        throw "Obfuscar failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

# 4. Copy runtime host and dependency files to obfuscated directory
Write-Host "[3/4] Preparing self-contained obfuscated output..." -ForegroundColor Yellow
if (-not (Test-Path $obfuscatedDir)) {
    New-Item -ItemType Directory -Path $obfuscatedDir -Force | Out-Null
}

Get-ChildItem -Path $releaseDir | ForEach-Object {
    if ($_.Name -ne "Obfuscated") {
        if ($_.PSIsContainer) {
            Copy-Item -Path $_.FullName -Destination $obfuscatedDir -Recurse -Force
        } elseif ($_.Name -ne "8F.dll" -and $_.Name -ne "Mapping.txt") {
            Copy-Item -Path $_.FullName -Destination $obfuscatedDir -Force
        }
    }
}

Write-Host "[4/4] Obfuscation completed successfully!" -ForegroundColor Green
Write-Host "Obfuscated output: $obfuscatedDir" -ForegroundColor Cyan
Write-Host "Mapping log:       $obfuscatedDir\Mapping.txt" -ForegroundColor Cyan
