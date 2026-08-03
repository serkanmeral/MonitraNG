# MngLogs UI frontend build script
# Generates Nuxt static export and copies to Agent wwwroots (Windows + Linux)

param(
    [ValidateSet('windows', 'linux', 'both')]
    [string]$Target = 'both'
)

$ErrorActionPreference = "Stop"
$uiDir = Join-Path $PSScriptRoot "..\Presentation\MngLogs.UI"
$winWwwroot = Join-Path $PSScriptRoot "..\Presentation\MngLogs.Agent\wwwroot"
$linuxWwwroot = Join-Path $PSScriptRoot "..\Presentation\MngLogs.Agent.Linux\wwwroot"
$outputDir = Join-Path $uiDir ".output\public"

Write-Host "Building MngLogs UI..."
Push-Location $uiDir
try {
    if (Test-Path "package-lock.json") {
        npm ci
    } else {
        npm install
    }
    npm run generate
} finally {
    Pop-Location
}

if (-not (Test-Path $outputDir)) {
    Write-Error "Nuxt output not found at $outputDir"
    exit 1
}

function Copy-Wwwroot([string]$dest) {
    Write-Host "Copying to $dest ..."
    if (Test-Path $dest) {
        Remove-Item $dest -Recurse -Force
    }
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item -Path "$outputDir\*" -Destination $dest -Recurse -Force
}

if ($Target -eq 'windows' -or $Target -eq 'both') {
    Copy-Wwwroot $winWwwroot
}
if ($Target -eq 'linux' -or $Target -eq 'both') {
    Copy-Wwwroot $linuxWwwroot
}

Write-Host "Frontend build complete (target=$Target)."
exit 0
