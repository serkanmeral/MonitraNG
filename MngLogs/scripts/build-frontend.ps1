# MngLogs UI frontend build script
# Generates Nuxt static export and copies to MngLogs.Agent/wwwroot

$ErrorActionPreference = "Stop"
$uiDir = Join-Path $PSScriptRoot "..\Presentation\MngLogs.UI"
$wwwrootDir = Join-Path $PSScriptRoot "..\Presentation\MngLogs.Agent\wwwroot"
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

Write-Host "Copying to wwwroot..."
if (Test-Path $wwwrootDir) {
    Remove-Item $wwwrootDir -Recurse -Force
}
New-Item -ItemType Directory -Path $wwwrootDir -Force | Out-Null
Copy-Item -Path "$outputDir\*" -Destination $wwwrootDir -Recurse -Force

Write-Host "Frontend build complete. wwwroot ready."
exit 0
