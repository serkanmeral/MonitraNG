# Aktif backup testi: system-backup-daily job'unu Scheduler API ile günceller.
# Yerel dotnet run: endpoint Odak IP (mngadmin hostname çözülmez).
param(
    [string]$SchedulerBaseUrl = "http://localhost:5090",
    [string]$MngAdminBaseUrl = "http://192.168.20.20:5080",
    [string]$JobId = "system-backup-daily",
    [string]$CronExpression = "0 0/2 * * * ?",
    [switch]$ProductionCron
)

$ErrorActionPreference = "Stop"

if ($ProductionCron) {
    $CronExpression = "0 45 21 * * ?"
}

Write-Host "Scheduler: $SchedulerBaseUrl" -ForegroundColor Cyan
Write-Host "MngAdmin backup URL: $MngAdminBaseUrl/api/v1/backup/full" -ForegroundColor Cyan
Write-Host "Cron (test): $CronExpression" -ForegroundColor Cyan

$existing = Invoke-RestMethod -Uri "$SchedulerBaseUrl/api/v1/system/jobs/$JobId" -Method Get
if (-not $existing) {
    throw "Job not found: $JobId"
}

$existing.endpointUrl = "$MngAdminBaseUrl/api/v1/backup/full"
$existing.cronExpression = $CronExpression
$existing.isActive = $true
$existing.startDate = $null
$existing.expireDate = $null
$existing.httpMethod = "POST"
if ([string]::IsNullOrWhiteSpace($existing.payload)) {
    $existing.payload = "{}"
}

$updated = Invoke-RestMethod -Uri "$SchedulerBaseUrl/api/v1/system/jobs/$JobId" -Method Put -ContentType "application/json" -Body ($existing | ConvertTo-Json -Depth 10)
Write-Host "Job updated:" -ForegroundColor Green
$updated | Select-Object jobId, isActive, cronExpression, endpointUrl, expireDate, timeoutSeconds | Format-List

Write-Host ""
Write-Host "Beklenen Scheduler loglari (2 dk icinde):" -ForegroundColor Yellow
Write-Host "  Executing HTTP job: system-backup-daily"
Write-Host "  HTTP job execution completed ... ResponseCode: 200"
Write-Host ""
Write-Host "MngAdmin dogrulama:" -ForegroundColor Yellow
Write-Host "  Invoke-RestMethod $MngAdminBaseUrl/api/v1/backup/system -Method Get"
