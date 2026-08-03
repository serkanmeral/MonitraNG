#Requires -Version 7.0
#Requires -RunAsAdministrator
# Point installed Windows MngLogs agent at Odak PROD LogCollector (default).
param(
    [string]$CollectorUrl = "http://192.168.20.8:5091"
)

$ErrorActionPreference = "Stop"
$log = Join-Path $env:TEMP "mnglogs-retarget-collector.log"
function Log([string]$m) {
    $line = "$(Get-Date -Format o) $m"
    Add-Content -Path $log -Value $line
    Write-Host $line
}

$exe = "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe"
if (-not (Test-Path $exe)) { throw "Agent not installed: $exe" }

Log "Retarget collector -> $CollectorUrl"
& $exe config set --collector $CollectorUrl
if ($LASTEXITCODE -ne 0) { throw "config set failed exit=$LASTEXITCODE" }

Restart-Service MngLogsAgent -Force
Start-Sleep -Seconds 5
$svc = Get-Service MngLogsAgent
Log "Service status=$($svc.Status)"
& $exe status 2>&1 | ForEach-Object { Log "$_" }

try {
    $st = Invoke-RestMethod "http://127.0.0.1:5092/api/status" -TimeoutSec 10
    Log "live collectorBaseUrl=$($st.collectorBaseUrl) hostId=$($st.hostId)"
} catch {
    Log "status API: $($_.Exception.Message)"
}

Log "DONE log=$log"
