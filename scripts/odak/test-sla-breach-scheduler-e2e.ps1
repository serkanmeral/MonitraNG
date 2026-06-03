# SLA breach scan → MngScheduler cron E2E
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$WorkspaceId = "f414462a-cd9e-427e-87e8-3cdff0502325"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$mo = "$Gateway/operations/api/v1"
$sched = "$Gateway/scheduler/api/v1"

$jobId = "oc-sla-scan-$WorkspaceId"
$cron = "0/30 * * * * ?"

Write-Host "1) POST sync-scheduler (cron=$cron)..." -ForegroundColor Cyan
$sync = Invoke-RestMethod -Uri "$mo/sla/sync-scheduler?workspaceId=$WorkspaceId" -Method POST -Headers $hdr -Body (@{
    cronExpression = $cron
    isActive       = $true
} | ConvertTo-Json)
Write-Host "   jobId=$($sync.schedulerJobId) created=$($sync.created) updated=$($sync.updated)" -ForegroundColor Gray

if ($sync.schedulerJobId -ne $jobId) {
    Write-Host "WARN: expected jobId $jobId got $($sync.schedulerJobId)" -ForegroundColor Yellow
}

Write-Host "2) JobSync bekleniyor (40s)..." -ForegroundColor DarkGray
Start-Sleep -Seconds 40

Write-Host "3) Scheduler user job kontrol..." -ForegroundColor Cyan
$job = Invoke-RestMethod -Uri "$sched/user/jobs/$jobId" -Headers $hdr
Write-Host "   name=$($job.name) active=$($job.isActive) cron=$($job.cronExpression)" -ForegroundColor Gray
if (-not $job.isActive) { throw "Scheduler job pasif" }
if ($job.endpointUrl -notlike "*workspaceId=*$WorkspaceId*") { throw "endpointUrl workspaceId icermiyor" }

Write-Host "4) Cron tetik bekleniyor (45s)..." -ForegroundColor Yellow
Start-Sleep -Seconds 45

$executions = Invoke-RestMethod -Uri "$sched/user/jobs/$jobId/executions?limit=3" -Headers $hdr
$count = @($executions).Count
Write-Host "   executions=$count" -ForegroundColor Gray

if ($count -eq 0) {
    Write-Host "FAIL: scheduler execution yok (JobSync/cron kontrol edin)" -ForegroundColor Red
    exit 1
}

$last = $executions[0]
Write-Host "   last status=$($last.status) code=$($last.responseCode)" -ForegroundColor Gray

if ($last.status -eq "success" -and $last.responseCode -ge 200 -and $last.responseCode -lt 300) {
    Write-Host "OK: SLA breach scan scheduler E2E passed." -ForegroundColor Green
    exit 0
}

Write-Host "FAIL: execution status=$($last.status) code=$($last.responseCode)" -ForegroundColor Red
if ($last.errorMessage) { Write-Host $last.errorMessage -ForegroundColor Red }
exit 1
