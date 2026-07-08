# G4 — dm_data_sources seed (idempotent upsert by code)
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/Seed-DmCatalogByCode.ps1")

$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-dm-data-sources.json"
Invoke-DmCatalogSeed -BaseUrl $BaseUrl -Token $Token -Dataset "dm_data_sources" -SeedFile $seedFile -Label "G4 data sources" -WhatIf:$WhatIf
