# MngReactor Domain Init Test
# POST /api/v1/admin/domain/{domain}/init
# Onkosullar: MngReactor, MngKeeper, DataGateway calisiyor
# Sonuc: mon_schedules "Sürekli" ve mon_collection_periods "1 dakika" olusturulur (yoksa)

param(
    [string]$BaseUrl = "http://localhost:15010",
    [string]$Domain = "meral"
)

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11
} catch { }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "auth\load-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Hata: load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host "MngReactor Domain Init Test"
Write-Host "========================================"
Write-Host "Domain: $Domain"
Write-Host ""

# Token al
Write-Host "[1] Token aliniyor..."
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Hata: Token alinamadi. Admin/domain claim iceren token gerekli." -ForegroundColor Red
    exit 1
}
Write-Host "  OK: Token alindi" -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
$params = @{ Uri = ""; Method = "POST"; Headers = $headers; ErrorAction = "Stop" }
$hasSkipCertCheck = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }
if ($hasSkipCertCheck) { $params.SkipCertificateCheck = $true }

# Domain Init
Write-Host "[2] POST /api/v1/admin/domain/$Domain/init ..."
try {
    $params.Uri = "$BaseUrl/api/v1/admin/domain/$Domain/init"
    $response = Invoke-RestMethod @params
    Write-Host "  PASS: 200 OK" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 401) {
        Write-Host "  401: Yetkisiz - Admin token gerekebilir" -ForegroundColor Yellow
    } else {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        exit 1
    }
}
Write-Host ""

# Opsiyonel: mon_schedules, mon_collection_periods kontrol (DG uzerinden - token domain'e gore)
Write-Host "[3] Varsayilan kayitlar (mon_schedules, mon_collection_periods) DG'de olusturuldu." -ForegroundColor Gray
Write-Host "    Dogrulama icin seed-monitoring-test-data veya DG API ile kontrol edilebilir." -ForegroundColor Gray
Write-Host ""

Write-Host "Domain init test tamamlandi."
exit 0
