# Iki Linux sunucusundan basarili/basarisiz auth syslog + U1 alarm demo
param(
    [string]$TestHost = "192.168.20.20",
    [string]$ProdHost = "192.168.20.8",
    [string]$EngineHost = "192.168.20.20",
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [int]$WaitAlarmSeconds = 120
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

function Invoke-LinuxSyslogDemo {
    param(
        [string]$LinuxHost,
        [string]$EngineTarget,
        [string]$HostName,
        [string]$FailUser,
        [string]$FailSrcIp,
        [string]$OkUser,
        [string]$OkSrcIp
    )

    Initialize-OdakSshEnvironment -Server $LinuxHost
    $cred = Get-OdakSshCredential -Server $LinuxHost
    $session = New-SSHSession -ComputerName $LinuxHost -Credential $cred -AcceptKey
    Write-Host "`n=== $LinuxHost -> ${EngineTarget}:5514 (host=$HostName) ===" -ForegroundColor Cyan

    $remote = @"
set -e
send() { printf '%s\n' "`$1" | nc -u -w1 "$EngineTarget" 5514; sleep 1; }
send "Apr 10 12:05:01 $HostName sshd-session[801]: Accepted password for $OkUser from $OkSrcIp port 22 ssh2"
send "Apr 10 12:05:02 $HostName sshd-session[802]: Accepted password for $OkUser from $OkSrcIp port 22 ssh2"
send "Apr 10 12:06:01 $HostName sshd-session[811]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:02 $HostName sshd-session[812]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:03 $HostName sshd-session[813]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:04 $HostName sshd-session[814]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:05 $HostName sshd-session[815]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:06 $HostName sshd-session[816]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:07 $HostName sshd-session[817]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:08 $HostName sshd-session[818]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:09 $HostName sshd-session[819]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
send "Apr 10 12:06:10 $HostName sshd-session[820]: Failed password for invalid user $FailUser from $FailSrcIp port 22 ssh2"
echo OK
"@

    try {
        $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 300
        if ($r.Output) { $r.Output | ForEach-Object { Write-Host "  $_" } }
        if ($r.ExitStatus -ne 0) { throw "UDP syslog failed on $LinuxHost exit=$($r.ExitStatus) err=$($r.Error)" }
    }
    finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }
}

Write-Host "=== SIEM iki Linux host pilot ===" -ForegroundColor Cyan

Invoke-LinuxSyslogDemo -LinuxHost $TestHost -EngineTarget "127.0.0.1" -HostName "monitrang" `
    -FailUser "pilot_fail_test20" -FailSrcIp "192.168.20.131" `
    -OkUser "pilot_ok_test20" -OkSrcIp "192.168.20.132"

Invoke-LinuxSyslogDemo -LinuxHost $ProdHost -EngineTarget $EngineHost -HostName "monitrang-prod" `
    -FailUser "pilot_fail_prod08" -FailSrcIp "192.168.20.141" `
    -OkUser "pilot_ok_prod08" -OkSrcIp "192.168.20.142"

Start-Sleep -Seconds 2
for ($a = 1; $a -le 5; $a++) {
    $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 60
    Write-Host "Engine flush #$a accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor DarkGray
    if ($flush.accepted -ge 20) { break }
    Start-Sleep -Seconds 2
}

Start-Sleep -Seconds 12
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }

$failed = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=200&eventAction=login_failed" -Headers $hdr
$success = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=200&eventAction=login_success" -Headers $hdr

Write-Host "`nlogin_failed total=$($failed.total)" -ForegroundColor Green
$failed.items | Group-Object sourceHost, actorUser, networkSrcIp | ForEach-Object {
    $g = $_.Group[0]
    Write-Host "  fail | host=$($g.sourceHost) user=$($g.actorUser) src=$($g.networkSrcIp) count=$($_.Count)" -ForegroundColor DarkGray
}
Write-Host "login_success total=$($success.total)" -ForegroundColor Green
$success.items | Group-Object sourceHost, actorUser, networkSrcIp | ForEach-Object {
    $g = $_.Group[0]
    Write-Host "  ok   | host=$($g.sourceHost) user=$($g.actorUser) src=$($g.networkSrcIp) count=$($_.Count)" -ForegroundColor DarkGray
}

Write-Host "`nU1 alarm bekleniyor (max ${WaitAlarmSeconds}s)..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($WaitAlarmSeconds)
$found = @()
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $page = Invoke-RestMethod -Uri "$Gateway/alarm/api/v1/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    foreach ($a in ($items | Where-Object { $_.ruleName -like "U1*" -or $_.context.scenarioId -eq "U1" })) {
        if ($found.id -notcontains $a.id) {
            $found += $a
            Write-Host "  U1 alarm: id=$($a.id) rule=$($a.ruleName)" -ForegroundColor Green
        }
    }
    if ($found.Count -ge 2) { break }
}

Write-Host "`nAcik U1 alarm: $($found.Count) / 2 beklenen" -ForegroundColor $(if ($found.Count -ge 2) { "Green" } elseif ($found.Count -ge 1) { "Yellow" } else { "Red" })
if ($found.Count -lt 1) { exit 1 }
exit 0
