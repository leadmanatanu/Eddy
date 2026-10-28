<#
.SYNOPSIS
    Generates a 32-byte cryptographically secure secret file for config encryption.
.DESCRIPTION
    Creates Keys\config.secret with 32 random bytes. This secret must never be committed to source control.
#>
[CmdletBinding()]
param(
    [string]$Path,
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Path)) {
    $repoRoot = Split-Path -Parent $scriptDir
    $targetDir = Join-Path $repoRoot "Keys"
    $Path = Join-Path $targetDir "config.secret"
}

$parentDir = Split-Path -Parent $Path
if (-not (Test-Path $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

if ((Test-Path $Path) -and (-not $Force)) {
    Write-Host "Config secret already exists at: $Path (use -Force to overwrite)"
    return
}

$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[System.IO.File]::WriteAllBytes($Path, $bytes)

Write-Host "Successfully generated 32-byte config secret at: $Path"
