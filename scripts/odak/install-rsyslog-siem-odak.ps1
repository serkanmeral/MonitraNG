# Linux host rsyslog -> MngEngine syslog (auth/authpriv only)
param(
    [ValidateSet("test", "prod", "both")]
    [string]$Target = "both",
    [string]$TestServer = "192.168.20.20",
    [string]$ProdServer = "192.168.20.8",
    [string]$EngineHost = "192.168.20.20",
    [int]$EnginePort = 5514,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

function Install-RsyslogOnHost {
    param(
        [string]$Server,
        [string]$ForwardHost
    )

    Initialize-OdakSshEnvironment -Server $Server
    $sudoPass = $env:ODAK_SUDO_PASSWORD
    if ([string]::IsNullOrWhiteSpace($sudoPass)) { $sudoPass = $env:ODAK_SSH_PASSWORD }
    if ([string]::IsNullOrWhiteSpace($sudoPass)) {
        throw "ODAK_SSH_PASSWORD veya ODAK_SUDO_PASSWORD gerekli ($Server)"
    }

    $escapedSudo = $sudoPass.Replace("'", "'\''")
    $forwardLine = "auth,authpriv.* @${ForwardHost}:${EnginePort}"

    Write-Host "`n=== $Server -> ${ForwardHost}:${EnginePort} (UDP) ===" -ForegroundColor Cyan
    Write-Host "   ssh: imjournal (51-*.conf) — Failed/Accepted password only" -ForegroundColor DarkGray

    if (-not $Apply) {
        Write-Host "   Dry-run (-Apply ile kur)" -ForegroundColor Yellow
        return
    }

    $journalRaw = @'
module(load="imjournal" StateFile="/var/spool/rsyslog/imjournal.state")
# Yalnizca SIEM anlamli sshd satirlari (gurultu: Invalid user, pam_unix, Connection reset...)
if ($!_SYSTEMD_UNIT == "ssh.service" and ($!MESSAGE contains "Failed password" or $!MESSAGE contains "Accepted password")) then {
  action(type="omfwd" target="FORWARD_HOST" port="ENGINE_PORT" protocol="udp")
  stop
}
'@.Replace('FORWARD_HOST', $ForwardHost).Replace('ENGINE_PORT', "$EnginePort")
    $journalB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($journalRaw))

    $remoteScript = @"
set -e
SP='$escapedSudo'
run_sudo() { echo "`$SP" | sudo -S "`$@"; }
run_sudo apt-get update -qq
run_sudo apt-get install -y -qq rsyslog
run_sudo bash -c 'cat > /etc/rsyslog.d/50-monitrang-siem.conf << EOF
# MonitraNG SIEM — genis auth forward yok (journal gurultusu). SSH: 51-monitrang-siem-journal-sshd.conf
EOF'
printf '%s' '$journalB64' | base64 -d > /tmp/51-monitrang-siem.conf
run_sudo cp /tmp/51-monitrang-siem.conf /etc/rsyslog.d/51-monitrang-siem-journal-sshd.conf
run_sudo rsyslogd -N1
run_sudo systemctl enable rsyslog
run_sudo systemctl restart rsyslog
run_sudo systemctl is-active rsyslog
run_sudo cat /etc/rsyslog.d/50-monitrang-siem.conf
run_sudo cat /etc/rsyslog.d/51-monitrang-siem-journal-sshd.conf
"@

    $cred = Get-OdakSshCredential -Server $Server
    $session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
    try {
        $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remoteScript -TimeOut 300
        if ($r.Output) { $r.Output | ForEach-Object { Write-Host "   $_" } }
        if ($r.ExitStatus -ne 0) { throw "rsyslog kurulumu basarisiz exit=$($r.ExitStatus)" }
        Write-Host "   OK" -ForegroundColor Green
    }
    finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }
}

Write-Host "=== MonitraNG SIEM rsyslog kurulumu ===" -ForegroundColor Cyan
if (-not $Apply) { Write-Host "Dry-run — uygulamak icin -Apply" -ForegroundColor Yellow }

if ($Target -eq "test" -or $Target -eq "both") {
    Install-RsyslogOnHost -Server $TestServer -ForwardHost "127.0.0.1"
}
if ($Target -eq "prod" -or $Target -eq "both") {
    Install-RsyslogOnHost -Server $ProdServer -ForwardHost $EngineHost
}

Write-Host "`nOK tamam" -ForegroundColor Green
