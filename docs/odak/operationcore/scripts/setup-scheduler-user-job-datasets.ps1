# MngScheduler User Job datasets - domain DG (@scheduled_jobs, @job_executions)
# Onkosul: get-operationcore-token.ps1 (odak_admin veya domain admin)
#
#   .\docs\odak\operationcore\scripts\setup-scheduler-user-job-datasets.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
$loadTokenScript = Join-Path $PSScriptRoot "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$uri = "$BaseUrl/data/api/v1/datasets"

function Ensure-Dataset {
    param(
        [string]$Name,
        [string]$Description
    )

    $body = @{
        Name        = $Name
        Description = $Description
        ForceSchema = $false
        Logging     = "none"
        PublishMode = "none"
        Fields      = @()
    } | ConvertTo-Json -Compress

    Write-Host "POST $uri ($Name, forceSchema=false)" -ForegroundColor Cyan
    try {
        $null = Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -Body $body
        Write-Host "OK - $Name olusturuldu." -ForegroundColor Green
    }
    catch {
        $msg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($msg -match "mevcut|already|duplicate|409") {
            Write-Host "Zaten var - $Name atlandi." -ForegroundColor Yellow
        }
        else {
            Write-Host "HATA ($Name): $msg" -ForegroundColor Red
            exit 1
        }
    }
}

Ensure-Dataset -Name "@scheduled_jobs" -Description "MngScheduler domain user jobs (OperationCore + domain cron)"
Ensure-Dataset -Name "@job_executions" -Description "MngScheduler domain user job execution history"

Write-Host "Tamam. Schedule kaydi sonrasi MO sync-scheduler calistirin veya UI'dan kaydedin." -ForegroundColor Cyan
