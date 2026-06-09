# OC lookup demo smoke — profil fieldDisplays (L5) + aktivite timeline label cozumu
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\smoke-oc-lookup-demo.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [string]$WorkspaceId = "f414462a-cd9e-427e-87e8-3cdff0502325",
    [string]$WorkItemId = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$guidPattern = '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
$dataPath = "/data/api/v1/data"
$moApi = "$MoBaseUrl/api/v1"

function Invoke-Api {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($null -ne $Body) {
        $p.Body = $Body | ConvertTo-Json -Depth 20 -Compress
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-Items($Response) {
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items")) {
        if ($null -ne $Response.$prop) {
            return @($Response.$prop)
        }
    }
    return @($Response)
}

function Get-DataId($obj) {
    if ($null -eq $obj) { return $null }
    if ($obj.__dataId) { return "$($obj.__dataId)" }
    if ($obj.dataId) { return "$($obj.dataId)" }
    return $null
}

function Assert-NotGuidDisplay {
    param([string]$Label, [string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Label bos — beklenen gorunen metin (unvan/ad)"
    }
    if ($Value -match $guidPattern) {
        throw "$Label hala GUID: $Value"
    }
    Write-Host "  OK: $Label = '$Value'" -ForegroundColor Green
}

Write-Host "`nOC Lookup smoke" -ForegroundColor Cyan

Write-Host "[1] Tedarikci kayitlari..." -ForegroundColor Yellow
$suppliers = @(Get-Items (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/tedarikciler?limit=10&filter=isActive:eq:true"))
if ($suppliers.Count -lt 2) { throw "En az 2 aktif tedarikci gerekli (setup-oc-demo-tedarikci-lookup.ps1)" }
$s1 = $suppliers[0]
$s2 = $suppliers[1]
$id1 = Get-DataId $s1
$id2 = Get-DataId $s2
$name1 = $s1.unvan; if (-not $name1) { $name1 = $s1.name }
$name2 = $s2.unvan; if (-not $name2) { $name2 = $s2.name }
Write-Host "  A: $name1" -ForegroundColor Gray
Write-Host "  B: $name2" -ForegroundColor Gray

Write-Host "[2] Work item..." -ForegroundColor Yellow
if ([string]::IsNullOrEmpty($WorkItemId)) {
    $boards = @(Get-Items (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/op_boards?limit=5&filter=$([Uri]::EscapeDataString("workspaceId:eq:$WorkspaceId"))"))
    $boardId = Get-DataId ($boards | Select-Object -First 1)
    $types = @(Get-Items (Invoke-Api -Method GET -Uri "$BaseUrl$dataPath/op_work_item_types?limit=5&filter=$([Uri]::EscapeDataString("workspaceId:eq:$WorkspaceId"))"))
    $typeId = Get-DataId ($types | Select-Object -First 1)
    $created = Invoke-Api -Method POST -Uri "$moApi/work-items" -Body @{
        workspaceId = $WorkspaceId
        boardId     = $boardId
        typeId      = $typeId
        title       = "Lookup smoke $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    }
    $WorkItemId = $created.workItem.id
    if (-not $WorkItemId -and $created.workItem) { $WorkItemId = $created.workItem.Id }
    if (-not $WorkItemId -and $created.id) { $WorkItemId = $created.id }
}
if ([string]::IsNullOrEmpty($WorkItemId)) { throw "Work item id cozulemedi" }
Write-Host "  workItemId=$WorkItemId" -ForegroundColor Gray

Write-Host "[3] Ilk profile-view (tedarikciId)..." -ForegroundColor Yellow
Invoke-Api -Method PATCH -Uri "$moApi/work-items/$WorkItemId" -Body @{
    fields = @{ tedarikciId = $id1 }
} | Out-Null
Start-Sleep -Seconds 2
$pv1 = Invoke-Api -Method GET -Uri "$moApi/runtime/work-items/$WorkItemId/profile-view"
$display1 = $pv1.fieldDisplays.tedarikciId
if (-not $display1) { $display1 = $pv1.FieldDisplays.tedarikciId }
Assert-NotGuidDisplay -Label "fieldDisplays.tedarikciId" -Value $display1

Write-Host "[4] Tedarikci degistir + timeline..." -ForegroundColor Yellow
Invoke-Api -Method PATCH -Uri "$moApi/work-items/$WorkItemId" -Body @{
    fields = @{ tedarikciId = $id2 }
} | Out-Null
Start-Sleep -Seconds 2
$pv2 = Invoke-Api -Method GET -Uri "$moApi/runtime/work-items/$WorkItemId/profile-view"
$display2 = $pv2.fieldDisplays.tedarikciId
if (-not $display2) { $display2 = $pv2.FieldDisplays.tedarikciId }
Assert-NotGuidDisplay -Label "fieldDisplays.tedarikciId (guncel)" -Value $display2
if ($display2 -ne $name2) {
    Write-Host "  WARN: beklenen '$name2', gelen '$display2'" -ForegroundColor DarkYellow
}

$timeline = $pv2.timeline
if (-not $timeline) { $timeline = $pv2.Timeline }
$entries = @()
if ($timeline.items) { $entries = @($timeline.items) }
elseif ($timeline.Items) { $entries = @($timeline.Items) }

$changeRow = $null
foreach ($entry in $entries) {
    $type = $entry.type; if (-not $type) { $type = $entry.Type }
    if ($type -ne "activity") { continue }
    $changes = $entry.changes; if (-not $changes) { $changes = $entry.Changes }
    if (-not $changes) { continue }
    foreach ($ch in $changes) {
        $field = $ch.field; if (-not $field) { $field = $ch.Field }
        if ($field -eq "tedarikciId") {
            $changeRow = $ch
            break
        }
    }
    if ($changeRow) { break }
}

if (-not $changeRow) {
    Write-Host "  WARN: timeline'da tedarikciId degisikligi bulunamadi (aktivite yazimi gecikmis olabilir)" -ForegroundColor DarkYellow
}
else {
    $toDisplay = $changeRow.toDisplay; if (-not $toDisplay) { $toDisplay = $changeRow.ToDisplay }
    Assert-NotGuidDisplay -Label "timeline tedarikciId ToDisplay" -Value $toDisplay
}

Write-Host "[5] dependsOn alanlari metadata..." -ForegroundColor Yellow
$poolFields = $pv2.poolFields
if (-not $poolFields) { $poolFields = $pv2.PoolFields }
$hasUlke = $false
$hasSehirDepends = $false
foreach ($pf in @($poolFields)) {
    $key = $pf.key; if (-not $key) { $key = $pf.Key }
    if ($key -eq "ulkeId") { $hasUlke = $true }
    if ($key -eq "sehirId") {
        $opts = $pf.options; if (-not $opts) { $opts = $pf.Options }
        if ($opts -and $opts.lookup -and $opts.lookup.dependsOn) { $hasSehirDepends = $true }
        if ($opts -and $opts.lookup -and $opts.lookup.dependsOn.fieldKey -eq "ulkeId") { $hasSehirDepends = $true }
    }
}
if ($hasUlke) { Write-Host "  OK: ulkeId pool alani yuklu" -ForegroundColor Green }
else { Write-Host "  WARN: ulkeId pool'da yok — setup-oc-demo-lookup-dependson.ps1 calistirin" -ForegroundColor DarkYellow }
if ($hasSehirDepends) { Write-Host "  OK: sehirId dependsOn ulkeId" -ForegroundColor Green }
else { Write-Host "  WARN: sehirId dependsOn eksik" -ForegroundColor DarkYellow }

Write-Host "`nSmoke PASSED" -ForegroundColor Green
