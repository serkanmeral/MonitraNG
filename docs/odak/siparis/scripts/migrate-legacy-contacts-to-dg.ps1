# Legacy contacts -> odak_musteri_kisileri (DG, idempotent)
#
# Usage:
#   .\export-legacy-contacts-from-sql.ps1
#   .\migrate-legacy-contacts-to-dg.ps1
#   .\migrate-legacy-contacts-to-dg.ps1 -BaseUrl http://192.168.20.8:5040 -DryRun

param(
    [string]$LegacyContactsJsonPath = "",
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrEmpty($LegacyContactsJsonPath)) {
    $LegacyContactsJsonPath = Join-Path $scriptDir "..\datasets\legacy-contacts.json"
}
$mappingFile = Join-Path $scriptDir "..\datasets\migration-firm-mapping.json"

if (-not (Test-Path $LegacyContactsJsonPath)) {
    throw "Contacts JSON yok: $LegacyContactsJsonPath — once export-legacy-contacts-from-sql.ps1"
}
if (-not (Test-Path $mappingFile)) {
    throw "Firm mapping yok: $mappingFile — once migrate-legacy-firms-to-dg.ps1"
}

$env:MNG_OC_USE_PROD_TOKEN = "1"
$tokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$ctx = Initialize-DgMigrationHeaders -TokenScriptPath $tokenScript
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

function Get-DgItems {
    param($Response)
    if ($Response -is [Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    return @()
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Find-ExistingContactByLegacyId {
    param([string]$LegacyContactId)
    $skip = 0
    $limit = 500
    while ($true) {
        $uri = "$BaseUrl$dataPath/odak_musteri_kisileri?skip=$skip&limit=$limit"
        $items = Get-DgItems (Invoke-DgMigrationApi -AuthContext $ctx -Method GET -Uri $uri -RetryOnUnauthorized)
        foreach ($item in $items) {
            if ([string]$item.legacyContactId -eq $LegacyContactId) { return $item }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $null
}

$raw = Get-Content $LegacyContactsJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$contacts = @($raw.contacts)
$firmMapRaw = Get-Content $mappingFile -Raw -Encoding UTF8 | ConvertFrom-Json
$firmMap = @{}
$firmSource = $firmMapRaw.firms
if (-not $firmSource) { $firmSource = $firmMapRaw }
foreach ($prop in $firmSource.PSObject.Properties) {
    $firmMap[$prop.Name] = [string]$prop.Value
}

Write-Host "`n=== migrate-legacy-contacts-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $LegacyContactsJsonPath ($($contacts.Count) contact)" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl  DryRun: $DryRun`n" -ForegroundColor Cyan

$contactMapping = @{}
$created = 0
$skipped = 0
$noFirm = 0

foreach ($contact in $contacts) {
    $legacyContactId = [string]$contact.id
    $legacyFirmId = [string]$contact.firm_id
    if (-not $firmMap.ContainsKey($legacyFirmId)) {
        $noFirm++
        continue
    }
    $parentCustomerId = $firmMap[$legacyFirmId]

    $existing = Find-ExistingContactByLegacyId -LegacyContactId $legacyContactId
    if ($existing) {
        $dgId = $existing.__dataId; if (-not $dgId) { $dgId = $existing.dataId }
        $contactMapping[$legacyContactId] = $dgId
        $skipped++
        continue
    }

    $body = @{
        parentCustomerId = $parentCustomerId
        ad               = Limit-LegacyText $contact.ad 120
        email            = Limit-LegacyText $contact.email 200
        telefon          = Limit-LegacyText $contact.tel 40
        gorevUnvani      = Limit-LegacyText $contact.position 120
        birincilKisi     = $false
        aktif            = $true
        legacyContactId  = $legacyContactId
    }
    if (-not $body.telefon) { $body.telefon = $null }
    if (-not $body.gorevUnvani) { $body.gorevUnvani = $null }

    if ($DryRun) {
        Write-Host "[DRY] legacyContactId=$legacyContactId -> $($body.ad)" -ForegroundColor Yellow
        $contactMapping[$legacyContactId] = "DRY-RUN"
        $created++
        continue
    }

    $resp = Invoke-DgMigrationApi -AuthContext $ctx -Method POST -Uri "$BaseUrl$dataPath/odak_musteri_kisileri" -Body $body -RetryOnUnauthorized
    $dgId = Get-DataId $resp
    $contactMapping[$legacyContactId] = $dgId
    $created++
    if ($created % 50 -eq 0) { Write-Host "  ... $created olusturuldu" -ForegroundColor Gray }
}

if (-not $DryRun) {
    foreach ($contact in $contacts) {
        $legacyContactId = [string]$contact.id
        if ($contactMapping.ContainsKey($legacyContactId)) { continue }
        $hit = Find-ExistingContactByLegacyId -LegacyContactId $legacyContactId
        if ($hit) {
            $dgId = $hit.__dataId; if (-not $dgId) { $dgId = $hit.dataId }
            $contactMapping[$legacyContactId] = $dgId
        }
    }
    $mapPath = Join-Path $scriptDir "..\datasets\migration-contact-mapping.json"
    Write-Utf8JsonFile -Path $mapPath -Object $contactMapping -Depth 4
    Write-Host "Mapping: $mapPath ($($contactMapping.Count) kayit)" -ForegroundColor Green
}

Write-Host "`nOzet: created=$created skipped=$skipped noFirm=$noFirm mapped=$($contactMapping.Count)" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "(DryRun — DG guncellenmedi)" -ForegroundColor DarkGray
}
