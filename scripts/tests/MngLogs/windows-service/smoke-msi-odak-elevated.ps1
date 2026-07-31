#Requires -Version 7.0
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Elevated MSI install + service smoke for Odak collector (admin lab).
#>
param(
    [string] $CollectorUrl = "http://192.168.20.8:5091",
    [string] $MsiPath = "",
    [string] $HostId = "",
    [string] $ApiKey = "",
    [int] $LocalUiPort = 5092
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
if (-not $MsiPath) {
    $MsiPath = Join-Path $repo "MngLogs\artifacts\msi\MngLogs.Agent-0.2.0.msi"
}
if (-not (Test-Path $MsiPath)) {
    throw "MSI not found: $MsiPath"
}

$log = Join-Path $env:TEMP "mnglogs-agent-install.log"
Write-Host "=== MSI install ===" -ForegroundColor Cyan
Write-Host "MSI: $MsiPath"
Write-Host "Collector: $CollectorUrl"
Write-Host "Log: $log"

$msiArgs = @(
    "/i", "`"$MsiPath`"",
    "/qn",
    "/L*v", "`"$log`"",
    "COLLECTORURL=$CollectorUrl",
    "LOCALUIHOST=127.0.0.1",
    "LOCALUIPORT=$LocalUiPort"
)
if ($ApiKey) { $msiArgs += "APIKEY=$ApiKey" }
if ($HostId) { $msiArgs += "HOSTID=$HostId" }

$p = Start-Process -FilePath "msiexec.exe" -ArgumentList $msiArgs -Wait -PassThru
Write-Host "msiexec exit=$($p.ExitCode)"
if ($p.ExitCode -ne 0) {
    Write-Host "Tail of install log:" -ForegroundColor Yellow
    if (Test-Path $log) { Get-Content $log -Tail 40 }
    throw "msiexec failed with $($p.ExitCode)"
}

Write-Host "=== Wait for service ===" -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds(60)
do {
    $svc = Get-Service -Name MngLogsAgent -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq "Running") { break }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)

if (-not $svc -or $svc.Status -ne "Running") {
    Get-Service MngLogsAgent -ErrorAction SilentlyContinue | Format-List *
    throw "MngLogsAgent not Running"
}

Start-Sleep -Seconds 3
Write-Host "=== smoke-service.ps1 ===" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "smoke-service.ps1") -LocalUiPort $LocalUiPort

$exe = "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe"
Write-Host "=== config show ===" -ForegroundColor Cyan
& $exe config show
Write-Host "=== catalog show (if any) ===" -ForegroundColor Cyan
& $exe catalog show 2>&1 | Select-Object -First 40

Write-Host "=== Security channel read probe ===" -ForegroundColor Cyan
try {
    $q = [System.Diagnostics.Eventing.Reader.EventLogQuery]::new(
        "Security",
        [System.Diagnostics.Eventing.Reader.PathType]::LogName,
        "*[System[(EventID=4624 or EventID=4625)]]")
    $reader = [System.Diagnostics.Eventing.Reader.EventLogReader]::new($q)
    $n = 0
    for ($i = 0; $i -lt 3; $i++) {
        $ev = $reader.ReadEvent()
        if ($null -eq $ev) { break }
        $n++
        Write-Host ("  Security sample Id={0} Time={1}" -f $ev.Id, $ev.TimeCreated)
        $ev.Dispose()
    }
    $reader.Dispose()
    Write-Host "Security Event Log readable as LocalSystem/admin (samples=$n)" -ForegroundColor Green
} catch {
    Write-Warning "Security Event Log probe failed: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "DONE. Local UI: http://127.0.0.1:$LocalUiPort/" -ForegroundColor Green
Write-Host "Install log: $log"
