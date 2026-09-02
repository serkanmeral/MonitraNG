#Requires -Version 7.0
<#
.SYNOPSIS
  Registers the Classic Outlook COM add-in for the current user (HKCU, no admin).

.DESCRIPTION
  Copies MngLogs.OutlookAddin.dll to %LOCALAPPDATA%\MngLogs\OutlookAddin and
  writes Outlook Addins + CLSID keys. Close Outlook before running; reopen after.

  Dilim 1: ItemSend → http://127.0.0.1:{localUiPort}/dlp/evaluate (fail-open).
#>
param(
    [string] $SourceDir = "",
    [switch] $SkipBuild,
    [switch] $CloseOutlook
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "Presentation\MngLogs.OutlookAddin\MngLogs.OutlookAddin.csproj"
$clsid = "{E7B2C4A1-9F18-4D6E-8A3B-1C5E9D0F2B44}"
$progId = "MngLogs.OutlookAddin"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
# Click-to-Run Outlook often refuses unsigned .NET COM from AppData; Program Files is trusted.
$installDir = if ($isAdmin) {
    Join-Path $env:ProgramFiles "MngLogs\OutlookAddin"
} else {
    Join-Path $env:LOCALAPPDATA "MngLogs\OutlookAddin"
}

if (-not $SkipBuild) {
    Write-Host "==> dotnet build (net48 x64 Release)"
    & dotnet build $proj -c Release -p:PlatformTarget=x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SourceDir) {
    $SourceDir = Join-Path $root "Presentation\MngLogs.OutlookAddin\bin\Release\net48"
}
$SourceDir = (Resolve-Path $SourceDir).Path
$dll = Join-Path $SourceDir "MngLogs.OutlookAddin.dll"
if (-not (Test-Path $dll)) {
    Write-Error "Add-in dll not found: $dll"
    exit 1
}

if ($CloseOutlook) {
    Get-Process OUTLOOK -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Write-Host "==> Copy to $installDir"
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path (Join-Path $SourceDir "*") -Destination $installDir -Recurse -Force
$dllPath = Join-Path $installDir "MngLogs.OutlookAddin.dll"
$codeBase = ([Uri]$dllPath).AbsoluteUri

$asm = "MngLogs.OutlookAddin, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null"

function Write-AddinKey([string]$path) {
    New-Item -Path $path -Force | Out-Null
    Set-ItemProperty -Path $path -Name "FriendlyName" -Value "MngLogs DLP"
    Set-ItemProperty -Path $path -Name "Description" -Value "Origin classification DLP via local MngLogsAgent"
    Set-ItemProperty -Path $path -Name "LoadBehavior" -Type DWord -Value 3
    Set-ItemProperty -Path $path -Name "CommandLineSafe" -Type DWord -Value 0
}

function Write-ComRegistration([string]$classesRoot) {
    New-Item -Path "$classesRoot\$progId" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$progId" -Name "(default)" -Value "MngLogs.OutlookAddin.Connect"
    New-Item -Path "$classesRoot\$progId\CLSID" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$progId\CLSID" -Name "(default)" -Value $clsid
    New-Item -Path "$classesRoot\CLSID\$clsid" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\CLSID\$clsid" -Name "(default)" -Value "MngLogs.OutlookAddin.Connect"
    New-Item -Path "$classesRoot\CLSID\$clsid\ProgId" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\CLSID\$clsid\ProgId" -Name "(default)" -Value $progId
    $inproc = "$classesRoot\CLSID\$clsid\InprocServer32"
    New-Item -Path $inproc -Force | Out-Null
    Set-ItemProperty -Path $inproc -Name "(default)" -Value "mscoree.dll"
    Set-ItemProperty -Path $inproc -Name "ThreadingModel" -Value "Both"
    Set-ItemProperty -Path $inproc -Name "Class" -Value "MngLogs.OutlookAddin.Connect"
    Set-ItemProperty -Path $inproc -Name "Assembly" -Value $asm
    Set-ItemProperty -Path $inproc -Name "RuntimeVersion" -Value "v4.0.30319"
    Set-ItemProperty -Path $inproc -Name "CodeBase" -Value $codeBase
    $verKey = "$inproc\0.1.0.0"
    New-Item -Path $verKey -Force | Out-Null
    Set-ItemProperty -Path $verKey -Name "Class" -Value "MngLogs.OutlookAddin.Connect"
    Set-ItemProperty -Path $verKey -Name "Assembly" -Value $asm
    Set-ItemProperty -Path $verKey -Name "RuntimeVersion" -Value "v4.0.30319"
    Set-ItemProperty -Path $verKey -Name "CodeBase" -Value $codeBase
    New-Item -Path "$classesRoot\CLSID\$clsid\Implemented Categories\{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}" -Force | Out-Null
}

Write-Host "==> HKCU COM + Outlook Addins"
Write-ComRegistration "HKCU:\Software\Classes"
Write-AddinKey "HKCU:\Software\Microsoft\Office\Outlook\Addins\$progId"
Write-AddinKey "HKCU:\Software\Microsoft\Office\16.0\Outlook\Addins\$progId"

$c2rAddins = "HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\REGISTRY\MACHINE\SOFTWARE\Microsoft\Office\Outlook\Addins"
$c2rClasses = "HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\REGISTRY\MACHINE\SOFTWARE\Classes"
if ($isAdmin) {
    Write-Host "==> Native HKLM Classes (mscoree reads this, not only C2R)"
    Write-ComRegistration "HKLM:\SOFTWARE\Classes"
    Write-AddinKey "HKLM:\SOFTWARE\Microsoft\Office\Outlook\Addins\$progId"
    if (Test-Path $c2rAddins) {
        Write-Host "==> Click-to-Run virtual HKLM (Outlook enumerates add-ins here)"
        Write-ComRegistration $c2rClasses
        Write-AddinKey "$c2rAddins\$progId"
    }
    $regasm = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
    if (Test-Path $regasm) {
        Write-Host "==> regasm /codebase"
        & $regasm $dllPath /codebase /nologo 2>&1 | Out-Host
    }
} elseif (-not $isAdmin -and (Test-Path 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\REGISTRY\MACHINE\SOFTWARE\Microsoft\Office\Outlook\Addins')) {
    Write-Host "==> Elevating to register in Click-to-Run virtual HKLM"
    $self = $PSCommandPath
    $arg = "-NoProfile -ExecutionPolicy Bypass -File `"$self`" -SkipBuild -CloseOutlook"
    $p = Start-Process -FilePath "pwsh.exe" -Verb RunAs -Wait -PassThru -ArgumentList $arg
    if ($p.ExitCode -ne 0) {
        Write-Warning "Elevated C2R registration failed (exit $($p.ExitCode)). HKCU keys are set; Outlook Click-to-Run may still ignore HKCU."
    }
}

$resiliency = "HKCU:\Software\Microsoft\Office\16.0\Outlook\Resiliency"
New-Item -Path "$resiliency\DoNotDisableAddinList" -Force | Out-Null
New-ItemProperty -Path "$resiliency\DoNotDisableAddinList" -Name $progId -PropertyType DWord -Value 1 -Force | Out-Null
# Outlook 16 default disable threshold is ~1s; .NET COM first load usually exceeds it.
New-Item -Path "HKCU:\Software\Microsoft\Office\16.0\Outlook" -Force | Out-Null
New-ItemProperty -Path "HKCU:\Software\Microsoft\Office\16.0\Outlook" -Name "AddinLoadTimeout" -PropertyType DWord -Value 15000 -Force | Out-Null
foreach ($leaf in @("DisabledItems", "CrashingAddinList", "NotificationReminderAddinData")) {
    $p = Join-Path $resiliency $leaf
    if (Test-Path $p) {
        Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$ngen = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\ngen.exe"
if (Test-Path $ngen) {
    Write-Host "==> ngen (faster cold start)"
    & $ngen install $dllPath /nologo 2>&1 | Out-Host
}

Write-Host ""
Write-Host "Install OK (HKCU)."
Write-Host "  DLL     : $dllPath"
Write-Host "  Add-in  : $progId"
Write-Host "  Log     : $env:LOCALAPPDATA\MngLogs\OutlookAddin\addin.log"
Write-Host "If Outlook shows Slow and Disabled Add-ins: pick 'Do not monitor' (30 days) → Apply, then restart Outlook."
exit 0
