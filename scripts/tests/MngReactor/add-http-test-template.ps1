# HTTP Collector test için Collectible şablonu ekler.
# Node-RED flow (NodeRed_HttpTestFlow.json) ile birlikte kullanın.
#
# Kullanım: .\add-http-test-template.ps1
# Önce: scripts/tests/MngDataGateway/auth/load-token.ps1 ile token yüklenmiş olmalı
#       setup-monitoring-datasets.ps1 ile mon_collectible_templates oluşturulmuş olmalı

param(
    [string]$BaseUrl = "https://localhost:5040",
    [switch]$UseGateway = $true
)
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "..\MngDataGateway\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. get-token.ps1 ile token alin." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# Node-RED test flow response yapisi:
# { "timestamp": "...", "sensors": { "temperature": 25.5, "humidity": 60, "pressure": 1013.25 },
#   "storage": { "disk": { "usagePercent": 72.5, "freeGB": 125.3 } }, "status": "healthy" }

$collectibles = @(
    @{ code = "temperature";       name = "Sıcaklık (°C)";     data_type = "number"; path = "$.sensors.temperature" },
    @{ code = "humidity";         name = "Nem (%)";            data_type = "number"; path = "$.sensors.humidity" },
    @{ code = "pressure";         name = "Basınç (hPa)";       data_type = "number"; path = "$.sensors.pressure" },
    @{ code = "disk_usage_percent"; name = "Disk kullanım (%)"; data_type = "number"; path = "$.storage.disk.usagePercent" },
    @{ code = "disk_free_gb";     name = "Boş disk (GB)";      data_type = "number"; path = "$.storage.disk.freeGB" },
    @{ code = "status";           name = "Durum";              data_type = "string"; path = "$.status" }
)

$template = @{
    name               = "HTTP - Node-RED Test"
    collection_method  = "HTTP"
    description        = "Node-RED test flow (/api/metrics) ile uyumlu. sensors, storage, status alanları."
    collectibles       = $collectibles
}

$uri = "$BaseUrl$dataPath/mon_collectible_templates"
$body = $template | ConvertTo-Json -Depth 10 -Compress

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)

if ($useCurl) {
    $bodyFile = [System.IO.Path]::GetTempFileName()
    $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
    $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
    Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
    $lines = ($output.Trim() -split "`n")
    $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
    $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
    if ($httpCode -eq "200" -or $httpCode -eq "201") {
        Write-Host "HTTP Node-RED Test sablonu olusturuldu." -ForegroundColor Green
        exit 0
    }
    if ($httpCode -eq "409" -or ($responseBody -match "mevcut|already exists|zaten|duplicate|unique")) {
        Write-Host "Sablon zaten mevcut." -ForegroundColor Yellow
        exit 0
    }
    Write-Host "HATA: HTTP $httpCode" -ForegroundColor Red
    if ($responseBody) { Write-Host $responseBody -ForegroundColor Gray }
    exit 1
}

try {
    $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
    if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $irmParams.SkipCertificateCheck = $true
    }
    $null = Invoke-RestMethod @irmParams
    Write-Host "HTTP Node-RED Test sablonu olusturuldu." -ForegroundColor Green
} catch {
    $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
    if ($_.Exception.Response.StatusCode -eq 409 -or $errMsg -match "mevcut|already exists|zaten|duplicate|unique") {
        Write-Host "Sablon zaten mevcut." -ForegroundColor Yellow
        exit 0
    }
    Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
