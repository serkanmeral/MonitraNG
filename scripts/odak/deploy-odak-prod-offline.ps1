# Production OFFLINE deploy (192.168.20.8) — internet gerektirmez (sunucu tarafinda)
#
# Kullanim:
#   .\scripts\odak\deploy-odak-prod-offline.ps1 -Services mngdocument
#   .\scripts\odak\deploy-odak-prod-offline.ps1 -Services mngdocument,mngui
#   .\scripts\odak\deploy-odak-prod-offline.ps1 -Services mngdocument -SkipBuild -ArchivePath .\artifacts\odak-docker\....tar
#
# On kosul (gelistirme makinesi): Docker calisiyor (WSL2) veya hazir .tar archive
# On kosul (prod): sync ile guncel compose + onceki base/3rd-party image'lar yuklu

param(
    [Parameter(Mandatory)]
    [string]$Services,
    [string]$Version = "latest",
    [string]$ArchivePath = "",
    [string]$PathsCsv = "",
    [switch]$SkipSync,
    [switch]$SkipBuild,
    [switch]$SkipDeploy,
    [switch]$BuildOnly,
    [switch]$NoCache,
    [switch]$IncludeMngCommon
)

$ErrorActionPreference = "Stop"
$offlineScript = Join-Path $PSScriptRoot "deploy-odak-offline.ps1"

$params = @{
    Server   = "192.168.20.8"
    Services = $Services
    Target   = "prod"
    Version  = $Version
}
if ($ArchivePath) { $params.ArchivePath = $ArchivePath }
if ($PathsCsv) { $params.PathsCsv = $PathsCsv }
if ($SkipSync) { $params.SkipSync = $true }
if ($SkipBuild) { $params.SkipBuild = $true }
if ($SkipDeploy) { $params.SkipDeploy = $true }
if ($BuildOnly) { $params.BuildOnly = $true }
if ($NoCache) { $params.NoCache = $true }
if ($IncludeMngCommon) { $params.IncludeMngCommon = $true }

& $offlineScript @params
