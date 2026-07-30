#Requires -Version 7.0
<#
.SYNOPSIS
  Smoke-checks a running MngLogsAgent service (health + status CLI).
#>
param(
    [string] $InstallDir = "C:\Program Files\MngLogs\Agent",
    [string] $LocalUiHost = "127.0.0.1",
    [int] $LocalUiPort = 5092
)

$ErrorActionPreference = "Stop"
$ServiceName = "MngLogsAgent"
$exe = Join-Path $InstallDir "MngLogs.Agent.exe"

$svc = Get-Service -Name $ServiceName -ErrorAction Stop
Write-Host "Service: $($svc.Name) Status=$($svc.Status) StartType=$($svc.StartType)"
if ($svc.Status -ne "Running") {
    Write-Error "Service is not Running"
    exit 1
}

$healthUrl = "http://${LocalUiHost}:${LocalUiPort}/health"
Write-Host "GET $healthUrl"
$health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 10
Write-Host ($health | ConvertTo-Json -Compress)

if (Test-Path $exe) {
    Write-Host "CLI status:"
    & $exe status
}

$logDir = Join-Path $env:ProgramData "MngLogs\Agent\logs"
if (Test-Path $logDir) {
    $latest = Get-ChildItem $logDir -Filter "agent-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Host "Latest log: $($latest.FullName) ($([math]::Round($latest.Length/1KB,1)) KB)"
    }
}

Write-Host "Smoke OK"
exit 0
