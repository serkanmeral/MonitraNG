# Legacy Kalite -> Odak DG — tam migrasyon (production varsayilan)
# Kaynak: SQL dump + yerel legacy MySQL (NCR/sevkiyat/PO export icin)
#
# Onkosul:
#   - %USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql (guncel dump)
#   - Yerel MySQL :3307 kalite DB (dump import edilmis) — NCR/CAPA/sevkiyat/PO export
#   - PO PDF: %USERPROFILE%\kalite-legacy-local\uploads veya kalite-legacy-docker\uploads
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1
#   .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1 -DryRun
#   .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1 -SkipPoPdf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$ProdServer = "192.168.20.8",
    [string]$SqlDumpPath = "",

    [string]$LegacyMySqlHost = "127.0.0.1",
    [int]$LegacyMySqlPort = 3307,
    [string]$LegacyMySqlUser = "root",
    [string]$LegacyMySqlPassword = "",
    [string]$LegacyDatabase = "kalite",
    [string]$LegacyUploadRoot = "",

    [switch]$DryRun,
    [switch]$SkipPoPdf,
    [switch]$SkipTokenRefresh
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path

$prodTokenFile = "$env:TEMP\operationcore_dg_token_prod.txt"
$getProdToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1"

$mysqlArgs = @{
    LegacyMySqlHost     = $LegacyMySqlHost
    LegacyMySqlPort     = $LegacyMySqlPort
    LegacyMySqlUser     = $LegacyMySqlUser
    LegacyMySqlPassword = $LegacyMySqlPassword
    LegacyDatabase      = $LegacyDatabase
}

function New-DgParams {
    $p = @{
        BaseUrl    = $BaseUrl
        UseGateway = $true
    }
    if ($DryRun) { $p.DryRun = $true }
    return $p
}

function Invoke-Step {
    param(
        [string]$Title,
        [scriptblock]$Action
    )
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE -and $Title -notmatch 'Dogrulama') {
        throw "Adim basarisiz: $Title (exit $LASTEXITCODE)"
    }
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE -and $Title -match 'Dogrulama') {
        Write-Host "Dogrulama uyarisi (exit $LASTEXITCODE) — sayim farki olabilir, logu kontrol edin." -ForegroundColor Yellow
    }
}

Write-Host "`n=== migrate-legacy-full-to-prod ===" -ForegroundColor Green
Write-Host "Hedef DG : $BaseUrl" -ForegroundColor Green
Write-Host "Prod SSH : $ProdServer (index onarimi)" -ForegroundColor Green
Write-Host "DryRun   : $DryRun`n" -ForegroundColor Green

$env:MNG_OC_USE_PROD_TOKEN = "1"

if (-not $SkipTokenRefresh) {
    Invoke-Step "Production token" {
        & $getProdToken
        if (-not (Test-Path $prodTokenFile)) {
            throw "Prod token dosyasi olusturulamadi: $prodTokenFile"
        }
        Write-Host "Prod token hazir: $prodTokenFile" -ForegroundColor Gray
    }
}

if (-not $DryRun) {
    Invoke-Step "[0/8] @datasets createInfo tarih onarimi (prod Mongo)" {
        & (Join-Path $scriptDir "repair-dataset-createinfo-dates.ps1") -Server $ProdServer
    }
}
else {
    Write-Host "`n[0/8] createInfo onarimi atlandi (DryRun)" -ForegroundColor Yellow
}

Invoke-Step "[1/8] SQL dump -> musteri + is paketi + kalemler" {
    $p = New-DgParams
    if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
    & (Join-Path $scriptDir "migrate-legacy-from-sql-dump.ps1") @p
}

Invoke-Step "[2/8] Kalan kalemler (SQL dump)" {
    $p = New-DgParams
    if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
    & (Join-Path $scriptDir "migrate-remaining-lines.ps1") @p
}

Invoke-Step "[3/8] Orphan kalem temizligi" {
    $p = New-DgParams
    & (Join-Path $scriptDir "remove-orphan-siparis-kalemleri.ps1") @p
}

if (-not $DryRun) {
    Invoke-Step "[4/8] Mongo index onarimi (prod)" {
        & (Join-Path $scriptDir "repair-odak-siparis-kalemleri-indexes.ps1") -Server $ProdServer
    }
}
else {
    Write-Host "`n[4/8] Index onarimi atlandi (DryRun)" -ForegroundColor Yellow
}

Invoke-Step "[5/8] NCR + CAPA export + migrasyon" {
    & (Join-Path $scriptDir "export-legacy-ncs-from-mysql.ps1") @mysqlArgs
    $p = New-DgParams
    & (Join-Path $scriptDir "migrate-legacy-ncs-to-dg.ps1") @p
}

Invoke-Step "[6/8] Sevkiyat export + migrasyon" {
    & (Join-Path $scriptDir "export-legacy-shipments-from-mysql.ps1") @mysqlArgs
    $p = New-DgParams
    & (Join-Path $scriptDir "migrate-legacy-shipments-to-dg.ps1") @p
}

if (-not $SkipPoPdf) {
    Invoke-Step "[7/8] PO PDF export + migrasyon (tum adaylar)" {
        $exportArgs = @{} + $mysqlArgs
        $exportArgs.All = $true
        & (Join-Path $scriptDir "export-legacy-po-candidates-from-mysql.ps1") @exportArgs

        $pdfArgs = New-DgParams
        $pdfArgs += $mysqlArgs
        $pdfArgs.All = $true
        $pdfArgs.SkipExisting = $true
        if ($LegacyUploadRoot) { $pdfArgs.LegacyUploadRoot = $LegacyUploadRoot }
        & (Join-Path $scriptDir "migrate-legacy-po-pdf-to-dg.ps1") @pdfArgs
    }
}
else {
    Write-Host "`n[7/8] PO PDF atlandi (-SkipPoPdf)" -ForegroundColor Yellow
}

Invoke-Step "[8/8] Dogrulama (SQL dump vs DG)" {
    $p = New-DgParams
    $p.UseSqlDump = $true
    if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
    & (Join-Path $scriptDir "verify-legacy-dg-migration.ps1") @p
}

Write-Host "`n=== Tam migrasyon bitti ===" -ForegroundColor Green
Write-Host "Sonraki: https://mng.odaksavunma.com/apps/odak-siparis/packages" -ForegroundColor Gray
Write-Host "Turkce spot-check: odak_musteriler.unvan ve odak_siparis_kalemleri.description alanlarinda 'i','s','g' kontrolu" -ForegroundColor Gray
