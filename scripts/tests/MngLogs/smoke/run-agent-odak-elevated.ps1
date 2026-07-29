$ErrorActionPreference = "Stop"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:MngLogsAgentSettings__System__CollectorBaseUrl = "http://192.168.20.8:5091"
$env:MngLogsAgentSettings__System__DataDirectory = Join-Path $env:TEMP "MngLogs-Agent-Pilot"
$env:MngLogsAgentSettings__System__HostId = "$env:COMPUTERNAME-pilot"
$env:MngLogsAgentSettings__Policy__Domain = "odak"
$env:MngLogsAgentSettings__Policy__HeartbeatIntervalSeconds = "20"
$env:MngLogsAgentSettings__Policy__ShipIntervalSeconds = "3"
$env:MngLogsAgentSettings__Policy__EventLog__Enabled = "true"
$env:MngLogsAgentSettings__Policy__EventLog__PollIntervalSeconds = "8"

# Prefer default Security + System packages (clear pilot-only Application override)
$policyPath = Join-Path $env:MngLogsAgentSettings__System__DataDirectory "policy.json"
if (Test-Path $policyPath) { Remove-Item $policyPath -Force }
$bm = Join-Path $env:MngLogsAgentSettings__System__DataDirectory "eventlog-bookmarks.json"
if (Test-Path $bm) { Remove-Item $bm -Force }

New-Item -ItemType Directory -Force -Path $env:MngLogsAgentSettings__System__DataDirectory | Out-Null

# Prove Event Log write works when elevated
try {
  if (-not [System.Diagnostics.EventLog]::SourceExists("MngLogsPilot")) {
    [System.Diagnostics.EventLog]::CreateEventSource("MngLogsPilot", "Application")
  }
  $log = New-Object System.Diagnostics.EventLog("Application")
  $log.Source = "MngLogsPilot"
  $log.WriteEntry("MngLogs elevated pilot $(Get-Date -Format o)", [System.Diagnostics.EventLogEntryType]::Information, 1000)
  Write-Host "Wrote Application EventID 1000"
} catch {
  Write-Host "Event write note: $_"
}

Set-Location "c:\Users\monitra\Dev\MonitraNG\MonitraNG\MngLogs\Presentation\MngLogs.Agent"
dotnet build -c Release
dotnet run -c Release --no-build --no-launch-profile
