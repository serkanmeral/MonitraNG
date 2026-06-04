# SIEM U2 — fail→success login sequence (sec_events → sequence alarm)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [int]$FailCount = 3
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"
$reactor = "$Gateway/reactor/api/v1/ingest/sec-events"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM U2 fail->success sequence E2E ===" -ForegroundColor Cyan

Write-Host "`n1) U2 sequence rule (failCount=$FailCount, cooldown=0)..." -ForegroundColor Cyan
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U2 SIEM E2E $(Get-Date -Format 'HHmmss')"
    type             = "sequence"
    matchKey         = "login_success_after_failures"
    groupByFields    = @("userId", "srcIp")
    severity         = 8
    cooldownMinutes  = 0
    dedupKeyTemplate = "{ruleId}:{groupKey}"
    sequenceSteps    = @(
        @{ matchKey = "login_failed"; minCount = $FailCount; withinMinutes = 10 },
        @{ matchKey = "login_success"; withinMinutesAfterFirst = 15 }
    )
} | ConvertTo-Json -Depth 6)
Write-Host "   ruleId=$($rule.id)" -ForegroundColor DarkGray

$failRaw = Read-Fixture "windows_4625_failed_logon.json"
$successRaw = Read-Fixture "windows_4624_success_logon.json"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$userSuffix = Get-Random
$srcIp = "10.88.0.$((Get-Random -Minimum 10 -Maximum 250))"

function Send-SecEvent([string]$FixtureRaw, [string]$HostName) {
    $obj = $FixtureRaw | ConvertFrom-Json
    $obj.TimeCreated = $receivedAt
    $obj.TargetUserName = "u2-e2e-$userSuffix"
    $obj.IpAddress = $srcIp
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "ad"; product = "windows"; host = $HostName }
                raw        = $obj
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
}

Write-Host "`n2) POST $FailCount x login_failed..." -ForegroundColor Yellow
for ($i = 0; $i -lt $FailCount; $i++) {
    Send-SecEvent $failRaw "dc01"
    Start-Sleep -Milliseconds 400
}

Write-Host "`n3) POST login_success..." -ForegroundColor Yellow
Send-SecEvent $successRaw "dc01"
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n4) Sequence alarm bekleniyor (severity>=8)..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=8" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        $ctx = $match.context
        $ctxKey = if ($ctx.key) { $ctx.key } else { $ctx.Key }
        $trigger = if ($ctx.triggerKey) { $ctx.triggerKey } else { $ctx.TriggerKey }
        Write-Host "   Alarm raised: $($match.id) key=$ctxKey trigger=$trigger srcIp=$($ctx.srcIp)" -ForegroundColor Green
        if ($ctxKey -ne "login_success_after_failures") {
            Write-Host "FAIL: context.key beklenen login_success_after_failures, gelen $ctxKey" -ForegroundColor Red
            exit 1
        }
        if ($trigger -ne "login_success") {
            Write-Host "FAIL: context.triggerKey beklenen login_success, gelen $trigger" -ForegroundColor Red
            exit 1
        }
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host "FAIL: U2 sequence alarm bulunamadi (mngalarm-worker sequence tipi deploy edildi mi?)" -ForegroundColor Red
    exit 1
}

Write-Host "`nOK SIEM U2 login_failed* -> login_success -> sequence alarm PASS" -ForegroundColor Green
exit 0
