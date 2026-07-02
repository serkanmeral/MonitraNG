# Production (192.168.20.8) — FortiGate syslog 514 → MngEngine 541 relay
# IT FortiGate varsayilan hedefi 514/UDP kullandiginda host rsyslog bu trafigi Engine'e iletir.
#
# Usage (repo kokunden):
#   .\scripts\odak\install-prod-fortigate-syslog-relay.ps1          # dry-run
#   .\scripts\odak\install-prod-fortigate-syslog-relay.ps1 -Apply
#
# Kalici config: /etc/rsyslog.d/53-monitrang-fortigate-514-relay.conf
# Engine listener: ApplicationResources/mng_apps/docker-compose.odak.prod.yml (541/542)

param(
    [string]$Server = "192.168.20.8",
    [string]$EngineHost = "127.0.0.1",
    [int]$EnginePort = 541,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$templatePath = Join-Path $PSScriptRoot "../../docs/odak/monitoring/templates/rsyslog-prod-fortigate-514-to-engine.conf"
if (-not (Test-Path $templatePath)) {
    throw "Sablon bulunamadi: $templatePath"
}

$conf = Get-Content -Raw -LiteralPath $templatePath
$conf = $conf.Replace("ENGINE_HOST", $EngineHost).Replace("ENGINE_PORT", "$EnginePort")
$confB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($conf))

Write-Host "=== Prod FortiGate syslog relay ($Server) ===" -ForegroundColor Cyan
Write-Host "  Dinle: UDP 514 (FortiGate varsayilan)" -ForegroundColor Gray
Write-Host "  Hedef: ${EngineHost}:${EnginePort} (MngEngine fortigate listener)" -ForegroundColor Gray
Write-Host "  Config: /etc/rsyslog.d/53-monitrang-fortigate-514-relay.conf" -ForegroundColor Gray

if (-not $Apply) {
    Write-Host "Dry-run — uygulamak icin -Apply" -ForegroundColor Yellow
    Write-Host $conf
    exit 0
}

Initialize-OdakSshEnvironment -Server $Server
$sudoPass = $env:ODAK_SUDO_PASSWORD
if ([string]::IsNullOrWhiteSpace($sudoPass)) { $sudoPass = $env:ODAK_SSH_PASSWORD }
if ([string]::IsNullOrWhiteSpace($sudoPass)) {
    throw "ODAK_SSH_PASSWORD veya ODAK_SUDO_PASSWORD gerekli (.env.odak.prod.local)"
}
$escapedSudo = $sudoPass.Replace("'", "'\''")

$remoteScript = @"
set -e
SP='$escapedSudo'
run_sudo() { echo "`$SP" | sudo -S "`$@"; }
run_sudo apt-get update -qq
run_sudo apt-get install -y -qq rsyslog
printf '%s' '$confB64' | base64 -d > /tmp/53-monitrang-fortigate-514-relay.conf
run_sudo cp /tmp/53-monitrang-fortigate-514-relay.conf /etc/rsyslog.d/53-monitrang-fortigate-514-relay.conf
run_sudo rsyslogd -N1
run_sudo systemctl enable rsyslog
run_sudo systemctl restart rsyslog
run_sudo systemctl is-active rsyslog
echo '--- listeners (514, 541) ---'
ss -ulnp 2>/dev/null | egrep ':514 |:541 ' || true
run_sudo cat /etc/rsyslog.d/53-monitrang-fortigate-514-relay.conf
"@

$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remoteScript -TimeOut 300
    if ($r.Output) { $r.Output | ForEach-Object { Write-Host "   $_" } }
    if ($r.ExitStatus -ne 0) {
        if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Red } }
        throw "Relay kurulumu basarisiz exit=$($r.ExitStatus)"
    }
    Write-Host "`nOK FortiGate 514→${EnginePort} relay kuruldu" -ForegroundColor Green
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
