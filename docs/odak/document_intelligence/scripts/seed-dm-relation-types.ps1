# F1-1 — dm_relation_types seed (idempotent upsert by code)
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/Seed-DmCatalogByCode.ps1")

$seedFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/seed-dm-relation-types.json"
Invoke-DmCatalogSeed -BaseUrl $BaseUrl -Token $Token -Dataset "dm_relation_types" -SeedFile $seedFile -Label "F1-1 relation types" -WhatIf:$WhatIf
