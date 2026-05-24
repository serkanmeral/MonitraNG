# MngKeeper 1.3.x LDAP milestone — sync + sunucuda build/up
# Kullanım (repo kökünden):
#   $env:ODAK_SSH_PASSWORD = '<odak-ssh-parola>'
#   .\scripts\odak\deploy-keeper-odak.ps1
#   .\scripts\odak\deploy-keeper-odak.ps1 -FullBuild

param(
    [switch]$FullBuild,
    [switch]$SkipSync,
    [string]$Server = "192.168.20.20",
    [string]$User = "odak"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $RepoRoot

if (-not $SkipSync) {
    Write-Host "=== 1/2 Kaynak senkronu (MngKeeper + mng_apps) ===" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "sync-odak-source.ps1") `
        -Server $Server -User $User `
        -Paths "MngKeeper", "ApplicationResources/mng_apps"
}

Write-Host "=== 2/2 Sunucuda mngkeeper build + up ===" -ForegroundColor Cyan
if ($FullBuild) {
    & (Join-Path $PSScriptRoot "deploy-odak-apps.ps1") -Server $Server -User $User -Services mngkeeper -FullBuild
} else {
    & (Join-Path $PSScriptRoot "deploy-odak-apps.ps1") -Server $Server -User $User -Services mngkeeper
}

Write-Host "=== Keeper deploy tamam ===" -ForegroundColor Green
Write-Host "Smoke: http://${Server}:5001/api/version/short" -ForegroundColor Gray
