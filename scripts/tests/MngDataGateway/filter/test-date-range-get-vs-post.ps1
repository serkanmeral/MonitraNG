# Compare GET ?filter= vs POST /query for date range on odak_egitimler
# Usage:
#   $env:DG_BASE_URL = 'http://192.168.20.20:5010'
#   .\scripts\tests\MngDataGateway\filter\test-date-range-get-vs-post.ps1

param(
    [string]$BaseUrl = $(if ($env:DG_BASE_URL) { $env:DG_BASE_URL.TrimEnd('/') } else { 'http://192.168.20.20:5010' }),
    [string]$Dataset = 'odak_egitimler',
    [string]$Year = '2017'
)

$ErrorActionPreference = 'Stop'
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath '..\auth\load-token.ps1'
if (-not (Test-Path $loadTokenScript)) { throw "load-token.ps1 not found: $loadTokenScript" }

$token = & $loadTokenScript
if ([string]::IsNullOrWhiteSpace($token)) { throw 'Token alinamadi.' }

$headers = @{ Authorization = "Bearer $token" }
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Test-PipelineHasDateRange {
    param($Response)
    $json = $Response | ConvertTo-Json -Depth 30 -Compress
    return ($json -match '\$gte') -and ($json -match '\$lte') -and ($json -match 'gerceklesenTarih|ISODate')
}

Write-Host "GET vs POST date range — $BaseUrl / $Dataset / year $Year" -ForegroundColor Cyan

# --- GET ---
$filter = "durum:eq:Tamamlandi,gerceklesenTarih:gte:${Year}-01-01,gerceklesenTarih:lte:${Year}-12-31"
$getUrl = "$BaseUrl/api/v1/data/$Dataset" +
    "?filter=$([uri]::EscapeDataString($filter))&limit=5&skip=0&expand=false&showQuery=true&fields=egitimNo,durum,gerceklesenTarih"

Write-Host "`n[GET] $getUrl" -ForegroundColor Yellow
try {
    $getRes = Invoke-RestMethod -Uri $getUrl -Method GET -Headers $headers
    $getOk = Test-PipelineHasDateRange $getRes.query
    if ($getOk) {
        Write-Host 'GET PASS: pipeline has gerceklesenTarih gte/lte' -ForegroundColor Green
    } else {
        Write-Host 'GET FAIL: pipeline missing date range in $match' -ForegroundColor Red
        ($getRes.query | ConvertTo-Json -Depth 12) | Write-Host
    }
} catch {
    Write-Host "GET ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $getOk = $false
}

# --- POST /query (native match) ---
$postUrl = "$BaseUrl/api/v1/data/$Dataset/query?limit=5&skip=0&expand=false&showQuery=true&fields=egitimNo,durum,gerceklesenTarih"
$body = @{
    match = @{
        durum = 'Tamamlandi'
        gerceklesenTarih = @{
            '$gte' = "${Year}-01-01T00:00:00.000Z"
            '$lte' = "${Year}-12-31T23:59:59.999Z"
        }
    }
} | ConvertTo-Json -Depth 6

Write-Host "`n[POST /query] $postUrl" -ForegroundColor Yellow
Write-Host "Body: $body" -ForegroundColor DarkGray
try {
    $postRes = Invoke-RestMethod -Uri $postUrl -Method POST -Headers $headers -Body $body -ContentType 'application/json'
    $postOk = Test-PipelineHasDateRange $postRes.query
    if ($postOk) {
        Write-Host 'POST PASS: pipeline has gerceklesenTarih gte/lte' -ForegroundColor Green
    } else {
        Write-Host 'POST FAIL: pipeline missing date range in $match' -ForegroundColor Red
        ($postRes.query | ConvertTo-Json -Depth 12) | Write-Host
    }
    if ($postRes -is [System.Array]) {
        Write-Host "POST returned array (legacy showQuery shape), count=$($postRes.Count)" -ForegroundColor Gray
    }
} catch {
    Write-Host "POST ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message -ForegroundColor DarkGray }
    $postOk = $false
}

Write-Host "`nSummary: GET=$getOk POST=$postOk" -ForegroundColor $(if ($getOk -and $postOk) { 'Green' } elseif ($postOk) { 'Yellow' } else { 'Red' })
if (-not $getOk -and $postOk) {
    Write-Host 'Oneri: Raporlama icin POST /query kullanilabilir (GET filter parser deploy/fix bekleniyor).' -ForegroundColor Cyan
}
