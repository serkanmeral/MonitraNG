# Test: LINE-ACTIVITY-STD tasarim guncellemesi (build + replace seed + publish).
#
# Published sablonlar WOPI ile yazilamaz; bu script -Replace ile yeniden olusturur.
#
# Kullanim:
#   .\docs\odak\document_intelligence\scripts\deploy-line-activity-design-test.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$WopiHost = "http://192.168.20.20:5095",
    [switch]$SkipPublish = $false,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "=== Activity tasarim deploy (test) ===" -ForegroundColor Cyan

& (Join-Path $scriptDir "build-line-activity-seed-docx.ps1")

$seedArgs = @{
    BaseUrl = $BaseUrl
    Replace = $true
}
if ($WhatIf) { $seedArgs.WhatIf = $true }
& (Join-Path $scriptDir "seed-designer-template-line-activity-standard.ps1") @seedArgs
if ($WhatIf) { exit 0 }

# Draft sablona footer tablo WOPI ile yeniden uygula (page-structure sonrasi)
$updateArgs = @{
    BaseUrl = $BaseUrl
    WopiHost = $WopiHost
    SkipBuild = $true
}
& (Join-Path $scriptDir "update-line-activity-template-test.ps1") @updateArgs

if (-not $SkipPublish) {
    & (Join-Path $scriptDir "patch-line-activity-standard-test.ps1") -BaseUrl $BaseUrl
}

Write-Host "=== Deploy tamam ===" -ForegroundColor Green
