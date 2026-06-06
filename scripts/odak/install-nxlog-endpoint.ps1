# Windows NxLog CE — Security log (endpoint) -> MngEngine wec-batch
# WEC yoksa bu script kullanilir. WEC icin: templates/nxlog-wec-to-engine.conf
param(
    [string]$EngineUrl = "http://192.168.20.20:5037",
    [string]$SourceHost = "",
    [ValidateSet("Endpoint", "Wec")]
    [string]$Mode = "Endpoint",
    [string]$NxLogVersion = "3.2.2329",
    [int]$NxLogFileId = 833,
    [switch]$Apply,
    [switch]$SkipDownload,
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-SourceHostFqdn {
    if (-not [string]::IsNullOrWhiteSpace($SourceHost)) { return $SourceHost.Trim() }
    try {
        return [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
    } catch {
        return $env:COMPUTERNAME
    }
}

function Get-NxLogInstallRoot {
    $candidates = @(
        "${env:ProgramFiles}\nxlog",
        "${env:ProgramFiles(x86)}\nxlog"
    )
    foreach ($p in $candidates) {
        if (Test-Path (Join-Path $p "nxlog.exe")) { return $p }
    }
    return $candidates[0]
}

function Get-NxLogConfPath {
    param([string]$InstallRoot)
    $paths = @(
        (Join-Path $InstallRoot "conf\nxlog.conf"),
        (Join-Path $InstallRoot "nxlog.conf")
    )
    foreach ($p in $paths) {
        if (Test-Path (Split-Path $p -Parent)) { return $p }
    }
    return $paths[0]
}

function Save-NxLogCeMsi {
    param(
        [string]$Destination,
        [string]$Version,
        [int]$FileId
    )

    if ((Test-Path $Destination) -and ((Get-Item $Destination).Length -gt 2MB)) {
        Write-Host "MSI mevcut: $Destination" -ForegroundColor DarkGray
        return
    }

    Write-Host "NxLog CE MSI indiriliyor (file_id=$FileId)..." -ForegroundColor Yellow
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "https://nxlog.co/products/nxlog-community-edition/download" -WebSession $session -UseBasicParsing
    if ($page.Content -notmatch 'csrf-token" content="([^"]+)"') {
        throw "NxLog indirme sayfasinda CSRF token bulunamadi"
    }
    $csrf = $Matches[1]
    $headers = @{
        "X-CSRF-Token"     = $csrf
        "X-Requested-With" = "XMLHttpRequest"
        Accept             = "application/json"
    }
    $resp = Invoke-RestMethod -Uri "https://nxlog.co/downloads" -Method POST -WebSession $session `
        -Headers $headers -ContentType "application/json" -Body (@{ file_id = $FileId } | ConvertTo-Json)
    if (-not $resp.success -or [string]::IsNullOrWhiteSpace($resp.data.link)) {
        throw "NxLog indirme API basarisiz: $($resp.message)"
    }
    Invoke-WebRequest -Uri $resp.data.link -OutFile $Destination -WebSession $session -UseBasicParsing
    $magic = [BitConverter]::ToString([IO.File]::ReadAllBytes($Destination)[0..3])
    if ($magic -ne "D0-CF-11-E0") {
        throw "Indirilen dosya gecerli MSI degil (magic=$magic). Manuel: https://nxlog.co/products/nxlog-community-edition/download"
    }
    Write-Host "MSI OK: $Destination ($((Get-Item $Destination).Length) bytes)" -ForegroundColor Green
}

function Install-NxLogCeMsi {
    param([string]$MsiPath)
    Write-Host "NxLog CE kuruluyor (sessiz)..." -ForegroundColor Yellow
    $proc = Start-Process -FilePath "msiexec.exe" -ArgumentList @("/i", "`"$MsiPath`"", "/qn", "/norestart") -Wait -PassThru
    if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne 3010) {
        throw "msiexec exit=$($proc.ExitCode)"
    }
    Write-Host "NxLog CE kurulumu tamam." -ForegroundColor Green
}

function Set-NxLogMachineEnv {
    param(
        [string]$EngineUrl,
        [string]$HostFqdn
    )
    [Environment]::SetEnvironmentVariable("MONITRA_ENGINE_URL", $EngineUrl, "Machine")
    [Environment]::SetEnvironmentVariable("MONITRA_WEC_HOST", $HostFqdn, "Machine")
    [Environment]::SetEnvironmentVariable("MONITRA_SOURCE_HOST", $HostFqdn, "Machine")
}

function Install-NxLogConfig {
    param(
        [string]$TemplateName,
        [string]$InstallRoot
    )

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
    $templatePath = Join-Path $repoRoot "docs/odak/monitoring/templates/$TemplateName"
    if (-not (Test-Path $templatePath)) { throw "Sablon bulunamadi: $templatePath" }

    $confPath = Get-NxLogConfPath -InstallRoot $InstallRoot
    $backup = "$confPath.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    if (Test-Path $confPath) {
        Copy-Item $confPath $backup -Force
        Write-Host "Yedek: $backup" -ForegroundColor DarkGray
    }

    $confDir = Split-Path $confPath -Parent
    if (-not (Test-Path $confDir)) { New-Item -ItemType Directory -Path $confDir -Force | Out-Null }
    Copy-Item $templatePath $confPath -Force
    Write-Host "Config: $confPath ($TemplateName)" -ForegroundColor Green
}

function Restart-NxLogService {
    $svc = Get-Service -Name nxlog -ErrorAction SilentlyContinue
    if (-not $svc) { throw "nxlog servisi bulunamadi — kurulum basarisiz olabilir" }
    if ($svc.Status -eq "Running") {
        Restart-Service nxlog -Force
    } else {
        Set-Service nxlog -StartupType Automatic
        Start-Service nxlog
    }
    Start-Sleep -Seconds 2
    $svc = Get-Service nxlog
    Write-Host "nxlog servisi: $($svc.Status)" -ForegroundColor $(if ($svc.Status -eq "Running") { "Green" } else { "Red" })
    if ($svc.Status -ne "Running") { throw "nxlog servisi calismiyor" }
}

# --- main ---
$hostFqdn = Get-SourceHostFqdn
$template = if ($Mode -eq "Wec") { "nxlog-wec-to-engine.conf" } else { "nxlog-endpoint-to-engine.conf" }
$msiPath = Join-Path $env:TEMP "nxlog-ce-$NxLogVersion.msi"

Write-Host "=== NxLog endpoint kurulumu ===" -ForegroundColor Cyan
Write-Host "Engine : $EngineUrl" -ForegroundColor DarkGray
Write-Host "Host   : $hostFqdn" -ForegroundColor DarkGray
Write-Host "Mode   : $Mode ($template)" -ForegroundColor DarkGray

if (-not $Apply) {
    Write-Host "`nDry-run — uygulamak icin -Apply (yönetici gerekir)" -ForegroundColor Yellow
    exit 0
}

if (-not (Test-IsAdministrator)) {
    Write-Host "Yönetici hakki gerekli; UAC ile yeniden baslatiliyor..." -ForegroundColor Yellow
    $argList = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PSCommandPath,
        "-EngineUrl", $EngineUrl,
        "-SourceHost", $hostFqdn,
        "-Mode", $Mode,
        "-Apply"
    )
    if ($SkipDownload) { $argList += "-SkipDownload" }
    if ($SkipInstall) { $argList += "-SkipInstall" }
    Start-Process -FilePath "pwsh.exe" -ArgumentList $argList -Verb RunAs -Wait
    exit $LASTEXITCODE
}

if (-not $SkipDownload) {
    Save-NxLogCeMsi -Destination $msiPath -Version $NxLogVersion -FileId $NxLogFileId
}

if (-not $SkipInstall) {
    $root = Get-NxLogInstallRoot
    if (-not (Test-Path (Join-Path $root "nxlog.exe"))) {
        Install-NxLogCeMsi -MsiPath $msiPath
    } else {
        Write-Host "NxLog zaten kurulu: $root" -ForegroundColor DarkGray
    }
}

$installRoot = Get-NxLogInstallRoot
Set-NxLogMachineEnv -EngineUrl $EngineUrl -HostFqdn $hostFqdn
Install-NxLogConfig -TemplateName $template -InstallRoot $installRoot
Restart-NxLogService

Write-Host "`nOK NxLog endpoint hazir." -ForegroundColor Green
Write-Host "Smoke: pwsh scripts/odak/test-nxlog-wec-template-e2e.ps1 -EngineUrl $EngineUrl" -ForegroundColor DarkGray
Write-Host "Canli olay: basarisiz oturum acmayi dene (4625) veya auditpol /get /category:*" -ForegroundColor DarkGray
exit 0
