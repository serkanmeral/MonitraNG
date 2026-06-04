# SIEM B1 — linux.auth.v1 parser ingest smoke (Engine/Reactor path via HTTP ingest)
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

Write-Host "=== SIEM B1 linux.auth.v1 ingest smoke ===" -ForegroundColor Cyan

$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$body = @{
    items = @(
        @{
            receivedAt = $receivedAt
            source     = @{ type = "endpoint"; product = "linux-syslog"; host = "bastion-b1" }
            raw        = Read-Fixture "linux_sshd_failed_password.syslog.txt"
        }
    )
} | ConvertTo-Json -Depth 8

$ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
Write-Host "   Ingest OK accepted=$($ingest.accepted)" -ForegroundColor Green

Start-Sleep -Seconds 2
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=login_failed&sourceType=endpoint" -Headers $hdr
$match = @($q.items) | Where-Object { $_.parserId -eq "linux.auth.v1" -and $_.networkSrcIp -eq "192.168.50.22" } | Select-Object -First 1
if (-not $match) {
    Write-Host "FAIL: linux.auth.v1 kaydi sorguda bulunamadi (deploy mngreactor gerekebilir)" -ForegroundColor Red
    exit 1
}

Write-Host "   Query OK parserId=$($match.parserId) action=$($match.eventAction) srcIp=$($match.networkSrcIp)" -ForegroundColor Green
Write-Host "`nOK SIEM B1 linux.auth.v1 ingest PASS" -ForegroundColor Green
exit 0
