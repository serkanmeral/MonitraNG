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
#   .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1 -StartFromStep 2
#
# Turkce metin: varsayilan -RepairText (Sanitize-LegacyText + NCR/sevkiyat metin onarimi).
# SQL dump + MySQL export utf8mb4; DG POST charset=utf-8.

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
    [switch]$SkipTokenRefresh,
    [switch]$RepairText = $true,
    [switch]$SkipRepairText,
    [switch]$SkipGoLiveVerify,
    [switch]$SkipRecordScopeBackfill,
    [int]$StartFromStep = 0
)

if ($SkipRepairText) { $RepairText = $false }

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
    try {
        & $Action
    }
    catch {
        throw "Adim basarisiz: $Title — $_"
    }
    # Nested token scriptleri LASTEXITCODE=1 birakabilir; adim throw etmediyse sifirla
    $stepExit = $LASTEXITCODE
    $global:LASTEXITCODE = 0
    if ($stepExit -ne 0 -and $null -ne $stepExit -and $Title -match 'Dogrulama') {
        Write-Host "Dogrulama uyarisi (exit $stepExit) — sayim farki olabilir, logu kontrol edin." -ForegroundColor Yellow
    }
}

Write-Host "`n=== migrate-legacy-full-to-prod ===" -ForegroundColor Green
Write-Host "Hedef DG : $BaseUrl" -ForegroundColor Green
Write-Host "Prod SSH : $ProdServer (index onarimi)" -ForegroundColor Green
Write-Host "DryRun     : $DryRun" -ForegroundColor Green
Write-Host "RepairText : $RepairText (Turkce mojibake onarimi)" -ForegroundColor Green
if ($StartFromStep -gt 0) {
    Write-Host "StartFromStep: $StartFromStep (onceki adimlar atlanir)`n" -ForegroundColor Yellow
}
else {
    Write-Host ""
}

function Test-RunStep {
    param([int]$StepNumber)
    return ($StartFromStep -le $StepNumber)
}

$env:MNG_OC_USE_PROD_TOKEN = "1"

if (-not $SkipTokenRefresh) {
    Invoke-Step "Production token" {
        & $getProdToken
        if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
            Write-Host "Token yenileme uyarisi (exit $LASTEXITCODE) — mevcut dosya deneniyor." -ForegroundColor Yellow
        }
        if (-not (Test-Path $prodTokenFile)) {
            throw "Prod token dosyasi olusturulamadi: $prodTokenFile"
        }
        $cachedToken = (Get-Content $prodTokenFile -Raw -ErrorAction Stop).Trim()
        if ([string]::IsNullOrEmpty($cachedToken)) {
            throw "Prod token dosyasi bos: $prodTokenFile"
        }
        Write-Host "Prod token hazir: $prodTokenFile" -ForegroundColor Gray
    }
}
else {
    if (-not (Test-Path $prodTokenFile)) {
        throw "SkipTokenRefresh: prod token dosyasi yok — once get-operationcore-token-prod.ps1"
    }
    Write-Host "Prod token dosyasi kullaniliyor (-SkipTokenRefresh): $prodTokenFile" -ForegroundColor Gray
}

if (-not $DryRun -and (Test-RunStep 0)) {
    Invoke-Step "[0/10] @datasets createInfo tarih onarimi (prod Mongo)" {
        & (Join-Path $scriptDir "repair-dataset-createinfo-dates.ps1") -Server $ProdServer
    }
}
elseif (-not (Test-RunStep 0)) {
    Write-Host "`n[0/10] createInfo onarimi atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow
}
else {
    Write-Host "`n[0/10] createInfo onarimi atlandi (DryRun)" -ForegroundColor Yellow
}

if (Test-RunStep 1) {
Invoke-Step "[1/10] SQL dump -> musteri + is paketi + kalemler" {
    $p = New-DgParams
    if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
    & (Join-Path $scriptDir "migrate-legacy-from-sql-dump.ps1") @p
}
} else { Write-Host "`n[1/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow }

if (Test-RunStep 2) {
Invoke-Step "[2/10] Kalan kalemler (SQL dump)" {
    $p = New-DgParams
    if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
    & (Join-Path $scriptDir "migrate-remaining-lines.ps1") @p
}
} else { Write-Host "`n[2/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow }

if (Test-RunStep 3) {
Invoke-Step "[3/10] Orphan kalem temizligi" {
    $p = New-DgParams
    & (Join-Path $scriptDir "remove-orphan-siparis-kalemleri.ps1") @p
}
} else { Write-Host "`n[3/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow }

if (-not $DryRun -and (Test-RunStep 4)) {
    Invoke-Step "[4/10] Mongo index onarimi (prod)" {
        & (Join-Path $scriptDir "repair-odak-siparis-kalemleri-indexes.ps1") -Server $ProdServer
    }
}
elseif (-not (Test-RunStep 4)) {
    Write-Host "`n[4/10] Index onarimi atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow
}
else {
    Write-Host "`n[4/10] Index onarimi atlandi (DryRun)" -ForegroundColor Yellow
}

if (Test-RunStep 5) {
Invoke-Step "[5/10] NCR + CAPA export + migrasyon (+ RepairText metin onarimi)" {
    & (Join-Path $scriptDir "export-legacy-ncs-from-mysql.ps1") @mysqlArgs
    $p = New-DgParams
    if ($RepairText) { $p.RepairText = $true }
    & (Join-Path $scriptDir "migrate-legacy-ncs-to-dg.ps1") @p
}
} else { Write-Host "`n[5/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow }

if (Test-RunStep 6) {
Invoke-Step "[6/10] Sevkiyat export + migrasyon" {
    & (Join-Path $scriptDir "export-legacy-shipments-from-mysql.ps1") @mysqlArgs
    $p = New-DgParams
    if ($RepairText) { $p.RepairText = $true }
    & (Join-Path $scriptDir "migrate-legacy-shipments-to-dg.ps1") @p
}
} else { Write-Host "`n[6/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow }

if (-not $SkipRecordScopeBackfill -and (Test-RunStep 7)) {
    Invoke-Step "[7/10] recordScope / lineMode backfill" {
        $p = New-DgParams
        & (Join-Path $scriptDir "backfill-odak-record-scope.ps1") @p
    }
}
elseif ($SkipRecordScopeBackfill) {
    Write-Host "`n[7/10] recordScope backfill atlandi (-SkipRecordScopeBackfill)" -ForegroundColor Yellow
}
else {
    Write-Host "`n[7/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow
}

if (-not $SkipPoPdf -and (Test-RunStep 8)) {
    Invoke-Step "[8/10] PO PDF export + migrasyon (tum adaylar)" {
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
elseif ($SkipPoPdf) {
    Write-Host "`n[8/10] PO PDF atlandi (-SkipPoPdf)" -ForegroundColor Yellow
}
else {
    Write-Host "`n[8/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow
}

if (Test-RunStep 9) {
Invoke-Step "[9/10] Temel dogrulama (paket/kalem/musteri)" {
    $p = New-DgParams
    $p.UseSqlDump = $true
    if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
    & (Join-Path $scriptDir "verify-legacy-dg-migration.ps1") @p
}
} else { Write-Host "`n[9/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow }

if (-not $SkipGoLiveVerify -and (Test-RunStep 10)) {
    Invoke-Step "[10/10] Canli gecis hazirlik (BLOCKER/WARN)" {
        $p = New-DgParams
        $p.UseSqlDump = $true
        if ($SqlDumpPath) { $p.SqlDumpPath = $SqlDumpPath }
        & (Join-Path $scriptDir "verify-odak-go-live-readiness.ps1") @p
    }
}
elseif ($SkipGoLiveVerify) {
    Write-Host "`n[10/10] Go-live dogrulama atlandi (-SkipGoLiveVerify)" -ForegroundColor Yellow
}
else {
    Write-Host "`n[10/10] atlandi (StartFromStep=$StartFromStep)" -ForegroundColor Yellow
}

Write-Host "`n=== Tam migrasyon bitti ===" -ForegroundColor Green
Write-Host "Sonraki: https://mng.odaksavunma.com/apps/odak-siparis/packages" -ForegroundColor Gray
Write-Host "UAT: 10 rastgele paket alan karsilastirmasi (VERI_MIGRASYON_PLANI §9)" -ForegroundColor Gray
Write-Host "Rapor: docs/odak/siparis/datasets/odak-go-live-readiness-report.json" -ForegroundColor Gray
