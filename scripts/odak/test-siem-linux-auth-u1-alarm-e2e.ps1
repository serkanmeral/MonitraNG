# SIEM — linux.auth.v1 → sec_events → observation → U1 correlation alarm (sshd brute-force)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [int]$Threshold = 3,
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

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM Linux auth U1 brute-force E2E ===" -ForegroundColor Cyan

$srcIp = "10.88.$((Get-Random -Maximum 250)).$((Get-Random -Maximum 250))"
$linuxUser = "e2e_admin_$(Get-Random -Maximum 9999)"
Write-Host "   srcIp=$srcIp user=$linuxUser" -ForegroundColor DarkGray

Write-Host "`n1) U1 correlation rule (threshold=$Threshold, cooldown=0)..." -ForegroundColor Cyan
$ruleName = "U1 Linux E2E $(Get-Date -Format 'HHmmss')"
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = $ruleName
    type             = "correlation"
    matchKey         = "login_failed"
    groupByFields    = @("userId", "srcIp")
    windowMinutes    = 5
    threshold        = $Threshold
    severity         = 7
    cooldownMinutes  = 0
    dedupKeyTemplate = "{ruleId}:{groupKey}"
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id)" -ForegroundColor DarkGray

$linuxRaw = Read-Fixture "linux_sshd_failed_password.syslog.txt"
$linuxLine = ($linuxRaw -replace '192\.168\.50\.22', $srcIp) -replace 'invalid user admin', "invalid user $linuxUser"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

Write-Host "`n2) POST $Threshold x linux.auth.v1 login_failed sec-events..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "endpoint"; product = "linux-syslog"; host = "bastion-linux-u1" }
                raw        = $linuxLine
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 300
}
Write-Host "   Ingest tamam" -ForegroundColor Green

Start-Sleep -Seconds 2
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=3&eventAction=login_failed&sourceType=endpoint" -Headers $hdr
$parsed = @($q.items) | Where-Object { $_.parserId -eq "linux.auth.v1" -and $_.networkSrcIp -eq $srcIp } | Select-Object -First 1
if (-not $parsed) {
    Write-Host "WARN: linux.auth.v1 kaydi sorguda gorunmedi (devam)" -ForegroundColor Yellow
} else {
    Write-Host "   Parse OK parserId=$($parsed.parserId) user=$($parsed.actorUser)" -ForegroundColor DarkGray
}

Write-Host "`n3) Alarm raised bekleniyor..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity) srcIp=$srcIp" -ForegroundColor Green
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host "FAIL: Linux auth U1 correlation alarm bulunamadi" -ForegroundColor Red
    if ($FailIfSkipped) { exit 1 }
    exit 1
}

Write-Host "`nOK SIEM linux.auth.v1 -> observation -> U1 alarm PASS" -ForegroundColor Green
exit 0
