# SIEM B1 — windows.security.extended.v1 parser ingest smoke (4720 / 4728 / 5136)
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
    return (Get-Content -Path (Join-Path $fixtureDir $Name) -Raw).TrimEnd() | ConvertFrom-Json
}

function Test-ExtendedIngest([string]$Name, [string]$Fixture, [string]$EventAction) {
    Write-Host "`n--- $Name ---" -ForegroundColor Cyan
    $rawObj = Read-Fixture $Fixture
    $receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "ad"; product = "windows"; host = "DC01-ODAK" }
                raw        = $rawObj
            }
        )
    } | ConvertTo-Json -Depth 8

    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Write-Host "   Ingest OK accepted=$($ingest.accepted)" -ForegroundColor Green

    Start-Sleep -Seconds 2
    $q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=$EventAction&sourceProduct=windows" -Headers $hdr
    $match = @($q.items) | Where-Object {
        $_.parserId -eq "windows.security.extended.v1" -and $_.eventCode -eq "$($rawObj.EventID)"
    } | Select-Object -First 1
    if (-not $match) {
        Write-Host "FAIL: windows.security.extended.v1 kaydi bulunamadi (EventID=$($rawObj.EventID))" -ForegroundColor Red
        exit 1
    }
    Write-Host "   Query OK parserId=$($match.parserId) action=$($match.eventAction) code=$($match.eventCode)" -ForegroundColor Green
}

Write-Host "=== SIEM B1 windows.security.extended.v1 ingest smoke ===" -ForegroundColor Cyan

Test-ExtendedIngest "4720 account_created" "windows_4720_account_created.json" "account_created"
Test-ExtendedIngest "4728 group_member_added" "windows_4728_group_member_added.json" "group_member_added"
Test-ExtendedIngest "5136 directory_object_modified" "windows_5136_directory_modified.json" "directory_object_modified"

Write-Host "`nOK SIEM B1 windows.security.extended.v1 ingest PASS" -ForegroundColor Green
exit 0
