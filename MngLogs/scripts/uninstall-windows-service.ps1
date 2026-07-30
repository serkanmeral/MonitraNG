#Requires -Version 7.0
#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Stops and removes the MngLogsAgent Windows Service. Optionally removes binaries.

.DESCRIPTION
  By default keeps %ProgramData%\MngLogs\Agent (PIN, queue, bookmarks, system.json)
  so GPO upgrade / reinstall does not wipe fleet config. Use -RemoveData to wipe.
#>
param(
    [string] $InstallDir = "C:\Program Files\MngLogs\Agent",
    [switch] $RemoveBinaries,
    [switch] $RemoveData
)

$ErrorActionPreference = "Stop"
$ServiceName = "MngLogsAgent"
$DataDir = Join-Path $env:ProgramData "MngLogs\Agent"

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne "Stopped") {
        Write-Host "Stopping $ServiceName..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        try { $svc.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(45)) } catch { }
    }
    Write-Host "Deleting service $ServiceName..."
    & sc.exe delete $ServiceName | Out-Host
    Start-Sleep -Seconds 1
} else {
    Write-Host "Service $ServiceName not found."
}

if ($RemoveBinaries -and (Test-Path $InstallDir)) {
    Write-Host "Removing binaries: $InstallDir"
    Remove-Item $InstallDir -Recurse -Force
}

if ($RemoveData -and (Test-Path $DataDir)) {
    Write-Host "Removing data: $DataDir"
    Remove-Item $DataDir -Recurse -Force
} elseif (Test-Path $DataDir) {
    Write-Host "Data retained: $DataDir (use -RemoveData to wipe)"
}

Write-Host "Uninstall done."
exit 0
