#Requires -Version 7.0
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs MngLogs Agent as a per-machine Windows Service (LocalSystem).

.DESCRIPTION
  Mimics the future MSI/GPO layout:
  - Binaries: Program Files\MngLogs\Agent
  - Config/data/logs: %ProgramData%\MngLogs\Agent (not removed on uninstall by default)
  - Service: MngLogsAgent / display "MngLogs Agent" / start=auto / failure restart
  - Silent, non-interactive — suitable pattern for AD Software Installation + MST later

  Does NOT open UI dialogs. Collector URL / API key written to system.json under ProgramData.
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [string] $InstallDir = "C:\Program Files\MngLogs\Agent",

    [string] $CollectorUrl = "",
    [string] $ApiKey = "",
    [string] $HostId = "",
    [int] $LocalUiPort = 5092,
    [string] $LocalUiHost = "127.0.0.1",

    [switch] $StartService,
    [switch] $SkipConfig
)

$ErrorActionPreference = "Stop"

$ServiceName = "MngLogsAgent"
$DisplayName = "MngLogs Agent"
$DataDir = Join-Path $env:ProgramData "MngLogs\Agent"
$exeName = "MngLogs.Agent.exe"

$SourceDir = (Resolve-Path $SourceDir).Path
$sourceExe = Join-Path $SourceDir $exeName
if (-not (Test-Path $sourceExe)) {
    Write-Error "Source exe not found: $sourceExe (run publish-agent.ps1 first)"
    exit 1
}

function Stop-AgentServiceIfExists {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $svc) { return }
    if ($svc.Status -ne "Stopped") {
        Write-Host "Stopping $ServiceName..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        $svc.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(45))
    }
}

Write-Host "==> Installing $DisplayName (per-machine / LocalSystem)"
Write-Host "    Source : $SourceDir"
Write-Host "    Target : $InstallDir"
Write-Host "    Data   : $DataDir"

Stop-AgentServiceIfExists

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $DataDir "logs") -Force | Out-Null

Write-Host "==> Copying binaries"
robocopy $SourceDir $InstallDir /MIR /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
# robocopy exit codes 0-7 are success
if ($LASTEXITCODE -ge 8) {
    Write-Error "robocopy failed with exit $LASTEXITCODE"
    exit $LASTEXITCODE
}

$binPath = Join-Path $InstallDir $exeName
if (-not (Test-Path $binPath)) {
    Write-Error "Install copy failed; missing $binPath"
    exit 1
}

if (-not $SkipConfig) {
    Write-Host "==> Writing system.json (GPO/MST equivalent)"
    & (Join-Path $PSScriptRoot "write-system-config.ps1") `
        -CollectorUrl $CollectorUrl `
        -ApiKey $ApiKey `
        -HostId $HostId `
        -LocalUiPort $LocalUiPort `
        -LocalUiHost $LocalUiHost `
        -DataDirectory $DataDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "==> Service already registered; updating binPath"
    & sc.exe config $ServiceName binPath= "`"$binPath`"" start= auto DisplayName= $DisplayName | Out-Host
} else {
    Write-Host "==> Creating service $ServiceName"
    & sc.exe create $ServiceName binPath= "`"$binPath`"" start= auto DisplayName= $DisplayName obj= LocalSystem | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& sc.exe description $ServiceName "MonitraNG MngLogs field agent (metrics, event log, ship to collector). Local UI on loopback." | Out-Null
# Restart on failure — important for GPO-deployed always-on agents
& sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
& sc.exe failureflag $ServiceName 1 | Out-Null

if ($StartService) {
    Write-Host "==> Starting $ServiceName"
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 2
    Get-Service -Name $ServiceName | Format-List Name, Status, StartType
    try {
        $health = Invoke-RestMethod -Uri "http://${LocalUiHost}:${LocalUiPort}/health" -TimeoutSec 5
        Write-Host "Health: $($health | ConvertTo-Json -Compress)"
    } catch {
        Write-Warning "Service started but Local UI health check failed: $($_.Exception.Message)"
        Write-Warning "Check logs under $DataDir\logs"
    }
}

Write-Host ""
Write-Host "Install complete."
Write-Host "  Service : $ServiceName"
Write-Host "  Local UI: http://${LocalUiHost}:${LocalUiPort}/"
Write-Host "  Logs    : $DataDir\logs"
Write-Host "  CLI     : `"$binPath`" status"
exit 0
