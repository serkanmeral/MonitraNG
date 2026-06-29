# Test OFFLINE deploy (192.168.20.20) — deploy-odak-offline.ps1 wrapper

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
    Server   = "192.168.20.20"
    Services = $Services
    Target   = "test"
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
