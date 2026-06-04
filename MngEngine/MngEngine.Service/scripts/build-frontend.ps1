# MngEngine UI frontend build script
# Generates Nuxt static export and copies to MngEngine.Api/wwwroot

$ErrorActionPreference = "Stop"
$uiDir = Join-Path $PSScriptRoot "..\Presentation\MngEngine.UI"
$wwwrootDir = Join-Path $PSScriptRoot "..\Presentation\MngEngine.Api\wwwroot"
$outputDir = Join-Path $uiDir ".output\public"

Write-Host "Building MngEngine UI..."
Push-Location $uiDir
try {
    npm ci 2>$null || npm install
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
