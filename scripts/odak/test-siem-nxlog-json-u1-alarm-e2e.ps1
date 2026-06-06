# SIEM — NxLog JSON (UDP/Engine) → windows.nxlog-json.v1 → U1 correlation alarm
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 1514,
    [string]$Domain = "odak",
    [string]$SourceHost = "TERMINAL.odak.local",
    [int]$Threshold = 3,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/nxlog_terminal_4625.json.txt"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP NxLog U1 alarm: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }

$srcIp = "10.77.$((Get-Random -Maximum 250)).$((Get-Random -Maximum 250))"
$failUser = "u1_nxlog_$((Get-Random -Maximum 99999))"
$eventTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")

Write-Host "=== SIEM NxLog JSON U1 brute-force E2E ===" -ForegroundColor Cyan
Write-Host "  UDP ${Server}:${UdpPort} host=$SourceHost user=$failUser srcIp=$srcIp" -ForegroundColor DarkGray

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"

Write-Host "`n1) U1 correlation rule (threshold=$Threshold)..." -ForegroundColor Cyan
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U1 NxLog E2E $(Get-Date -Format 'HHmmss')"
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

$template = Get-Content $fixturePath -Raw
$payload = ($template -replace 'probe_fail_user', $failUser) `
    -replace '192\.168\.20\.99', $srcIp `
    -replace '2026-06-06 10:27:29', $eventTime `
    -replace 'TERMINAL\.odak\.local', $SourceHost

function Send-Udp([string]$Json) {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    } finally {
        $udp.Close()
    }
}

Write-Host "`n2) UDP x$Threshold login_failed (4625)..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    Send-Udp $payload
    Start-Sleep -Milliseconds 350
}

Write-Host "`n3) Engine flush..." -ForegroundColor Cyan
$flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 90
Write-Host "   accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor Green
if ($flush.accepted -lt $Threshold) {
    throw "FAIL: flush accepted=$($flush.accepted) (beklenen >= $Threshold)"
}

Start-Sleep -Seconds 2
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=login_failed&sourceType=ad" -Headers $hdr
$parsed = @($q.items) | Where-Object {
    $_.parserId -eq "windows.nxlog-json.v1" -and $_.actorUser -eq $failUser -and $_.networkSrcIp -eq $srcIp
} | Select-Object -First 1
if ($parsed) {
    Write-Host "   Parse OK parser=$($parsed.parserId) host=$($parsed.sourceHost)" -ForegroundColor DarkGray
} else {
    Write-Host "   WARN: sec_events sorguda henuz gorunmedi (devam)" -ForegroundColor Yellow
}

Write-Host "`n4) Alarm raised bekleniyor..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 25; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity)" -ForegroundColor Green
        $found = $true
        break
    }
}

if (-not $found) {
    throw "FAIL: NxLog JSON U1 correlation alarm bulunamadi"
}

Write-Host "`nOK SIEM NxLog JSON -> observation -> U1 alarm PASS" -ForegroundColor Green
