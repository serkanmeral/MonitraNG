# Odak Uretim panosu — liste kolonlarina closedAt + customerOrderRef filtre destegi
#
# Usage:
#   .\docs\odak\siparis\scripts\patch-odak-siparis-board-list.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$BoardId = "",
    [string]$SeedFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrEmpty($SeedFile)) {
    $SeedFile = Join-Path $repoRoot "docs/odak/is_surecleri/seed/odak-uretim-seed.json"
}
if ([string]::IsNullOrEmpty($BoardId) -and (Test-Path $SeedFile)) {
    $seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($seed.boardPackageId) {
        $BoardId = [string]$seed.boardPackageId
    }
    elseif ($seed.boardProdId) {
        $BoardId = [string]$seed.boardProdId
    }
}
if ([string]::IsNullOrEmpty($BoardId)) {
    $BoardId = "75ec624c-a8be-4131-b072-9408ace1fd32"
}
$ocScriptDir = Join-Path (Split-Path (Split-Path $scriptDir -Parent) -Parent) "operationcore\scripts"
$loadTokenScript = Join-Path $ocScriptDir "load-operationcore-token.ps1"

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

function Get-RelationId($value) {
    if ($null -eq $value) { return $null }
    if ($value -is [string]) { return $value }
    if ($value.__dataId) { return [string]$value.__dataId }
    if ($value.dataId) { return [string]$value.dataId }
    return [string]$value
}

$uri = "$BaseUrl/data/api/v1/data/op_boards/$BoardId"
$board = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET
$row = if ($board -is [Array]) { $board[0] } else { $board }

$config = @{}
if ($row.config) {
    $row.config.PSObject.Properties | ForEach-Object { $config[$_.Name] = $_.Value }
}

$listColumns = @()
if ($config.listColumns) {
    foreach ($c in @($config.listColumns)) {
        $col = @{}
        $c.PSObject.Properties | ForEach-Object { $col[$_.Name] = $_.Value }
        $listColumns += $col
    }
}

function Ensure-Column {
    param([string]$Key, [hashtable]$Defaults)
    $idx = 0
    foreach ($c in $listColumns) {
        if ([string]$c.key -eq $Key) {
            foreach ($k in $Defaults.Keys) {
                if ($k -eq "filterable" -and $Defaults.filterable -eq $true) {
                    $listColumns[$idx].filterable = $true
                }
                elseif (-not $listColumns[$idx].ContainsKey($k)) {
                    $listColumns[$idx][$k] = $Defaults[$k]
                }
            }
            return
        }
        $idx++
    }
    $listColumns += ($Defaults + @{ key = $Key })
}

Ensure-Column -Key "packageNo" -Defaults @{
    label = "Is paketi no"
    sortable = $true
    filterable = $true
}
Ensure-Column -Key "beginDate" -Defaults @{
    label = "Baslangic"
    sortable = $true
    filterable = $true
    format = "date"
}
Ensure-Column -Key "closedAt" -Defaults @{
    label = "Kapanis"
    sortable = $true
    filterable = $true
    format = "date"
}
Ensure-Column -Key "customerOrderRef" -Defaults @{
    label = "Musteri PO"
    sortable = $true
    filterable = $true
}

$config["listColumns"] = $listColumns
if ($row.config.columns) { $config["columns"] = $row.config.columns }
if ($row.config.defaultSort) { $config["defaultSort"] = $row.config.defaultSort }

Write-Host "  listColumns: $($listColumns.Count) (closedAt + customerOrderRef dahil)" -ForegroundColor Gray

$body = @{
    name                 = [string]$row.name
    workspaceId          = Get-RelationId $row.workspaceId
    viewType             = [string]$row.viewType
    defaultStateFlowId   = Get-RelationId $row.defaultStateFlowId
    defaultFormId        = Get-RelationId $row.defaultFormId
    isDefault            = [bool]$row.isDefault
    visibleFields        = @($row.visibleFields)
    config               = $config
}
if ($null -ne $row.defaultDashboardId) {
    $body.defaultDashboardId = Get-RelationId $row.defaultDashboardId
}

$json = $body | ConvertTo-Json -Depth 25 -Compress
Invoke-RestMethod -Uri $uri -Headers $headers -Method PUT -Body $json | Out-Null
Write-Host "OK: op_boards listColumns guncellendi (closedAt, customerOrderRef filterable)" -ForegroundColor Green

$wsId = Get-RelationId $row.workspaceId
if ($wsId) {
    try {
        $reloadUri = "$BaseUrl/operations/api/v1/workspaces/$wsId/metadata-cache/reload"
        $reload = Invoke-RestMethod -Uri $reloadUri -Headers $headers -Method POST
        Write-Host "OK: metadata cache reload (keysRemoved=$($reload.keysRemoved))" -ForegroundColor Green
    }
    catch {
        Write-Host "WARN: metadata cache reload: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
