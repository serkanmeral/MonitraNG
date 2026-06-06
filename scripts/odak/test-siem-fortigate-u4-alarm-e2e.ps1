# SIEM U4 — FortiGate syslog (UDP/Engine :541) → denied_flow → correlation alarm
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [int]$UdpPort = 541,
    [string]$Domain = "odak",
    [int]$Threshold = 3,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixturePath = Join-Path $repoRoot "tests/fixtures/siem/fortigate_traffic_deny.syslog.txt"
$ruleFixture = Join-Path $repoRoot "tests/fixtures/siem/alarm_rules/u4_firewall_deny_spike.json"
$tokenScript = Join-Path $PSScriptRoot "../tests/MngDataGateway/auth/get-token.ps1"

function Skip-Test([string]$Reason) {
    Write-Host "SKIP FortiGate U4 alarm: $Reason" -ForegroundColor Yellow
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

if (-not (Test-Path $fixturePath)) { throw "Fixture eksik: $fixturePath" }

try {
    Invoke-RestMethod -Uri "$EngineUrl/swagger/index.html" -TimeoutSec 8 | Out-Null
} catch {
    Skip-Test "Engine erisilemiyor: $EngineUrl"
}

$dstIp = "10.88.$((Get-Random -Maximum 250)).$((Get-Random -Maximum 250))"
$srcIpBase = 113
$lineTemplate = (Get-Content $fixturePath -Raw).TrimEnd() -replace 'dstip=10\.0\.0\.10', "dstip=$dstIp"

Write-Host "=== SIEM FortiGate U4 deny spike E2E (UDP $UdpPort) ===" -ForegroundColor Cyan
Write-Host "  dstIp=$dstIp threshold=$Threshold" -ForegroundColor DarkGray

$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"

Write-Host "`n1) U4 correlation rule..." -ForegroundColor Cyan
$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = "U4 FortiGate E2E $(Get-Date -Format 'HHmmss')"
    type             = $ruleTemplate.type
    matchKey         = $ruleTemplate.matchKey
    groupByFields    = @($ruleTemplate.groupByFields)
    windowMinutes    = $ruleTemplate.windowMinutes
    threshold        = $Threshold
    severity         = $ruleTemplate.severity
    cooldownMinutes  = 0
    dedupKeyTemplate = $ruleTemplate.dedupKeyTemplate
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id) matchKey=denied_flow groupBy=dstIp" -ForegroundColor DarkGray

function Send-Udp([string]$Line) {
    $udp = New-Object System.Net.Sockets.UdpClient
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Line)
        [void]$udp.Send($bytes, $bytes.Length, $Server, $UdpPort)
    } finally {
        $udp.Close()
    }
}

Write-Host "`n2) UDP x$Threshold denied_flow..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $srcIp = "203.0.$srcIpBase.$((Get-Random -Maximum 250))"
    $line = $lineTemplate -replace 'srcip=203\.0\.113\.5', "srcip=$srcIp"
    Send-Udp $line
    Start-Sleep -Milliseconds 400
}

Write-Host "`n3) Engine flush..." -ForegroundColor Cyan
$totalPublished = 0
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 90
    $totalPublished += [int]$flush.published
    Write-Host "   flush#$attempt accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor DarkGray
    if ($totalPublished -ge $Threshold) { break }
    Send-Udp ($lineTemplate -replace 'srcip=203\.0\.113\.5', "srcip=203.0.$srcIpBase.$((Get-Random -Maximum 250))")
    Start-Sleep -Milliseconds 500
}
Write-Host "   toplam published=$totalPublished" -ForegroundColor Green
if ($totalPublished -lt $Threshold) {
    throw "FAIL: toplam published=$totalPublished (beklenen >= $Threshold)"
}

Start-Sleep -Seconds 2
$q = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=5&eventAction=denied_flow&sourceProduct=fortigate" -Headers $hdr
$parsed = @($q.items) | Where-Object { $_.networkDstIp -eq $dstIp } | Select-Object -First 1
if ($parsed) {
    Write-Host "   Parse OK parser=$($parsed.parserId) dstIp=$($parsed.networkDstIp)" -ForegroundColor DarkGray
}

Write-Host "`n4) Alarm raised bekleniyor..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 25; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=6" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        $ctx = $match.context
        $ctxDst = if ($ctx.dstIp) { $ctx.dstIp } else { $ctx.DstIp }
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity) dstIp=$ctxDst" -ForegroundColor Green
        if ($ctxDst -and $ctxDst -ne $dstIp) {
            throw "FAIL: context.dstIp beklenen $dstIp, gelen $ctxDst"
        }
        $found = $true
        break
    }
}

if (-not $found) {
    throw "FAIL: U4 FortiGate correlation alarm bulunamadi"
}

Write-Host "`nOK SIEM FortiGate UDP -> denied_flow -> U4 alarm PASS" -ForegroundColor Green
