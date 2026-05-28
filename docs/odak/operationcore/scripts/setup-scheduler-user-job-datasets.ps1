# MngScheduler User Job dataset — domain DG (@scheduled_jobs)
# Önkoşul: get-operationcore-token.ps1 (odak_admin veya domain admin)
#
#   .\docs\odak\operationcore\scripts\setup-scheduler-user-job-datasets.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
$loadTokenScript = Join-Path $PSScriptRoot "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alınamadı." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$body = @{
    Name        = "@scheduled_jobs"
    Description = "MngScheduler domain user jobs (OperationCore + domain cron)"
    ForceSchema   = $false
    Logging       = "none"
    PublishMode   = "none"
    Fields        = @()
} | ConvertTo-Json -Compress

$uri = "$BaseUrl/data/api/v1/datasets"
Write-Host "POST $uri (@scheduled_jobs, forceSchema=false)" -ForegroundColor Cyan

try {
    $null = Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -Body $body
    Write-Host "OK — @scheduled_jobs oluşturuldu." -ForegroundColor Green
}
catch {
    $msg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
    if ($msg -match "mevcut|already|duplicate|409") {
        Write-Host "Zaten var — atlandı." -ForegroundColor Yellow
    }
    else {
        Write-Host "HATA: $msg" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Tamam. Schedule kaydı sonrası MO sync-scheduler çalıştırın veya UI'dan kaydedin." -ForegroundColor Cyan
