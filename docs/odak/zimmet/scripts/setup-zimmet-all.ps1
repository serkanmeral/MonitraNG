# Zimmet — tam kurulum (F0 → F3 + demo)
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\setup-zimmet-all.ps1
#   .\docs\odak\zimmet\scripts\setup-zimmet-all.ps1 -SkipSchema
#   .\docs\odak\zimmet\scripts\setup-zimmet-all.ps1 -SeedDemo

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$UseGateway = $true,
    [switch]$SkipSchema = $false,
    [switch]$SkipForms = $false,
    [switch]$SkipMasterSeed = $false,
    [switch]$SkipOcSeed = $false,
    [switch]$SeedDemo = $true,
    [switch]$ReloadMetadataCache = $true,
    [switch]$PatchSideMenu = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "`n########################################" -ForegroundColor Magenta
Write-Host "# Zimmet — tam kurulum" -ForegroundColor Magenta
Write-Host "########################################`n" -ForegroundColor Magenta

$setupArgs = @{
    BaseUrl     = $BaseUrl
    UseGateway  = $UseGateway
    SkipSchema  = $SkipSchema
    SkipForms   = $SkipForms
}
& (Join-Path $scriptDir "setup-zimmet-datasets-and-forms.ps1") @setupArgs
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipMasterSeed) {
    & (Join-Path $scriptDir "seed-zimmet-master-data.ps1") -BaseUrl $BaseUrl -UseGateway:$UseGateway
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SkipOcSeed) {
    $ocArgs = @{
        BaseUrl              = $BaseUrl
        MoBaseUrl            = $MoBaseUrl
        UseGateway           = $UseGateway
        SeedDemo             = $SeedDemo
        ReloadMetadataCache  = $ReloadMetadataCache
    }
    & (Join-Path $scriptDir "seed-operation-core-zimmet.ps1") @ocArgs
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($PatchSideMenu) {
    & (Join-Path $scriptDir "patch-zimmet-side-menu.ps1") -BaseUrl $BaseUrl
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n########################################" -ForegroundColor Magenta
Write-Host "# Zimmet kurulum tamamlandi" -ForegroundColor Magenta
Write-Host "########################################" -ForegroundColor Magenta
Write-Host "AF:  /apps/automated-forms/view/zimmet-demirbaslar-form" -ForegroundColor Cyan
Write-Host "OC:  /apps/operation-core/workspace" -ForegroundColor Cyan
