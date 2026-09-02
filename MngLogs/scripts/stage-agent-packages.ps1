#Requires -Version 7.0
<#
.SYNOPSIS
  Stages Windows MSI + Linux tar.gz for MngLogCollector /agent/packages.

.DESCRIPTION
  Copies files into a folder (default: MngLogs/artifacts/agent-packages) and writes manifest.json
  with SHA-256. Collector serves this folder from /var/lib/mnglogcollector/agent-packages.
#>
param(
    [string] $WindowsMsi = "",
    [string] $LinuxTarGz = "",
    [string] $OutputDir = "",
    [string] $WindowsVersion = "",
    [string] $LinuxVersion = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDir) {
    $OutputDir = Join-Path $root "artifacts\agent-packages"
}

function Get-VersionFromName([string] $name, [string] $fallback) {
    if ($fallback) { return $fallback }
    if ($name -match '(\d+\.\d+\.\d+(?:\.\d+)?)') { return $Matches[1] }
    return ""
}

function Get-Sha256Lower([string] $path) {
    return (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$packages = @()

if ($WindowsMsi) {
    if (-not (Test-Path -LiteralPath $WindowsMsi)) { throw "Windows MSI not found: $WindowsMsi" }
    $src = Get-Item -LiteralPath $WindowsMsi
    $dest = Join-Path $OutputDir "windows.msi"
    Copy-Item -LiteralPath $src.FullName -Destination $dest -Force
    $ver = Get-VersionFromName $src.Name $WindowsVersion
    $packages += [ordered]@{
        id              = "windows"
        platform        = "windows"
        fileName        = "windows.msi"
        displayFileName = $src.Name
        version         = $ver
        sha256          = Get-Sha256Lower $dest
    }
    Write-Host "Windows: $($src.Name) -> windows.msi (v$ver)"
}

if ($LinuxTarGz) {
    if (-not (Test-Path -LiteralPath $LinuxTarGz)) { throw "Linux tar.gz not found: $LinuxTarGz" }
    $src = Get-Item -LiteralPath $LinuxTarGz
    $dest = Join-Path $OutputDir "linux.tar.gz"
    Copy-Item -LiteralPath $src.FullName -Destination $dest -Force
    $ver = Get-VersionFromName $src.Name $LinuxVersion
    $packages += [ordered]@{
        id              = "linux"
        platform        = "linux"
        fileName        = "linux.tar.gz"
        displayFileName = $src.Name
        version         = $ver
        sha256          = Get-Sha256Lower $dest
    }
    Write-Host "Linux: $($src.Name) -> linux.tar.gz (v$ver)"
}

if ($packages.Count -eq 0) {
    throw "Provide -WindowsMsi and/or -LinuxTarGz"
}

$manifest = [ordered]@{ packages = $packages }
$manifestPath = Join-Path $OutputDir "manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding utf8
Write-Host "Wrote $manifestPath"
Write-Host "Copy this folder to the collector host: /home/odak/mnglogs-agent-packages"
exit 0
