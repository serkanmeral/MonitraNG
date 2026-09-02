# F1-1 — dm_resource_kinds seed (idempotent upsert by code)
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/Seed-DmCatalogByCode.ps1")

$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-dm-resource-kinds.json"
Invoke-DmCatalogSeed -BaseUrl $BaseUrl -Token $Token -Dataset "dm_resource_kinds" -SeedFile $seedFile -Label "F1-1 resource kinds" -WhatIf:$WhatIf
