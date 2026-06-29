# Windows Server 2022 gelistirme makinesinde WSL2 + Ubuntu kurulumu (Docker Desktop icin)
#
# On kosul:
#   - Yonetici PowerShell
#   - VMware VM ise: nested virtualization acik olmali (VT-x/AMD-V)
#   - Internet (kernel + Ubuntu indirme; bir kez)
#
# Kullanim (repo kokunden, Yonetici PS):
#   .\scripts\odak\install-wsl2-dev-host.ps1
#   # Reboot sonrasi:
#   .\scripts\odak\install-wsl2-dev-host.ps1 -Phase PostReboot
#
# -WhatIf: yapilacaklari listeler, degisiklik yapmaz

param(
    [ValidateSet("Auto", "Features", "PostReboot")]
    [string]$Phase = "Auto",
    [switch]$WhatIf,
    [switch]$SkipUbuntu,
    [string]$UbuntuInstallPath = "C:\WSL\Ubuntu-22.04"
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-WslFeatureState {
    param([string]$FeatureName)
    $f = Get-WindowsOptionalFeature -Online -FeatureName $FeatureName -ErrorAction SilentlyContinue
    if (-not $f) { return "Missing" }
    return [string]$f.State
}

function Enable-WslFeature {
    param(
        [string]$FeatureName,
        [string]$Label
    )
    $state = Get-WslFeatureState $FeatureName
    Write-Host "$Label ($FeatureName): $state" -ForegroundColor $(if ($state -eq "Enabled") { "Green" } else { "Yellow" })
    if ($state -eq "Enabled") { return $false }

    if ($WhatIf) {
        Write-Host "  WhatIf: Enable-WindowsOptionalFeature $FeatureName" -ForegroundColor DarkGray
        return $true
    }

    Enable-WindowsOptionalFeature -Online -FeatureName $FeatureName -All -NoRestart | Out-Null
    return $true
}

function Test-NestedVirtualization {
    $cpus = Get-CimInstance Win32_Processor
    $enabled = @($cpus | Where-Object { $_.VirtualizationFirmwareEnabled -eq $true })
    return @{
        IsVmware = ((Get-CimInstance Win32_ComputerSystem).Manufacturer -match "VMware")
        AnyCpuEnabled = ($enabled.Count -gt 0)
        CpuCount = $cpus.Count
    }
}

function Install-Wsl2KernelPackage {
    $msiUrl = "https://wslstorestorage.blob.core.windows.net/wslblob/wsl_update_x64.msi"
    $msiPath = Join-Path $env:TEMP "wsl_update_x64.msi"

    if ($WhatIf) {
        Write-Host "WhatIf: kernel MSI indir + kur -> $msiPath" -ForegroundColor DarkGray
        return
    }

    if (-not (Test-Path $msiPath)) {
        Write-Host "WSL2 kernel MSI indiriliyor..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $msiUrl -OutFile $msiPath -UseBasicParsing
    }

    Write-Host "WSL2 kernel MSI kuruluyor..." -ForegroundColor Cyan
    $p = Start-Process msiexec.exe -ArgumentList "/i `"$msiPath`" /quiet /norestart" -Wait -PassThru
    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) {
        throw "WSL2 kernel MSI kurulumu basarisiz (exit $($p.ExitCode))"
    }
}

function Install-UbuntuDistro {
    if ($WhatIf) {
        Write-Host "WhatIf: Ubuntu-22.04 kurulumu -> $UbuntuInstallPath" -ForegroundColor DarkGray
        return
    }

    $listed = wsl -l -v 2>&1 | Out-String
    if ($listed -match "Ubuntu-22.04") {
        Write-Host "Ubuntu-22.04 zaten kayitli." -ForegroundColor Green
        return
    }

    Write-Host "Ubuntu-22.04 kuruluyor (wsl --install)..." -ForegroundColor Cyan
    wsl --install -d Ubuntu-22.04 --no-launch
    if ($LASTEXITCODE -ne 0) {
        Write-Host "wsl --install basarisiz; rootfs import deneniyor..." -ForegroundColor Yellow
        $rootfsUrl = "https://cloud-images.ubuntu.com/wsl/jammy/current/ubuntu-jammy-wsl-amd64-wsl.rootfs.tar.gz"
        $rootfsTar = Join-Path $env:TEMP "ubuntu-jammy-wsl.rootfs.tar.gz"
        if (-not (Test-Path $rootfsTar)) {
            Invoke-WebRequest -Uri $rootfsUrl -OutFile $rootfsTar -UseBasicParsing
        }
        if (-not (Test-Path $UbuntuInstallPath)) {
            New-Item -ItemType Directory -Path $UbuntuInstallPath -Force | Out-Null
        }
        wsl --import Ubuntu-22.04 $UbuntuInstallPath $rootfsTar --version 2
        if ($LASTEXITCODE -ne 0) { throw "Ubuntu import basarisiz" }
    }
}

function Test-Wsl2Ready {
    $status = wsl --status 2>&1 | Out-String
    if ($status -match "kernel file is not found|WSL 2 requires") { return $false }
    $list = wsl -l -v 2>&1 | Out-String
    if ($list -notmatch "Ubuntu") { return $false }
    return $true
}

function Start-DockerDesktopIfInstalled {
    $dockerDesktop = "${env:ProgramFiles}\Docker\Docker\Docker Desktop.exe"
    if (-not (Test-Path $dockerDesktop)) {
        Write-Host "Docker Desktop bulunamadi; WSL2 sonrasi elle baslatin." -ForegroundColor Yellow
        return
    }
    if ($WhatIf) {
        Write-Host "WhatIf: Docker Desktop baslat" -ForegroundColor DarkGray
        return
    }
    Write-Host "Docker Desktop baslatiliyor..." -ForegroundColor Cyan
    Start-Process $dockerDesktop
    Start-Sleep -Seconds 5
    $null = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Docker calisiyor." -ForegroundColor Green
    } else {
        Write-Host "Docker henuz hazir degil; Docker Desktop tray ikonundan bekleyin, sonra: docker info" -ForegroundColor Yellow
    }
}

# --- Main ---
if (-not (Test-IsAdministrator)) {
    throw @"
Bu script Yonetici PowerShell gerektirir.
Baslat -> PowerShell -> Sag tik -> Run as administrator
Sonra: cd $((Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
       .\scripts\odak\install-wsl2-dev-host.ps1
"@
}

Write-Host "=== WSL2 dev host kurulumu ===" -ForegroundColor Magenta
Write-Host "OS: $((Get-CimInstance Win32_OperatingSystem).Caption)" -ForegroundColor DarkGray

$nested = Test-NestedVirtualization
if ($nested.IsVmware -and -not $nested.AnyCpuEnabled) {
    Write-Host @"

UYARI: Bu makine VMware VM ve nested virtualization KAPALI gorunuyor.
WSL2 icin VM ayarlarinda (kapali guc) su secenek acilmali:
  Virtualize Intel VT-x/EPT or AMD-V/RVI  (VMware)
Sonra Windows'u yeniden baslatin ve bu script'i tekrar calistirin.

Devam ediliyor (nested virt olmadan WSL2 calismayabilir)...
"@ -ForegroundColor Red
}

$needReboot = $false
$runFeatures = ($Phase -in @("Auto", "Features"))
$runPost = ($Phase -in @("Auto", "PostReboot"))

if ($runFeatures) {
    Write-Host "`n--- Windows ozellikleri ---" -ForegroundColor Cyan
    if (Enable-WslFeature "Microsoft-Windows-Subsystem-Linux" "WSL") { $needReboot = $true }
    if (Enable-WslFeature "VirtualMachinePlatform" "Virtual Machine Platform") { $needReboot = $true }

    if ($needReboot -and $Phase -eq "Auto" -and -not $WhatIf) {
        Write-Host @"

Ozellikler etkinlestirildi. REBOOT GEREKLI.
Yeniden baslattiktan sonra (Yonetici PS):
  .\scripts\odak\install-wsl2-dev-host.ps1 -Phase PostReboot

"@ -ForegroundColor Yellow
        $answer = Read-Host "Simdi yeniden baslatilsin mi? (E/H)"
        if ($answer -match '^[EeYy]') {
            Restart-Computer -Force
        }
        exit 0
    }
}

if ($runPost -or ($Phase -eq "Auto" -and -not $needReboot)) {
    Write-Host "`n--- WSL2 kernel ---" -ForegroundColor Cyan
    Install-Wsl2KernelPackage

    if (-not $WhatIf) {
        Write-Host "wsl --update..." -ForegroundColor Cyan
        wsl --update 2>&1 | ForEach-Object { Write-Host $_ }
        wsl --set-default-version 2
    }

    if (-not $SkipUbuntu) {
        Write-Host "`n--- Ubuntu ---" -ForegroundColor Cyan
        Install-UbuntuDistro
    }

    if (-not $WhatIf) {
        Write-Host "`n--- Durum ---" -ForegroundColor Cyan
        wsl --status 2>&1 | ForEach-Object { Write-Host $_ }
        wsl -l -v 2>&1 | ForEach-Object { Write-Host $_ }
    }

    Write-Host "`n--- Docker Desktop ---" -ForegroundColor Cyan
    Start-DockerDesktopIfInstalled

    if (-not $WhatIf -and (Test-Wsl2Ready)) {
        Write-Host @"

WSL2 kurulumu tamamlandi.
Sonraki adim (offline deploy):
  .\scripts\odak\prefetch-odak-docker-base-images.ps1 -IncludeThirdParty
  .\scripts\odak\deploy-odak-prod-offline.ps1 -Services mngdocument

"@ -ForegroundColor Green
    }
}

if ($WhatIf) {
    Write-Host "WhatIf tamamlandi; degisiklik yapilmadi." -ForegroundColor Yellow
}
