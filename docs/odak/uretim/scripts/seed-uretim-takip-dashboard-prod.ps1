# Üretim Takip — güncel aşama panosu (production)
# Çalıştırma: pwsh -File .\docs\odak\uretim\scripts\seed-uretim-takip-dashboard-prod.ps1

param([string]$BaseUrl = "http://192.168.20.8:5040")

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) { throw "pwsh 7 gerekli." }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$env:MNG_OC_USE_PROD_TOKEN = "1"
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1") -AutoRefresh
$headers = @{ Authorization = "Bearer $($token.Trim())" }
$dataPath = "/data/api/v1/data"

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 30 -Compress
        $p.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $p.ContentType = "application/json; charset=utf-8"
    }
    return Invoke-RestMethod @p
}
function Get-Items($Response) {
    if (-not $Response) { return @() }
    if ($Response.data) { return @($Response.data) }
    if ($Response -is [System.Array]) { return @($Response) }
    return @($Response)
}
function Get-DataId($Response) {
    $d = if ($Response.data) { $Response.data } else { $Response }
    $id = [string]$d.__dataId
    if (-not $id) { $id = [string]$d.dataId }
    return $id
}
function Find-One([string]$Collection, [string]$Filter) {
    $uri = "$BaseUrl$dataPath/$Collection`?limit=5&expand=false&filter=" + [Uri]::EscapeDataString($Filter)
    $rows = @(Get-Items (Invoke-Dg GET $uri))
    if ($rows.Count -lt 1) { throw "Bulunamadi: $Collection $Filter" }
    return $rows[0]
}

$ws = Find-One "op_workspaces" "name:eq:Üretim Takip"
$workspaceId = Get-DataId $ws
Write-Host "workspace $workspaceId"

function State-Id([string]$Title) {
    $row = Find-One "op_states" "name:eq:Üretim Takip - $Title"
    return (Get-DataId $row)
}
$states = [ordered]@{
    kapandi   = State-Id "Kapandı"
    sevk      = State-Id "Sevk edildi"
    kalite    = State-Id "Kalite"
    uretim    = State-Id "Üretim"
    montaj    = State-Id "Montaj"
    baslamadi = State-Id "Başlamadı"
    boya      = State-Id "Boya"
    talasli   = State-Id "Talaşlı"
}

$board = Find-One "op_boards" "workspaceId:eq:$workspaceId,name:eq:Üretim Takip - İş emirleri"
$boardId = Get-DataId $board
Write-Host "board $boardId"

$wsQuery = @{ workspaceId = $workspaceId }
function New-Count($key, $title, $icon, $color, $stateId) {
    return @{
        key = $key; type = "summaryCard"; title = $title; icon = $icon; accentColor = $color
        dataset = "op_work_items"; queryKey = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateId }); take = 500
    }
}
function New-List($key, $title, $stateId) {
    return @{
        key = $key; type = "list"; title = $title
        dataset = "op_work_items"; queryKey = "wi_by_workspace_and_state"
        parameters = ($wsQuery + @{ stateId = $stateId }); take = 8
    }
}

$widgets = @(
    (New-Count "count_kapandi" "Kapandı" "mdi-check-circle" "success" $states.kapandi),
    (New-Count "count_sevk" "Sevk edildi" "mdi-truck-check" "success" $states.sevk),
    (New-Count "count_kalite" "Kalite" "mdi-clipboard-check" "secondary" $states.kalite),
    (New-Count "count_uretim" "Üretim" "mdi-factory" "warning" $states.uretim),
    (New-Count "count_montaj" "Montaj" "mdi-wrench" "primary" $states.montaj),
    (New-Count "count_baslamadi" "Başlamadı" "mdi-pause-circle" "secondary" $states.baslamadi),
    (New-Count "count_boya" "Boya" "mdi-format-paint" "info" $states.boya),
    (New-Count "count_talasli" "Talaşlı" "mdi-cog" "warning" $states.talasli),
    (New-List "list_kalite" "Kalitede bekleyenler" $states.kalite),
    (New-List "list_uretim" "Üretimde bekleyenler" $states.uretim),
    (New-List "list_montaj" "Montajda bekleyenler" $states.montaj),
    (New-List "list_baslamadi" "Henüz başlamayanlar" $states.baslamadi)
)
$layout = @{
    type = "rows"
    rows = @(
        @{ cols = @(
            @{ widgetId = "count_kapandi"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_sevk"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_kalite"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_uretim"; span = 12; spanMd = 6; spanLg = 3 }
        ) },
        @{ cols = @(
            @{ widgetId = "count_montaj"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_baslamadi"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_boya"; span = 12; spanMd = 6; spanLg = 3 },
            @{ widgetId = "count_talasli"; span = 12; spanMd = 6; spanLg = 3 }
        ) },
        @{ cols = @(
            @{ widgetId = "list_kalite"; span = 12; spanMd = 6 },
            @{ widgetId = "list_uretim"; span = 12; spanMd = 6 }
        ) },
        @{ cols = @(
            @{ widgetId = "list_montaj"; span = 12; spanMd = 6 },
            @{ widgetId = "list_baslamadi"; span = 12; spanMd = 6 }
        ) }
    )
}
$name = "Üretim Takip - Güncel aşama"
$body = @{
    name = $name
    description = "İş emirlerinin şu an hangi aşamada olduğu. Kaynak: üretim takip raporu."
    workspaceId = $workspaceId
    scope = "workspace"
    isDefault = $true
    isActive = $true
    layout = $layout
    widgets = $widgets
}
$existingUri = "$BaseUrl$dataPath/op_dashboards?limit=5&expand=false&filter=" + [Uri]::EscapeDataString("name:eq:$name")
$existing = @(Get-Items (Invoke-Dg GET $existingUri))
if ($existing.Count -gt 0) {
    $dashboardId = Get-DataId $existing[0]
    Invoke-Dg PUT "$BaseUrl$dataPath/op_dashboards/$dashboardId" $body | Out-Null
    Write-Host "SYNC dashboard $dashboardId"
}
else {
    $created = Invoke-Dg POST "$BaseUrl$dataPath/op_dashboards" $body
    $dashboardId = Get-DataId $created
    Write-Host "OK dashboard $dashboardId"
}

Invoke-Dg PUT "$BaseUrl$dataPath/op_boards/$boardId" @{ defaultDashboardId = $dashboardId } | Out-Null
Write-Host "board panoya baglandi"

try {
    Invoke-Dg POST "$BaseUrl/operations/api/v1/workspaces/$workspaceId/metadata-cache/reload" @{} | Out-Null
    Write-Host "cache yenilendi"
}
catch {
    Write-Host "cache uyarisi: $($_.Exception.Message)"
}

$runtime = Invoke-Dg GET "$BaseUrl/operations/api/v1/runtime/dashboards/$dashboardId"
$widgetList = $runtime.widgets
if (-not $widgetList -and $runtime.data) { $widgetList = $runtime.data.widgets }
foreach ($w in @($widgetList)) {
    $total = $w.execution.total
    Write-Host ("  {0} = {1} ok={2}" -f $w.title, $total, $w.execution.success)
}
Write-Host "BITTI dashboard=$dashboardId board=$boardId"
