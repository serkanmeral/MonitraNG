# Geriye uyumluluk: eski "Ogreticiler" kok seed'i yerine MonitraNG/Ogreticiler/Manager yapisini kullanin.
# Usage: .\seed-tutorials.ps1 [-BaseUrl ...] [-WhatIf]

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$newScript = Join-Path $scriptDir "seed-monitrang-tutorials.ps1"

$server = if ($BaseUrl -match "192\.168\.20\.8") { "192.168.20.8" } else { "192.168.20.20" }

$params = @{
    BaseUrl = $BaseUrl
    Server  = $server
}
if ($WhatIf) { $params.WhatIf = $true }

Write-Host "Not: seed-tutorials.ps1 -> seed-monitrang-tutorials.ps1 yonlendiriliyor." -ForegroundColor Gray
& $newScript @params
