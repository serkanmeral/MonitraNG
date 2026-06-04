# NxLog WEC şablonu — Engine wec-batch format doğrulama (Faz 2.4 lab smoke)
# Şablon: docs/odak/monitoring/templates/nxlog-wec-to-engine.conf
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$WecHost = "WEC01.odak.local",
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP NxLog template: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

Write-Host "=== NxLog WEC template format E2E (Faz 2.4) ===" -ForegroundColor Cyan

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$suffix = Get-Random -Maximum 99999

# NxLog XPath EventID setinden mevcut fixture'lar (4624/4625/4720/4728)
$specs = @(
    @{ Fixture = "windows_4625_failed_logon.json"; User = "nxlog_fail_$suffix"; Action = "login_failed" },
    @{ Fixture = "windows_4624_success_logon.json"; User = "nxlog_ok_$suffix"; Action = "login_success" },
    @{ Fixture = "windows_4720_account_created.json"; User = "nxlog_acct_$suffix"; Action = "account_created" },
    @{ Fixture = "windows_4728_group_member_added.json"; User = "nxlog_grp_$suffix"; Action = "group_member_added" }
)

$items = @()
foreach ($spec in $specs) {
    $ev = Read-Fixture $spec.Fixture
    $ev.TimeCreated = $receivedAt
    if ($ev.PSObject.Properties.Name -contains "TargetUserName") { $ev.TargetUserName = $spec.User }
    if ($ev.PSObject.Properties.Name -contains "SubjectUserName") { $ev.SubjectUserName = "NXLOG_ADMIN_$suffix" }
    if ($ev.PSObject.Properties.Name -contains "MemberName") { $ev.MemberName = $spec.User }
    $items += @{
        receivedAt = $receivedAt
        source     = @{ type = "ad"; product = "windows"; host = $WecHost }
        raw        = $ev
    }
}

$body = @{ autoFlush = $true; items = $items } | ConvertTo-Json -Depth 12

Write-Host "`n1) POST wec-batch (NxLog mini-batch x$($items.Count))..." -ForegroundColor Yellow
try {
    $ingest = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/wec-batch" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 90
} catch {
    Skip-Test $_.ErrorDetails.Message
}

Write-Host "   enqueued=$($ingest.enqueued) accepted=$($ingest.accepted) flushed=$($ingest.flushed)" -ForegroundColor Green
if ($ingest.accepted -lt $items.Count) { throw "FAIL: accepted=$($ingest.accepted)" }

Start-Sleep -Seconds 3

Write-Host "`n2) Reactor query (Gateway)..." -ForegroundColor Yellow
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }

$missing = @()
foreach ($spec in $specs) {
    $q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=$($spec.Action)&sourceHost=$WecHost" -Headers $hdr
    $hit = @($q.items) | Where-Object { $_.actorUser -like "*$suffix*" -or $_.targetUser -like "*$suffix*" } | Select-Object -First 1
    if (-not $hit) {
        $missing += $spec.Action
    } else {
        Write-Host "   OK $($spec.Action) parserId=$($hit.parserId)" -ForegroundColor DarkGray
    }
}

if ($missing.Count -gt 0) {
    throw "FAIL: Reactor'da bulunamadi: $($missing -join ', ')"
}

Write-Host "`nOK NxLog WEC template format E2E PASS ($($items.Count) EventID)" -ForegroundColor Green
exit 0
