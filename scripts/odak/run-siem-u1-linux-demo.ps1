# U1 demo — Linux rsyslog uzerinden 10x basarisiz login (siem-mvp-v1)
param(
    [string]$LinuxHost = "192.168.20.20",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$Domain = "odak",
    [string]$TestUser = "u1_manual_run",
    [string]$TestSrcIp = "192.168.99.100",
    [int]$FailCount = 10,
    [int]$WaitAlarmSeconds = 90
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $LinuxHost

$sudoPass = $env:ODAK_SUDO_PASSWORD
if ([string]::IsNullOrWhiteSpace($sudoPass)) { $sudoPass = $env:ODAK_SSH_PASSWORD }
if ([string]::IsNullOrWhiteSpace($sudoPass)) {
    throw "ODAK_SSH_PASSWORD gerekli (.env.odak.local)"
}
$escapedSudo = $sudoPass.Replace("'", "'\''")

Write-Host "=== U1 Linux demo ===" -ForegroundColor Cyan
Write-Host "  Host: $LinuxHost" -ForegroundColor DarkGray
Write-Host "  user=$TestUser srcIp=$TestSrcIp count=$FailCount" -ForegroundColor DarkGray
Write-Host "  (U1 cooldown: ayni user+IP icin 15 dk bekleyin veya farkli -TestUser/-TestSrcIp kullanin)" -ForegroundColor DarkYellow

$cred = Get-OdakSshCredential -Server $LinuxHost
$session = New-SSHSession -ComputerName $LinuxHost -Credential $cred -AcceptKey
try {
    $remote = @"
SP='$escapedSudo'
run_sudo() { echo "`$SP" | sudo -S "`$@"; }
for i in `$(seq 1 $FailCount); do
  run_sudo logger -p auth.info -t sshd "Failed password for invalid user $TestUser from $TestSrcIp port 22 ssh2"
  sleep 1
done
echo OK_SENT_$FailCount
"@
    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 180
    if ($r.Output) { $r.Output | ForEach-Object { Write-Host "  $_" } }
    if ($r.ExitStatus -ne 0) { throw "logger dongusu basarisiz exit=$($r.ExitStatus)" }
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}

Start-Sleep -Seconds 2
try {
    $flush = Invoke-RestMethod -Uri "$EngineUrl/api/SecEvents/flush" -Method POST -TimeoutSec 60
    Write-Host "Engine flush: accepted=$($flush.accepted) published=$($flush.published)" -ForegroundColor DarkGray
} catch {
    Write-Host "Engine flush atlandi (rsyslog zaten gondermis olabilir): $($_.Exception.Message)" -ForegroundColor DarkYellow
}

$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain }

Start-Sleep -Seconds 5
$events = Invoke-RestMethod -Uri "$Gateway/reactor/api/v1/sec-events?limit=50&eventAction=login_failed&sourceProduct=linux-syslog" -Headers $hdr
$mine = @($events.items) | Where-Object { $_.actorUser -eq $TestUser -and $_.networkSrcIp -eq $TestSrcIp }
Write-Host "sec_events (login_failed): $($mine.Count) / $FailCount" -ForegroundColor $(if ($mine.Count -ge $FailCount) { "Green" } else { "Yellow" })

Write-Host "U1 alarm bekleniyor..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($WaitAlarmSeconds)
$found = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$Gateway/alarm/api/v1/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    $found = $items | Where-Object {
        ($_.context.scenarioId -eq "U1" -or $_.ruleName -like "U1*") -and
        ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($found) { break }
}

if ($found) {
    Write-Host "OK U1 alarm: id=$($found.id) severity=$($found.severity)" -ForegroundColor Green
    Write-Host "   UI: $Gateway -> Alarm Merkezi /apps/alarm-center/alarms" -ForegroundColor DarkGray
    exit 0
}

Write-Host "U1 alarm henuz gorunmuyor — Olaylar: /apps/siem-center/events?eventAction=login_failed" -ForegroundColor Yellow
exit 1
