# SIEM B1 — firewall.vendor.v1 parser ingest smoke (FortiGate pilot, Reactor HTTP ingest)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$reactor = "$Gateway/reactor/api/v1/ingest/sec-events"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"

function Read-Fixture([string]$Name) {
    return (Get-Content -Path (Join-Path $fixtureDir $Name) -Raw).TrimEnd()
}

Write-Host "=== SIEM B1 firewall.vendor.v1 (FortiGate) ingest smoke ===" -ForegroundColor Cyan

$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$body = @{
    items = @(
        @{
            receivedAt = $receivedAt
            source     = @{ type = "firewall"; product = "fortigate"; host = "FGT-ODAK" }
            raw        = Read-Fixture "fortigate_traffic_deny.syslog.txt"
        }
    )
} | ConvertTo-Json -Depth 8

$ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
Write-Host "   Ingest OK accepted=$($ingest.accepted)" -ForegroundColor Green

Start-Sleep -Seconds 2
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=denied_flow&sourceProduct=fortigate" -Headers $hdr
$match = @($q.items) | Where-Object { $_.parserId -eq "firewall.vendor.v1" -and $_.networkSrcIp -eq "203.0.113.5" } | Select-Object -First 1
if (-not $match) {
    Write-Host "FAIL: firewall.vendor.v1 kaydi sorguda bulunamadi (deploy mngreactor gerekebilir)" -ForegroundColor Red
    exit 1
}

Write-Host "   Query OK parserId=$($match.parserId) action=$($match.eventAction) srcIp=$($match.networkSrcIp) dstPort=$($match.networkDstPort)" -ForegroundColor Green
Write-Host "`nOK SIEM B1 firewall.vendor.v1 ingest PASS" -ForegroundColor Green
exit 0
