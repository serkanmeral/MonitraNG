#Requires -Version 7.0
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"
$repo = "C:\Users\monitra\Dev\MonitraNG\MonitraNG"
$log = Join-Path $env:TEMP "mnglogs-reinstall-1.0.7.log"
function Log([string]$m) {
  $line = "$(Get-Date -Format o) $m"
  Add-Content -Path $log -Value $line
  Write-Host $line
}

Log "=== Reinstall MngLogs Agent 1.0.7 (selectionMode all/selected + catalog sync status) ==="
$source = Join-Path $repo "MngLogs\artifacts\agent\win-x64"
if (-not (Test-Path (Join-Path $source "MngLogs.Agent.exe"))) {
  throw "Publish missing: $source"
}

$svc = Get-Service MngLogsAgent -ErrorAction SilentlyContinue
if ($svc) {
  if ($svc.Status -ne "Stopped") {
    Stop-Service MngLogsAgent -Force
    Start-Sleep -Seconds 2
  }
}

# LAN bind so Discovery can open http://{primaryIp}:{port}/
& (Join-Path $repo "MngLogs\scripts\install-windows-service.ps1") `
  -SourceDir $source `
  -CollectorUrl "http://192.168.20.8:5091" `
  -HostId "" `
  -LocalUiHost "0.0.0.0" `
  -LocalUiPort 5092 `
  -StartService

# Allow inbound Local UI (lab / Discovery deep-link)
$ruleName = "MngLogs Agent Local UI"
Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5092 -Profile Any | Out-Null
Log "Firewall rule: $ruleName TCP 5092"

Start-Sleep -Seconds 4
& (Join-Path $repo "scripts\tests\MngLogs\windows-service\smoke-service.ps1")

$exe = "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe"
Log "CLI status:"
& $exe status 2>&1 | ForEach-Object { Log $_ }

try {
  $st = Invoke-RestMethod "http://127.0.0.1:5092/api/status" -TimeoutSec 10
  Log ("version={0} primaryIp={1} localUi={2}:{3}" -f `
    $st.version, $st.hostInventory.primaryIp, $st.hostInventory.localUiHost, $st.hostInventory.localUiPort)
  $ip = $st.hostInventory.primaryIp
  if ($ip) {
    $lan = Invoke-RestMethod "http://${ip}:5092/api/status" -TimeoutSec 10
    Log ("LAN Local UI OK http://${ip}:5092/ version={0}" -f $lan.version)
  }
} catch {
  Log "status API: $($_.Exception.Message)"
}

Log "DONE log=$log"
