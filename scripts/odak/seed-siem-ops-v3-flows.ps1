# Seed / upsert Odak SIEM ops flows (v3) as user drafts, then publish STOPPED (enabled=false).
# Default: only U1. Requires MngAlarm scenarios API.
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$PackageId = "siem-ops-v3",
    [string[]]$TemplateIds = @("U1"),
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$null = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$tokenFile = Join-Path $env:TEMP "serkan_token.txt"
if (-not (Test-Path $tokenFile)) { throw "Token dosyasi yok: $tokenFile" }
$token = (Get-Content -Path $tokenFile -Raw).Trim()
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }

$scenariosApi = "$Gateway/alarm/api/v1/scenarios"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manifestPath = Join-Path $repoRoot "tests/fixtures/siem/scenario_templates/packages/$PackageId/manifest.json"
if (-not (Test-Path $manifestPath)) { throw "Manifest yok: $manifestPath" }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
Write-Host "=== Seed SIEM ops flows $PackageId ($Domain) ===" -ForegroundColor Cyan
if (-not $Apply) { Write-Host "   Dry-run (-Apply ile uygula)" -ForegroundColor Yellow }

try {
    $null = Invoke-RestMethod -Uri "$scenariosApi`?includeDrafts=true" -Headers $hdr
} catch {
    throw "Scenarios API erisilemiyor (MngAlarm deploy gerekli): $($_.Exception.Message)"
}

function Get-Array {
    param($Raw)
    if ($null -eq $Raw) { return @() }
    if ($Raw -is [System.Array]) { return @($Raw) }
    return @($Raw)
}

$existing = Get-Array (Invoke-RestMethod -Uri "$scenariosApi`?includeDrafts=true" -Headers $hdr)
$created = 0
$published = 0

foreach ($templateId in $TemplateIds) {
    $tpl = @($manifest.templates) | Where-Object { $_.templateId -eq $templateId } | Select-Object -First 1
    if (-not $tpl) { throw "Template bulunamadi: $templateId" }

    $name = [string]$tpl.name
    $body = @{
        name       = $name
        severity   = [int]$tpl.severity
        enabled    = $false
        definition = $tpl.definition
    }

    $already = @($existing) | Where-Object {
        $_.name -eq $name -or ($_.templateId -eq $templateId -and $_.origin -eq "user")
    } | Select-Object -First 1

    if ($already -and $already.publishedVersion) {
        Write-Host "   SKIP published $($already.scenarioId) name=$($already.name) enabled=$($already.enabled)" -ForegroundColor DarkGray
        continue
    }

    if (-not $Apply) {
        if ($already) {
            Write-Host "   WOULD resume/publish STOPPED: $($already.name)" -ForegroundColor DarkGray
        } else {
            Write-Host "   WOULD create+publish STOPPED: $name ($templateId)" -ForegroundColor DarkGray
        }
        $created++
        continue
    }

    if ($already) {
        Write-Host "   RESUME draft $($already.scenarioId)" -ForegroundColor Yellow
        $draft = Invoke-RestMethod -Uri "$scenariosApi/$($already.scenarioId)" -Headers $hdr
    } else {
        $draft = Invoke-RestMethod -Uri "$scenariosApi/drafts" -Method POST -Headers $hdr -Body ($body | ConvertTo-Json -Depth 30)
        Write-Host "   DRAFT $($draft.scenarioId) v$($draft.version)" -ForegroundColor Green
        $created++
    }

    $validation = Invoke-RestMethod -Uri "$scenariosApi/$($draft.scenarioId)/versions/$($draft.version)/validate" `
        -Method POST -Headers $hdr
    if ($validation.isValid -ne $true) {
        $diag = ($validation.diagnostics | ConvertTo-Json -Compress)
        throw "Validate failed for $($draft.scenarioId): $diag"
    }
    Write-Host "   VALIDATED $($draft.scenarioId) v$($draft.version)" -ForegroundColor Green

    $pub = Invoke-RestMethod -Uri "$scenariosApi/$($draft.scenarioId)/versions/$($draft.version)/publish" `
        -Method POST -Headers $hdr
    if ($pub.enabled -eq $true) {
        Invoke-RestMethod -Uri "$scenariosApi/$($draft.scenarioId)/versions/$($pub.version)/enabled" `
            -Method POST -Headers $hdr -Body (@{ enabled = $false } | ConvertTo-Json) | Out-Null
        Write-Host "   PUBLISHED then forced STOP $($pub.scenarioId) v$($pub.version)" -ForegroundColor Yellow
    } else {
        Write-Host "   PUBLISHED STOPPED $($pub.scenarioId) v$($pub.version)" -ForegroundColor Green
    }
    $published++
}

Write-Host "Summary: wouldOrCreated=$created published=$published" -ForegroundColor Cyan
Write-Host "Next: Flow Lab'de U1'i review et; onaydan sonra Çalıştır." -ForegroundColor Cyan
if (-not $Apply) { Write-Host "OK dry-run" -ForegroundColor Green } else { Write-Host "OK seed done (stopped)" -ForegroundColor Green }
exit 0
