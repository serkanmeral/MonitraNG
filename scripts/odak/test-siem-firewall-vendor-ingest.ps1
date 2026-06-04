# SIEM B1 — firewall.vendor.v1 parser ingest smoke (FortiGate + PAN-OS + Cisco ASA)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [ValidateSet("fortigate", "pan-os", "cisco-asa", "all")]
    [string]$Vendor = "all"
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

function Test-VendorIngest([string]$Name, [hashtable]$Source, [string]$Fixture, [string]$EventAction, [string]$SrcIp) {
    Write-Host "`n--- $Name ---" -ForegroundColor Cyan
    $receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = $Source
                raw        = Read-Fixture $Fixture
            }
        )
    } | ConvertTo-Json -Depth 8

    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Write-Host "   Ingest OK accepted=$($ingest.accepted)" -ForegroundColor Green

    Start-Sleep -Seconds 2
    $product = $Source.product
    $q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=$EventAction&sourceProduct=$product" -Headers $hdr
    $match = @($q.items) | Where-Object {
        $_.parserId -eq "firewall.vendor.v1" -and $_.networkSrcIp -eq $SrcIp
    } | Select-Object -First 1
    if (-not $match) {
        Write-Host "FAIL: firewall.vendor.v1 kaydi bulunamadi (product=$product srcIp=$SrcIp)" -ForegroundColor Red
        exit 1
    }
    Write-Host "   Query OK parserId=$($match.parserId) action=$($match.eventAction) srcIp=$($match.networkSrcIp)" -ForegroundColor Green
}

Write-Host "=== SIEM B1 firewall.vendor.v1 ingest smoke ===" -ForegroundColor Cyan

if ($Vendor -eq "fortigate" -or $Vendor -eq "all") {
    Test-VendorIngest "FortiGate deny" @{
        type = "firewall"; product = "fortigate"; host = "FGT-ODAK"
    } "fortigate_traffic_deny.syslog.txt" "denied_flow" "203.0.113.5"
}

if ($Vendor -eq "pan-os" -or $Vendor -eq "all") {
    Test-VendorIngest "PAN-OS CEF deny" @{
        type = "firewall"; product = "pan-os"; host = "PA-ODAK"
    } "panw_traffic_deny.syslog.txt" "denied_flow" "203.0.113.15"
}

if ($Vendor -eq "cisco-asa" -or $Vendor -eq "all") {
    Test-VendorIngest "Cisco ASA deny" @{
        type = "firewall"; product = "cisco-asa"; host = "ASA-ODAK"
    } "cisco_asa_traffic_deny.syslog.txt" "denied_flow" "203.0.113.25"
}

Write-Host "`nOK SIEM B1 firewall.vendor.v1 ingest PASS" -ForegroundColor Green
exit 0
