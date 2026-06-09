# op_work_items — wi_count_by_state predefined query (P2 widget)
#
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\patch-op-wi-count-by-state-query.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = $env:WIDGET_TOKEN
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadTokenScript)) {
    $token = & $loadTokenScript
}
if ([string]::IsNullOrEmpty($token)) {
    $tokenFile = "$env:TEMP\operationcore_dg_token.txt"
    if (Test-Path $tokenFile) {
        $token = (Get-Content $tokenFile -Raw).Trim()
    }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$newQuery = @{
    name        = "wi_count_by_state"
    description = "Open WorkItems count grouped by state (workspace donut widget)"
    pipeline    = @(
        @{
            '$match' = @{
                workspaceId = ":workspaceId"
                closedAt    = $null
            }
        },
        @{
            '$group' = @{
                _id   = '$stateId'
                count = @{ '$sum' = 1 }
            }
        },
        @{
            '$project' = @{
                _id       = 0
                stateId   = '$_id'
                stateName = '$_id'
                count     = 1
            }
        },
        @{ '$sort' = @{ count = -1 } }
    )
    parameters  = @(
        @{ name = "workspaceId"; type = "text"; required = $true }
    )
}

$datasetName = "op_work_items"
$getUri = "$BaseUrl/data/api/v1/datasets/$datasetName"
Write-Host "GET $getUri" -ForegroundColor Cyan
$dataset = Invoke-RestMethod -Method GET -Uri $getUri -Headers $headers -TimeoutSec 30

$queries = @()
if ($dataset.queries) { $queries = @($dataset.queries) }
elseif ($dataset.Queries) { $queries = @($dataset.Queries) }

$existing = $queries | Where-Object { $_.name -eq 'wi_count_by_state' -or $_.Name -eq 'wi_count_by_state' }
if ($existing) {
    Write-Host "wi_count_by_state zaten mevcut — guncelleniyor" -ForegroundColor Yellow
    $queries = @($queries | Where-Object { ($_.name -ne 'wi_count_by_state') -and ($_.Name -ne 'wi_count_by_state') })
}
$queries += $newQuery

$body = @{}
foreach ($prop in $dataset.PSObject.Properties) {
    if ($prop.Name -match '^queries$|^Queries$') { continue }
    $body[$prop.Name] = $prop.Value
}
$body['queries'] = $queries

$json = ($body | ConvertTo-Json -Depth 40 -Compress)
Write-Host "PUT $getUri" -ForegroundColor Cyan
Invoke-RestMethod -Method PUT -Uri $getUri -Headers $headers -Body $json -TimeoutSec 60 | Out-Null
Write-Host "OK wi_count_by_state eklendi/guncellendi" -ForegroundColor Green
