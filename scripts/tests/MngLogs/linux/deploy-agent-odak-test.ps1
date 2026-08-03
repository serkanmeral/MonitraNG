# Publish + deploy MngLogs.Agent.Linux to Odak test host (192.168.20.20).
# NOT: Günlük / prod çalışması için deploy-agent-odak-prod.ps1 kullanın (collector = 192.168.20.8).
param(
    [string]$Server = "192.168.20.20",
    [string]$CollectorUrl = "http://192.168.20.20:5091",
    [string]$HostId = "monitrang-linux-pilot",
    [string]$SshUser = "odak",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "..\..\..\odak\OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
$mngLogs = Join-Path $repoRoot "MngLogs"
$publishDir = Join-Path $mngLogs "artifacts\agent\linux-x64"
$tarPath = Join-Path $mngLogs "artifacts\agent\mnglogs-agent-linux-x64.tar.gz"

if (-not $SkipPublish) {
    & (Join-Path $mngLogs "scripts\publish-agent-linux.ps1")
}

if (Test-Path $tarPath) { Remove-Item $tarPath -Force }
Push-Location $publishDir
try { & tar -czf $tarPath * } finally { Pop-Location }

Initialize-OdakSshEnvironment -Server $Server
if ([string]::IsNullOrWhiteSpace($env:ODAK_SSH_PASSWORD)) {
    throw "ODAK_SSH_PASSWORD gerekli"
}

$cred = Get-OdakSshCredential -Server $Server -User $SshUser
Send-OdakRemoteFile -ComputerName $Server -Credential $cred -LocalPath $tarPath -RemoteDestination "/tmp/mnglogs-agent-linux-x64.tar.gz" -AcceptKey

$systemJson = @"
{
  "collectorBaseUrl": "$CollectorUrl",
  "apiKey": "",
  "hostId": "$HostId",
  "localUiHost": "0.0.0.0",
  "localUiPort": 5092,
  "dataDirectory": "/var/lib/mnglogs/agent",
  "configDirectory": "/etc/mnglogs/agent"
}
"@
$policyJson = @'
{
  "domain": "odak",
  "heartbeatIntervalSeconds": 60,
  "shipIntervalSeconds": 5,
  "maxEventsPerBatch": 100,
  "metrics": {
    "enabled": true,
    "includeHostResources": true,
    "includeTopProcesses": true,
    "topProcessCount": 5
  },
  "eventLog": { "enabled": false },
  "journal": {
    "enabled": true,
    "pollIntervalSeconds": 10,
    "maxEventsPerPoll": 50,
    "disabledPackages": [],
    "packages": []
  },
  "serviceWatch": {
    "enabled": true,
    "pollIntervalSeconds": 15,
    "restartCooldownSeconds": 300,
    "restartMaxAttempts": 3,
    "includeInventory": true,
    "inventoryIntervalSeconds": 60,
    "services": [
      { "name": "ssh.service", "restartAllowed": false },
      { "name": "docker.service", "restartAllowed": false },
      { "name": "cron.service", "restartAllowed": false },
      { "name": "rsyslog.service", "restartAllowed": false }
    ],
    "applications": []
  }
}
'@

$sysB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($systemJson))
$polB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($policyJson))
$sudo = $env:ODAK_SSH_PASSWORD.Replace("'", "'\''")

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $remote = @"
set -e
SP='$sudo'
run_sudo() { echo "`$SP" | sudo -S -p '' "`$@"; }
run_sudo systemctl stop mnglogs-agent.service || true
run_sudo rm -rf /tmp/mnglogs-agent-extract
mkdir -p /tmp/mnglogs-agent-extract
tar -xzf /tmp/mnglogs-agent-linux-x64.tar.gz -C /tmp/mnglogs-agent-extract
run_sudo mkdir -p /opt/mnglogs/agent /etc/mnglogs/agent /var/lib/mnglogs/agent/queue /var/lib/mnglogs/agent/logs
run_sudo bash -c 'rm -rf /opt/mnglogs/agent/*; cp -a /tmp/mnglogs-agent-extract/. /opt/mnglogs/agent/; rm -rf /opt/mnglogs/agent/packaging'
run_sudo install -m 644 /tmp/mnglogs-agent-extract/packaging/mnglogs-agent.service /etc/systemd/system/mnglogs-agent.service
run_sudo chmod +x /opt/mnglogs/agent/MngLogs.Agent
printf '%s' '$sysB64' | base64 -d > /tmp/mnglogs-system.json
printf '%s' '$polB64' | base64 -d > /tmp/mnglogs-policy.json
run_sudo cp /tmp/mnglogs-system.json /etc/mnglogs/agent/system.json
run_sudo cp /tmp/mnglogs-policy.json /etc/mnglogs/agent/policy.json
# Reset journal bookmarks so cursor seed is re-applied after upgrades
run_sudo rm -f /var/lib/mnglogs/agent/journal-bookmarks.json
run_sudo systemctl daemon-reload
run_sudo systemctl enable mnglogs-agent.service
run_sudo systemctl restart mnglogs-agent.service
sleep 6
run_sudo systemctl is-active mnglogs-agent.service
curl -sS http://127.0.0.1:5092/health; echo
curl -sS http://127.0.0.1:5092/api/watch; echo
run_sudo /opt/mnglogs/agent/MngLogs.Agent status --config-dir /etc/mnglogs/agent --data-dir /var/lib/mnglogs/agent
"@
    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command (ConvertTo-UnixShell $remote) -TimeOut 240
    $r.Output | ForEach-Object { Write-Host $_ }
    if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor DarkYellow } }
    if ($r.ExitStatus -ne 0) { throw "Deploy failed exit=$($r.ExitStatus)" }
    Write-Host "OK — Local UI http://${Server}:5092/" -ForegroundColor Green
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
