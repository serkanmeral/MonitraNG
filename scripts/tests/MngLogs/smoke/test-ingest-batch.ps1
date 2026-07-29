# Smoke: POST a tiny ingest batch to MngLogs collector (local or Odak).
# Usage:
#   .\scripts\tests\MngLogs\smoke\test-ingest-batch.ps1
#   .\scripts\tests\MngLogs\smoke\test-ingest-batch.ps1 -BaseUrl http://192.168.20.8:5091 -ApiKey "..."

param(
    [string]$BaseUrl = "http://localhost:5091",
    [string]$Domain = "odak",
    [string]$HostId = "smoke-host-1",
    [string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
Write-Host "Health: $($health | ConvertTo-Json -Compress)"

$body = @{
    domain = $Domain
    hostId = $HostId
    hostname = "SMOKE-WKS"
    events = @(
        @{
            source = "windows-eventlog"
            sourceProduct = "smoke"
            severity = "info"
            message = "MngLogs smoke ingest $(Get-Date -Format o)"
            fields = @{ EventID = 4624; smoke = $true }
        }
    )
} | ConvertTo-Json -Depth 6

$headers = @{ "Content-Type" = "application/json" }
if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
    $headers["X-MngLogs-ApiKey"] = $ApiKey
}

$result = Invoke-RestMethod -Uri "$BaseUrl/api/v1/ingest/batches" -Method Post -Headers $headers -Body $body
Write-Host "Ingest: $($result | ConvertTo-Json -Compress)"
