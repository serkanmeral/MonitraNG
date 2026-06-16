# NCR full lifecycle: contain -> investigate -> decide -> close_ncr
# Mirrors browser profile transition flow for Odak Uretim workspace.
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [string]$WorkspaceId = "9f9cc085-81c7-4a92-9fa2-357ad5c654cd",
    [string]$BoardNcrId = "fbc470c2-01a4-4992-b45a-bd1d099f59ab",
    [string]$TypeNcrId = "a8c6bd1f-4783-423a-899f-d552548889e3",
    [string]$StateNcrOpen = "05c4af17-26f6-494c-b6a4-6abe442e1552",
    [string]$StateNcrContain = "77ffde6b-9483-4fe2-b388-6770b2f84746",
    [string]$StateNcrReview = "1a20a6c5-3f5e-4e23-bede-70e46f3d8377",
    [string]$StateNcrDecided = "c0a6896a-0465-4905-b4b2-454a31cfa7a4",
    [string]$StateNcrClosed = "33b7bce0-a2d0-4a51-9235-241232b2f9d2",
    [string]$WorkItemId,
    [switch]$CreateViaAutomation,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

function Get-DgItems {
    param([string]$Filter, [int]$Limit = 20)
    $encoded = [Uri]::EscapeDataString($Filter)
    $uri = "$BaseUrl/data/api/v1/data/op_work_items?filter=$encoded&limit=$Limit"
    $resp = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET
    if ($resp -is [System.Array]) { return @($resp) }
    $items = @($resp.items)
    if (-not $items.Count -and $resp.data) { $items = @($resp.data) }
    if (-not $items.Count -and $resp.__dataId) { $items = @($resp) }
    return $items
}

function Get-StateId {
    param($Record)
    if ($null -eq $Record) { return $null }
    if ($Record.stateId -is [string]) { return $Record.stateId }
    if ($Record.stateId.__dataId) { return $Record.stateId.__dataId }
    return [string]$Record.stateId
}

function Invoke-Transition {
    param(
        [string]$Id,
        [string]$Key,
        [hashtable]$Body = @{}
    )
    $json = if ($Body.Count -gt 0) { $Body | ConvertTo-Json -Depth 8 -Compress } else { "{}" }
    Write-Host "  -> $Key" -ForegroundColor Cyan
    if ($VerboseOutput) { Write-Host "     body: $json" -ForegroundColor DarkGray }
    $result = Invoke-RestMethod `
        -Uri "$MoBaseUrl/api/v1/work-items/$Id/transitions/$Key" `
        -Method POST -Body $json -Headers $headers
    return $result
}

function Get-WorkItemDetail {
    param([string]$Id)
    $view = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/runtime/work-items/$Id/profile-view" -Headers $headers -Method GET
    return @{ workItem = $view.profile.workItem; fields = $view.fields; actions = $view.profile.actions }
}

function New-NcrViaHoldQuality {
    param([string]$SeedPath)

    $seed = Get-Content $SeedPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $masterPath = Join-Path (Split-Path $SeedPath) "odak_master_ids.json"
    $productId = $null
    $customerId = $null
    $productGroupId = $null
    if (Test-Path $masterPath) {
        $mids = Get-Content $masterPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($mids.urunler.'ODK-CMP-1001') { $productId = $mids.urunler.'ODK-CMP-1001' }
        if ($mids.musteriler.'MUS-001') { $customerId = $mids.musteriler.'MUS-001' }
        if ($mids.urunGruplari.'UG-KOM') { $productGroupId = $mids.urunGruplari.'UG-KOM' }
    }

    $suffix = Get-Date -Format "HHmmss"
    $createBody = @{
        workspaceId = $seed.workspaceId
        typeId      = $seed.types.order
        title       = "Lifecycle test emir $suffix"
        boardId     = $seed.boardProdId
        fields      = @{
            priorityId       = $seed.priorities.normal
            orderType        = "seri"
            customerId       = $customerId
            customerOrderRef = "PO-LC-$suffix"
        }
    } | ConvertTo-Json -Depth 8 -Compress

    $order = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/work-items" -Method POST -Body $createBody -Headers $headers
    $orderId = $order.workItem.id
    Write-Host "  ODF olusturuldu: $($order.workItem.key)" -ForegroundColor Green

    $planFields = @{
        productGroupId = $productGroupId
        productId      = $productId
        quantity       = 4
        plannedDate    = (Get-Date).AddDays(7).ToUniversalTime().ToString("o")
        orderType      = "seri"
    }
    Invoke-Transition -Id $orderId -Key "plan" -Body @{ fields = $planFields } | Out-Null
    Invoke-Transition -Id $orderId -Key "start_production" -Body (@{ fields = @{ workCenter = "Test hatti" } }) | Out-Null
    Invoke-Transition -Id $orderId -Key "send_to_quality" -Body (@{ fields = @{ lotSerial = "LOT-LC-$suffix"; rejectedQty = 2 } }) | Out-Null
    Write-Host "  ODF kalite kontrolde" -ForegroundColor Green

    Invoke-Transition -Id $orderId -Key "hold_quality" -Body @{
        fields = @{
            qualityResult = "uygunsuz"
            qualityNotes  = "Lifecycle test uygunsuzluk"
            rejectedQty   = 2
        }
    } | Out-Null
    Start-Sleep -Seconds 2

    $parentFilter = [Uri]::EscapeDataString("parentItemId:eq:$orderId")
    $childUri = "$BaseUrl/data/api/v1/data/op_work_items?filter=$parentFilter" + "&limit=5"
    $childItems = Get-DgItems -Filter "parentItemId:eq:$orderId" -Limit 5
    if (-not $childItems.Count) { throw "hold_quality sonrasi NCR olusmadi (parent=$orderId)" }

    $ncr = $childItems | Where-Object {
        $tid = if ($_.typeId -is [string]) { $_.typeId } else { $_.typeId.__dataId }
        $tid -eq $seed.types.ncr
    } | Select-Object -First 1
    if (-not $ncr) { $ncr = $childItems[0] }
    $ncrIdResolved = if ($ncr.__dataId) { $ncr.__dataId } else { $ncr.id }
    Write-Host "  NCR otomasyon: $($ncr.key) ($ncrIdResolved) assignee=$($ncr.assignee)" -ForegroundColor Green
    return @{ Id = $ncrIdResolved; Key = $ncr.key; OrderKey = $order.workItem.key }
}

Write-Host "`n=== NCR Lifecycle Test ===" -ForegroundColor Magenta
Write-Host "MO: $MoBaseUrl | WS: $WorkspaceId`n"

try {
    $live = Invoke-RestMethod -Uri "$MoBaseUrl/api/v1/health/live" -TimeoutSec 8
    Write-Host "MO live: $($live.status)" -ForegroundColor Green
}
catch {
    Write-Host "MO health FAIL: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$ncrId = $WorkItemId
$ncrKey = $null

if (-not $ncrId) {
    $candidates = Get-DgItems -Filter "boardId:eq:$BoardNcrId" -Limit 30
    $openNcr = $candidates | Where-Object { (Get-StateId $_) -ne $StateNcrClosed } | Select-Object -First 1
    if ($openNcr) {
        $ncrId = if ($openNcr.__dataId) { $openNcr.__dataId } else { $openNcr.id }
        $ncrKey = $openNcr.key
        Write-Host "Mevcut acik NCR: $ncrKey ($ncrId) state=$(Get-StateId $openNcr)" -ForegroundColor Yellow
    }
}

if (-not $ncrId -and $CreateViaAutomation) {
    Write-Host "Acik NCR yok - hold_quality otomasyonu ile olusturuluyor..." -ForegroundColor Yellow
    $seedPath = Join-Path $repoRoot "docs/odak/is_surecleri/seed/odak-uretim-seed.json"
    $created = New-NcrViaHoldQuality -SeedPath $seedPath
    $ncrId = $created.Id
    $ncrKey = $created.Key
}

if (-not $ncrId) {
    Write-Host "Acik NCR yok. -WorkItemId veya -CreateViaAutomation kullanin." -ForegroundColor Red
    $all = Get-DgItems -Filter "boardId:eq:$BoardNcrId" -Limit 10
    Write-Host "NCR board kayitlari ($($all.Count)):" -ForegroundColor DarkYellow
    $all | ForEach-Object { Write-Host "  $($_.key) state=$($_.stateId)" }
    exit 2
}

$detail = Get-WorkItemDetail -Id $ncrId
$wi = $detail.workItem
$ncrKey = $wi.key
Write-Host "`nNCR: $ncrKey | state=$($wi.stateId) | assignee=$($wi.assignee)" -ForegroundColor White
Write-Host "title: $($wi.title)" -ForegroundColor DarkGray

$fields = @{}
if ($detail.fields) {
    foreach ($p in $detail.fields.PSObject.Properties) { $fields[$p.Name] = $p.Value }
}
Write-Host "fields: ncrSource=$($fields.ncrSource) affectedQty=$($fields.affectedQty) lotSerial=$($fields.lotSerial)" -ForegroundColor DarkGray

# Key format check (known gap: may be ODF-XXXX not NCR-XXXX)
if ($ncrKey -match '^ODF-') {
    Write-Host "WARN: NCR key workspace prefix (ODF) kullaniyor - beklenen NCR- prefix degil" -ForegroundColor DarkYellow
}
if ($wi.assignee -match '\{\{') {
    Write-Host "WARN: assignee token cozulmemis: $($wi.assignee)" -ForegroundColor DarkYellow
}

$steps = @()
$stateId = $wi.stateId
if ($stateId -isnot [string] -and $stateId.id) { $stateId = $stateId.id }

if ($stateId -eq $StateNcrOpen) {
    $steps += @{
        key = "contain"
        body = @{
            fields = @{
                containmentAction = "Etkilenen lot ayirtildi; uretim durduruldu (lifecycle test $(Get-Date -Format 'HHmmss'))"
            }
        }
    }
}

if ($steps.Count -gt 0) {
    foreach ($step in $steps) {
        Invoke-Transition -Id $ncrId -Key $step.key -Body $step.body | Out-Null
    }
    $detail = Get-WorkItemDetail -Id $ncrId
    $wi = $detail.workItem
    $stateId = $wi.stateId
    if ($stateId -isnot [string] -and $stateId.id) { $stateId = $stateId.id }
    Write-Host "  state after contain: $stateId" -ForegroundColor Green
}

if ($stateId -ne $StateNcrClosed) {
    if ($stateId -eq $StateNcrContain) {
        Invoke-Transition -Id $ncrId -Key "review" | Out-Null
        $detail = Get-WorkItemDetail -Id $ncrId
        $stateId = $detail.workItem.stateId
        if ($stateId -isnot [string] -and $stateId.id) { $stateId = $stateId.id }
        Write-Host "  state after review: $stateId" -ForegroundColor Green
    }

    if ($stateId -eq $StateNcrReview) {
        Invoke-Transition -Id $ncrId -Key "decide" -Body @{
            fields = @{ disposition = "scrap" }
        } | Out-Null
        $detail = Get-WorkItemDetail -Id $ncrId
        $stateId = $detail.workItem.stateId
        if ($stateId -isnot [string] -and $stateId.id) { $stateId = $stateId.id }
        Write-Host "  state after decide: $stateId disposition=$($detail.fields.disposition)" -ForegroundColor Green
    }

    if ($stateId -eq $StateNcrDecided) {
        Invoke-Transition -Id $ncrId -Key "close_ncr" -Body @{
            fields = @{
                disposition = "scrap"
                dispositionReason = "Hurda - delaminasyon, geri donusum mumkun degil (lifecycle test)"
            }
        } | Out-Null
        $detail = Get-WorkItemDetail -Id $ncrId
        $wi = $detail.workItem
        $stateId = $wi.stateId
        if ($stateId -isnot [string] -and $stateId.id) { $stateId = $stateId.id }
        Write-Host "  state after close: $stateId" -ForegroundColor Green
    }
}

if ($stateId -eq $StateNcrClosed -or $wi.stateId -eq $StateNcrClosed) {
    Write-Host "`nPASS: NCR lifecycle tamamlandi - $ncrKey kapandi" -ForegroundColor Green
    Write-Host "  disposition=$($detail.fields.disposition) reason=$($detail.fields.dispositionReason)" -ForegroundColor DarkGray
    exit 0
}

Write-Host "`nFAIL: NCR kapanmadi - state=$($wi.stateId)" -ForegroundColor Red
exit 1
