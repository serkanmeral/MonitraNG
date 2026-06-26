# dm_document_templates (ve gerekirse dm_template_categories) şema yaması — D1-beta alanları
# Prod'da dataset "zaten var" ile oluşturulduysa setup script şemayı güncellemez; bu script PUT yapar.
#
#   .\get-operationcore-token-prod.ps1
#   .\patch-document-intelligence-templates-dataset.ps1
#   .\patch-document-intelligence-templates-dataset.ps1 -BaseUrl "http://192.168.20.20:5040" -WhatIf

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$datasetsFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/documentintelligence_datasets_phase1.json"
$isProd = $BaseUrl -match "192\.168\.20\.8"

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok. -Token veya OC token script." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}
$datasetsPath = "/data/api/v1/datasets"
$utf8 = [System.Text.Encoding]::UTF8

$schemas = Get-Content $datasetsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$byName = @{}
foreach ($s in $schemas) { $byName[$s.name] = $s }

$targets = @("dm_document_templates")
foreach ($name in $targets) {
    if (-not $byName.ContainsKey($name)) { throw "Schema missing: $name" }
    $schema = $byName[$name]
    $bodyObj = @{
        Description = [string]$schema.description
        ForceSchema = [bool]$schema.forceSchema
        Logging     = [string]$schema.logging
        PublishMode = [string]$schema.publish_mode
        Fields      = @($schema.fields)
        Validations = @($schema.validations)
        Queries     = @($schema.queries)
        IndexList   = @($schema.indexList)
    }
    $bodyJson = $bodyObj | ConvertTo-Json -Depth 30 -Compress
    $uri = "$BaseUrl$datasetsPath/$name"

    Write-Host ""
    Write-Host "PATCH dataset: $name" -ForegroundColor Cyan
    Write-Host "  Fields: $($schema.fields.Count)" -ForegroundColor Gray

    if ($WhatIf) {
        Write-Host "  WhatIf PUT $uri" -ForegroundColor Yellow
        continue
    }

    try {
        $bytes = $utf8.GetBytes($bodyJson)
        $r = Invoke-RestMethod -Uri $uri -Method PUT -Headers $headers -Body $bytes -ContentType "application/json; charset=utf-8"
        Write-Host "  OK updated (dataId=$($r.dataId))" -ForegroundColor Green
    } catch {
        $msg = $_.ErrorDetails.Message
        if (-not $msg) { $msg = $_.Exception.Message }
        Write-Host "  HATA: $msg" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Cyan
