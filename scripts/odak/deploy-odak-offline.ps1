# Odak OFFLINE deploy — gelistirme makinesinde build, sunucuda load + compose up (-NoBuild)
#
# Prod ve test sunuculari internete KAPALI iken kullanilir.
# Sunucuda docker compose build CALISTIRMAZ; image tarball ile gider.
#
# Tipik akis (gelistirme makinesi — internet + Docker gerekir):
#   1) .\scripts\odak\prefetch-odak-docker-base-images.ps1 -IncludeThirdParty   # bir kez
#   2) .\scripts\odak\deploy-odak-prod-offline.ps1 -Services mngdocument
#
# Hazir .tar dosyasi ile (Docker olmadan aktarim):
#   .\scripts\odak\deploy-odak-prod-offline.ps1 -Services mngdocument -SkipBuild -ArchivePath .\artifacts\odak-docker\....tar
#
# Parametreler:
#   -SkipSync     Kod senkronu atlanir
#   -SkipBuild    Yerel build/save atlanir; -ArchivePath veya artifacts klasorundeki son arsiv kullanilir
#   -SkipDeploy   Yalnizca sync + build + upload + docker load
#   -BuildOnly    Sunucuya gonderme; yalnizca local build (+ -Export ile tar uretir)
#
# Ortam:
#   Test: 192.168.20.20  (varsayilan)
#   Prod: deploy-odak-prod-offline.ps1 veya -Server 192.168.20.8 -Target prod

param(
    [string]$Server = "192.168.20.20",
    [Parameter(Mandatory)]
    [string]$Services,
    [string]$Version = "latest",
    [ValidateSet("prod", "test")]
    [string]$Target = "test",
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
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
. (Join-Path $PSScriptRoot "OdakDockerOffline.ps1")

if ($Server -eq $script:OdakProdServer) { $Target = "prod" }
if ($Server -eq $script:OdakTestServer -and $Target -eq "test") { }

$serviceList = Resolve-OdakDockerServiceList -Services @($Services -split ',')
$imageRefs = @($serviceList | ForEach-Object { Get-OdakDockerImageRef -ServiceName $_ -Version $Version })

Write-Host "=== Odak OFFLINE deploy ===" -ForegroundColor Magenta
Write-Host "Sunucu: $Server | Hedef: $Target | Servisler: $($serviceList -join ', ')" -ForegroundColor Cyan

# --- Sync kaynak (compose + servis klasorleri) ---
if (-not $SkipSync -and -not $BuildOnly) {
    $syncScript = Join-Path $PSScriptRoot "sync-odak-source.ps1"
    $syncPaths = if ($PathsCsv) {
        $PathsCsv -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    } else {
        Get-OdakDockerSyncPathsForServices -ServiceNames $serviceList
    }
    Write-Host "Sync: $($syncPaths -join ', ')" -ForegroundColor Cyan
    $syncParams = @{
        Server = $Server
        Paths  = $syncPaths
    }
    if ($IncludeMngCommon) { $syncParams.IncludeMngCommon = $true }
    & $syncScript @syncParams
}

# --- Local build + export ---
$archive = $ArchivePath
if (-not $SkipBuild) {
    Assert-OdakDockerAvailable
    foreach ($svc in $serviceList) {
        Build-OdakDockerServiceImage -ServiceName $svc -Version $Version -Target $Target -NoCache:$NoCache | Out-Null
    }
    if ([string]::IsNullOrWhiteSpace($archive)) {
        $archive = Get-OdakDockerDefaultArchivePath -ServiceNames $serviceList -Version $Version -Server $Server
    }
    Export-OdakDockerImages -ImageRefs $imageRefs -ArchivePath $archive
} else {
    if ([string]::IsNullOrWhiteSpace($archive)) {
        $archive = Get-OdakDockerDefaultArchivePath -ServiceNames $serviceList -Version $Version -Server $Server
    }
    if (-not (Test-Path $archive)) {
        throw "Archive bulunamadi: $archive ( -ArchivePath belirtin veya -SkipBuild kaldirin )"
    }
    Write-Host "Mevcut archive kullaniliyor: $archive" -ForegroundColor Yellow
}

if ($BuildOnly) {
    Write-Host "BuildOnly — sunucuya gonderilmedi." -ForegroundColor Green
    exit 0
}

# --- Upload + docker load ---
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
Import-OdakDockerArchiveRemote -Server $Server -Credential $cred -LocalArchivePath $archive

if ($SkipDeploy) {
    Write-Host "SkipDeploy — container yeniden baslatilmadi." -ForegroundColor Yellow
    exit 0
}

# --- compose up (build yok) ---
$deployScript = Join-Path $PSScriptRoot "deploy-odak-apps.ps1"
$svcCsv = $serviceList -join ','
& $deployScript -Server $Server -Services $svcCsv -NoBuild

Write-Host "=== Offline deploy tamam ===" -ForegroundColor Green
Write-Host "Archive: $archive" -ForegroundColor DarkGray
