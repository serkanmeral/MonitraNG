# Smoke: start Agent locally (console), hit local UI status.
# Prerequisite: collector optional; agent still shows status if collector down.
#
#   cd MngLogs/Presentation/MngLogs.Agent; dotnet run
#   .\scripts\tests\MngLogs\smoke\test-agent-local-ui.ps1

param(
    [string]$BaseUrl = "http://127.0.0.1:5092"
)

$ErrorActionPreference = "Stop"
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
Write-Host "Health: $($health | ConvertTo-Json -Compress)"

$status = Invoke-RestMethod -Uri "$BaseUrl/api/status" -Method Get
Write-Host "Status: hostId=$($status.hostId) domain=$($status.domain) pending=$($status.queuePending) collector=$($status.collectorBaseUrl)"

$config = Invoke-RestMethod -Uri "$BaseUrl/api/config" -Method Get
Write-Host "Config: collector=$($config.system.collectorBaseUrl) heartbeatSec=$($config.policy.heartbeatIntervalSeconds)"
