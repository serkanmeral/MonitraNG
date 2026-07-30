<#
.SYNOPSIS
  Generate sample Windows Event Log entries for MngLogs pilot testing (non-Security).

.DESCRIPTION
  Default path (no admin): starts nested powershell.exe processes so
  "Windows PowerShell" channel emits EventID 400/403/600 (powershell-engine package).

  Optional -ApplicationSamples: writes Application/1000 via MngLogsPilot source
  (source must exist; creating the source needs one elevated New-EventLog).

  Optional -ElevatedSystemSamples: Restart-Service Spooler (admin) for System service events.

.EXAMPLE
  .\generate-sample-events.ps1
  .\generate-sample-events.ps1 -Count 3
  .\generate-sample-events.ps1 -ApplicationSamples
  .\generate-sample-events.ps1 -ElevatedSystemSamples
#>
[CmdletBinding()]
param(
    [int]$Count = 2,
    [switch]$ApplicationSamples,
    [switch]$ElevatedSystemSamples,
    [string]$Source = "MngLogsPilot",
    [string]$LogName = "Application",
    [int]$EventId = 1000
)

$ErrorActionPreference = "Stop"

function Test-IsElevated {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = [Security.Principal.WindowsPrincipal]::new($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-EventSourceRegistered {
    param([string]$Name, [string]$Log)
    $reg = "HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\$Log\$Name"
    return Test-Path $reg
}

Write-Host "=== MngLogs Event Log sample generator ===" -ForegroundColor Cyan
Write-Host ""

# --- Primary: PowerShell engine (no admin) ---
Write-Host "1) powershell-engine (Windows PowerShell 400/403/600)..." -ForegroundColor Cyan
for ($i = 1; $i -le $Count; $i++) {
    $arg = "Write-Output 'MngLogs sample $i'; exit 0"
    $p = Start-Process -FilePath "powershell.exe" -ArgumentList @("-NoProfile", "-NonInteractive", "-Command", $arg) -Wait -PassThru -WindowStyle Hidden
    Write-Host "  nested powershell exit=$($p.ExitCode) ($i/$Count)" -ForegroundColor Green
}
Write-Host "  Expect agent package powershell-engine within poll interval." -ForegroundColor DarkGray
Write-Host ""

# --- Optional: Application custom source ---
if ($ApplicationSamples) {
    Write-Host "2) application-signals (Application/$EventId)..." -ForegroundColor Cyan
    if (-not (Test-EventSourceRegistered -Name $Source -Log $LogName)) {
        if (-not (Test-IsElevated)) {
            Write-Host @"
  Source '$Source' not registered. One-time elevated:
    New-EventLog -LogName $LogName -Source $Source
  Then re-run with -ApplicationSamples.
"@ -ForegroundColor Yellow
        }
        else {
            New-EventLog -LogName $LogName -Source $Source
            Write-Host "  Created source $Source" -ForegroundColor Green
        }
    }

    if (Test-EventSourceRegistered -Name $Source -Log $LogName) {
        $stamp = Get-Date -Format o
        $samples = @(
            @{ Kind = "Information"; Message = "MngLogs pilot INFO sample $stamp" },
            @{ Kind = "Warning"; Message = "MngLogs pilot WARNING sample $stamp" },
            @{ Kind = "Error"; Message = "MngLogs pilot ERROR sample $stamp" }
        )
        for ($i = 0; $i -lt $Count; $i++) {
            $s = $samples[$i % $samples.Count]
            # Avoid [EventLog]::SourceExists (throws without Security ACL). Use registry + .NET write.
            $log = New-Object System.Diagnostics.EventLog($LogName)
            $log.Source = $Source
            $entryType = [System.Diagnostics.EventLogEntryType]::Information
            if ($s.Kind -eq "Warning") { $entryType = [System.Diagnostics.EventLogEntryType]::Warning }
            if ($s.Kind -eq "Error") { $entryType = [System.Diagnostics.EventLogEntryType]::Error }
            try {
                # Bypass SourceExists by writing through unsafe path used when source is known registered
                $log.ModifyOverflowPolicy([System.Diagnostics.OverflowAction]::OverwriteAsNeeded, 7)
                $log.WriteEntry($s.Message, $entryType, $EventId)
                Write-Host "  wrote Application EventID=$EventId ($($s.Kind))" -ForegroundColor Green
            }
            catch {
                Write-Host "  WriteEntry failed: $($_.Exception.Message)" -ForegroundColor Yellow
                Write-Host "  Fallback: elevated eventcreate.exe or New-EventLog + Write-EventLog" -ForegroundColor DarkGray
            }
            finally {
                $log.Dispose()
            }
        }
    }
}
else {
    Write-Host "2) Application samples skipped (add -ApplicationSamples; needs registered source)." -ForegroundColor DarkGray
}

Write-Host ""

# --- Optional: System via Spooler ---
if ($ElevatedSystemSamples) {
    Write-Host "3) system-lifecycle via Spooler restart..." -ForegroundColor Cyan
    if (-not (Test-IsElevated)) {
        Write-Host "  Needs admin; skipped." -ForegroundColor Yellow
    }
    else {
        try {
            Restart-Service -Name Spooler -Force -ErrorAction Stop
            Write-Host "  Spooler restarted." -ForegroundColor Green
        }
        catch {
            Write-Host "  Spooler restart failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "3) System samples skipped (add -ElevatedSystemSamples as admin)." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Check: Durum > Olay gunlugu | OS dashboard | Kaynaklar" -ForegroundColor Cyan
Write-Host "Security (4624...) not generated here; enable security-auth package when admin arrives."
