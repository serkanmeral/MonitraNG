# siem.scenario-cards sablonunu isActive=true yap (Faz 3 composite)
#
#   .\docs\odak\widgets\scripts\activate-siem-scenario-cards.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = $env:WIDGET_TOKEN
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadTokenScript)) {
    $token = & $loadTokenScript
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization   = "Bearer $token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}

$tid = 'siem.scenario-cards'
$dataPath = "/data/api/v1/data/@widget_templates"
$filter = [uri]::EscapeDataString("templateId:eq:$tid")
$listUri = "$BaseUrl$dataPath`?filter=$filter&limit=1"
$list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET -TimeoutSec 30
$row = $null
if ($list -is [array] -and $list.Count -gt 0) { $row = $list[0] }
elseif ($list.items -and $list.items.Count -gt 0) { $row = $list.items[0] }

if (-not $row) {
    Write-Host "SKIP $tid — kayit yok" -ForegroundColor Yellow
    exit 1
}

$id = $row.__dataId
if (-not $id) { $id = $row.dataId }
$body = @{}
foreach ($prop in $row.PSObject.Properties) {
    if ($prop.Name -notmatch '^_' -and $prop.Name -ne 'dataId') {
        $body[$prop.Name] = $prop.Value
    }
}
$body['isActive'] = $true
if ($body.ContainsKey('category') -and $body['category'] -is [System.Management.Automation.PSCustomObject]) {
    $cat = $body['category']
    if ($cat.__dataId) { $body['category'] = [string]$cat.__dataId }
    elseif ($cat.dataId) { $body['category'] = [string]$cat.dataId }
}

$json = ($body | ConvertTo-Json -Depth 30 -Compress)
Invoke-RestMethod -Uri "$BaseUrl$dataPath/$id" -Headers $headers -Method PUT -Body $json -TimeoutSec 30 | Out-Null
Write-Host "OK $tid isActive=true" -ForegroundColor Green
