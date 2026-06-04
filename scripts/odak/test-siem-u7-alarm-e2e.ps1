# SIEM U7 — baseline sonrası yeni src→dst akışı (new_flow) → correlation alarm
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [switch]$ResetBaseline,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"
$reactor = "$Gateway/reactor/api/v1/ingest/sec-events"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"
$ruleFixture = Join-Path $fixtureDir "alarm_rules/u7_new_flow.json"

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

function Invoke-BaselineReset {
    param([object]$SshSession)
    . (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
    $mongoJs = @"
const dbName = 'mng_$Domain';
const coll = db.getSiblingDB(dbName).sec_flow_baseline;
coll.deleteMany({});
coll.insertOne({ _id: '__meta__', learningComplete: false, uniquePairCount: 0, updatedAt: new Date() });
print(JSON.stringify({ ok: true, db: dbName }));
"@
    $r = Invoke-OdakMongoJsonEval -SshSession $SshSession -JavaScript $mongoJs
    $text = ($r.Output -join "").Trim()
    if ($text -notmatch '"ok"\s*:\s*true') {
        Write-Host "   Baseline reset uyarisi: $text" -ForegroundColor Yellow
    } else {
        Write-Host "   Baseline sifirlandi ($Domain)" -ForegroundColor DarkGray
    }
}

Write-Host "=== SIEM U7 new_flow (baseline sonrasi yeni src->dst) E2E ===" -ForegroundColor Cyan

if ($ResetBaseline) {
    Write-Host "`n0) Baseline reset (SSH)..." -ForegroundColor Cyan
    try {
        Import-Module Posh-SSH -Force -ErrorAction Stop
        . (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
        Initialize-OdakSshEnvironment -Server $Server
        $cred = Get-OdakSshCredential -User $User -Server $Server
        $session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
        Invoke-BaselineReset -SshSession $session
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    } catch {
        Write-Host "   SKIP baseline reset: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

$seedSrc = "10.90.$((Get-Random -Minimum 1 -Maximum 250)).1"
$seedDst = "10.90.$((Get-Random -Minimum 1 -Maximum 250)).100"
$newSrc = "10.91.$((Get-Random -Minimum 1 -Maximum 250)).2"
$newDst = "10.91.$((Get-Random -Minimum 1 -Maximum 250)).200"

$firewallRaw = Read-Fixture "firewall_deny.syslog.txt"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

function New-FirewallLine([string]$Src, [string]$Dst) {
    return ($firewallRaw -replace 'SRC=203\.0\.113\.5', "SRC=$Src") -replace 'DST=10\.0\.0\.10', "DST=$Dst"
}

Write-Host "`n1) Baseline ogrenme — seed pair ($seedSrc -> $seedDst)..." -ForegroundColor Yellow
$seedBody = @{
    items = @(
        @{
            receivedAt = $receivedAt
            source     = @{ type = "firewall"; product = "generic-syslog"; host = "fw-u7-seed" }
            raw        = New-FirewallLine $seedSrc $seedDst
        }
    )
} | ConvertTo-Json -Depth 8
$seedIngest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $seedBody -TimeoutSec 60
if ($seedIngest.accepted -lt 1) { throw "Seed ingest basarisiz: $($seedIngest | ConvertTo-Json -Compress)" }
Write-Host "   Seed tamam (ogrenme fazı -> denied_flow)" -ForegroundColor Green

Write-Host "`n2) U7 correlation rule (matchKey=new_flow, threshold=1, cooldown=0)..." -ForegroundColor Cyan
$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$ruleName = "U7 SIEM E2E $(Get-Date -Format 'HHmmss')"
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = $ruleName
    type             = $ruleTemplate.type
    matchKey         = $ruleTemplate.matchKey
    groupByFields    = @($ruleTemplate.groupByFields)
    windowMinutes    = $ruleTemplate.windowMinutes
    threshold        = 1
    severity         = $ruleTemplate.severity
    cooldownMinutes  = 0
    dedupKeyTemplate = $ruleTemplate.dedupKeyTemplate
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id) matchKey=$($ruleTemplate.matchKey)" -ForegroundColor DarkGray

Write-Host "`n3) POST yeni src->dst ($newSrc -> $newDst) — new_flow bekleniyor..." -ForegroundColor Yellow
$newBody = @{
    items = @(
        @{
            receivedAt = $receivedAt
            source     = @{ type = "firewall"; product = "generic-syslog"; host = "fw-u7-new" }
            raw        = New-FirewallLine $newSrc $newDst
        }
    )
} | ConvertTo-Json -Depth 8
$newIngest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $newBody -TimeoutSec 60
if ($newIngest.accepted -lt 1) { throw "New flow ingest basarisiz: $($newIngest | ConvertTo-Json -Compress)" }
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n4) Alarm raised bekleniyor (severity>=7)..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        $ctx = $match.context
        $ctxSrc = if ($ctx.srcIp) { $ctx.srcIp } else { $ctx.SrcIp }
        $ctxDst = if ($ctx.dstIp) { $ctx.dstIp } else { $ctx.DstIp }
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity) srcIp=$ctxSrc dstIp=$ctxDst" -ForegroundColor Green
        if ($ctxSrc -ne $newSrc -or $ctxDst -ne $newDst) {
            Write-Host "FAIL: context src/dst beklenen $newSrc->$newDst, gelen $ctxSrc->$ctxDst" -ForegroundColor Red
            exit 1
        }
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host "FAIL: U7 new_flow correlation alarm bulunamadi" -ForegroundColor Red
    if ($FailIfSkipped) { exit 1 }
    exit 1
}

Write-Host "`nOK SIEM U7 baseline -> new_flow -> alarm PASS" -ForegroundColor Green
exit 0
