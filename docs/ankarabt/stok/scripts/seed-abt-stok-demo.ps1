# Abt demo: tanimlar zaten kurulu olmali. Stok yalnizca kapali depo girisinden gelir.
# Ayni baslik varsa is yeniden acilmaz; yarim kalmissa gecis kaldigi yerden surer.

param(
    [string]$BaseUrl = "http://192.168.1.249:5040",
    [string]$MoBaseUrl = "",
    [string]$MasterIdsFile = "",
    [string]$OcIdsFile = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $repoRoot "docs/odak/zimmet/scripts/lib/ZimmetDgCommon.ps1")

if ([string]::IsNullOrEmpty($MoBaseUrl)) { $MoBaseUrl = "$BaseUrl/operations" }
if ([string]::IsNullOrEmpty($MasterIdsFile)) { $MasterIdsFile = Join-Path $repoRoot "docs/ankarabt/stok/seed/abt_master_ids.json" }
if ([string]::IsNullOrEmpty($OcIdsFile)) { $OcIdsFile = Join-Path $repoRoot "docs/ankarabt/stok/seed/abt_oc_ids.json" }
if ([string]::IsNullOrEmpty($OutputFile)) { $OutputFile = Join-Path $repoRoot "docs/ankarabt/stok/seed/abt_demo.json" }

$ctx = Initialize-ZimmetDgSession -BaseUrl $BaseUrl -UseGateway -RepoRoot $repoRoot
$master = Get-Content $MasterIdsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$oc = Get-Content $OcIdsFile -Raw -Encoding UTF8 | ConvertFrom-Json

function Get-RowId {
    param($Item)
    if (-not $Item) { return $null }
    foreach ($name in @("__dataId", "dataId", "DataId", "id")) {
        $value = $Item.$name
        if ($value) { return [string]$value }
    }
    return $null
}

function Get-Rows {
    param([string]$Collection, [string]$Filter, [int]$Limit = 20)
    $filter = [uri]::EscapeDataString($Filter)
    $uri = "$($ctx.BaseUrl)$($ctx.DataPath)/$Collection`?filter=$filter&limit=$Limit"
    return @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $ctx -Method GET -Uri $uri))
}

function Get-OneRow {
    param([string]$Collection, [string]$Filter)
    return @(Get-Rows -Collection $Collection -Filter $Filter -Limit 5) | Select-Object -First 1
}

function Invoke-MoPost {
    param([string]$Uri, [object]$Body)
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json; charset=utf-8" -Headers $ctx.Headers -TimeoutSec 120
}

function Get-UserName {
    param($User)
    foreach ($name in @("username", "userName", "email")) {
        if ($User.$name) { return [string]$User.$name }
    }
    return ""
}

function Get-UserId {
    param($User)
    if ($User.userId) { return [string]$User.userId }
    if ($User.id) { return [string]$User.id }
    return ""
}

function Get-StateName {
    param($WorkItem)
    if ($WorkItem.stateName) { return [string]$WorkItem.stateName }
    if ($WorkItem.stateId -is [pscustomobject] -and $WorkItem.stateId.name) { return [string]$WorkItem.stateId.name }
    if ($WorkItem.state -and $WorkItem.state.name) { return [string]$WorkItem.state.name }
    $stateId = $WorkItem.stateId
    if ($stateId -is [pscustomobject]) { $stateId = Get-RowId $stateId }
    if (-not $stateId -and $WorkItem.state) { $stateId = $WorkItem.state.id }
    if (-not $stateId) { return "" }
    $state = Invoke-ZimmetDg -Ctx $ctx -Method GET -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_states/$stateId"
    $row = $state.data
    if (-not $row) { $row = $state }
    if ($row.name) { return [string]$row.name }
    return ""
}

function Get-WorkItemByTitle {
    param([string]$Title)
    return Get-OneRow -Collection "op_work_items" -Filter "title:eq:$Title"
}

$prio = Get-OneRow -Collection "op_priorities" -Filter "name:eq:ZIM - Normal"
$prioId = Get-RowId $prio
if (-not $prioId) { throw "Oncelik bulunamadi: ZIM - Normal" }

$today = (Get-Date).ToUniversalTime().ToString("o")
$soon = (Get-Date).ToUniversalTime().AddDays(21).ToString("o")
$far = (Get-Date).ToUniversalTime().AddMonths(36).ToString("o")
$year = (Get-Date).ToUniversalTime().AddMonths(12).ToString("o")

$supplierId = $null
try {
    $supplier = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $ctx -Method GET -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/tedarikciler?limit=1")) | Select-Object -First 1
    $supplierId = Get-RowId $supplier
    if ($supplierId) { Write-Host "Tedarikci baglandi: $supplierId" -ForegroundColor Gray }
    else { Write-Host "Tedarikci listesi bos; giris tedarikcisiz." -ForegroundColor DarkYellow }
}
catch {
    Write-Host "Tedarikci okunamadi; giris tedarikcisiz. $($_.Exception.Message)" -ForegroundColor DarkYellow
}

$savedNames = @()
if (Test-Path $OutputFile) {
    $previous = Get-Content $OutputFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($previous.persons) { $savedNames = @($previous.persons | ForEach-Object { [string]$_.username }) }
}

$users = @(Get-KeeperActiveUsers -Ctx $ctx -PageSize 100)
if ($users.Count -lt 2) { throw "Abt alaninda en az iki aktif kullanici gerekli." }

function Resolve-Person {
    param([string]$Username)
    return @($users | Where-Object { (Get-UserName $_) -eq $Username } | Select-Object -First 1)
}

$person1 = $null
$person2 = $null
if ($savedNames.Count -ge 2) {
    $person1 = Resolve-Person $savedNames[0]
    $person2 = Resolve-Person $savedNames[1]
}
if (-not $person1 -or -not $person2) {
    $ranked = @($users | Sort-Object { Get-UserName $_ })
    $preferred = @($ranked | Where-Object { (Get-UserName $_) -ne "abt_admin" })
    $pick = if ($preferred.Count -ge 2) { @($preferred | Select-Object -First 2) } else { @($ranked | Select-Object -First 2) }
    $person1 = $pick[0]
    $person2 = $pick[1]
}

$p1Name = Get-UserName $person1
$p2Name = Get-UserName $person2
$p1Id = Get-UserId $person1
$p2Id = Get-UserId $person2
if (-not $p1Id -or -not $p2Id) { throw "Personel kimligi okunamadi." }
Write-Host "Personel 1: $p1Name" -ForegroundColor Gray
Write-Host "Personel 2: $p2Name" -ForegroundColor Gray

function Invoke-Chain {
    param([string]$WorkItemId, [string[]]$Keys, [hashtable]$DeliverFields, [hashtable]$ReturnFields)
    foreach ($key in $Keys) {
        $body = "{}"
        if ($key -eq "deliver" -and $DeliverFields) {
            $body = @{ fields = $DeliverFields }
        }
        elseif ($key -eq "receive_return" -and $ReturnFields) {
            $body = @{ fields = $ReturnFields }
        }
        Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$WorkItemId/transitions/$key" -Body $body | Out-Null
        Write-Host "    gecis $key" -ForegroundColor DarkGray
    }
}

function Ensure-ClosedReceipt {
    param(
        [string]$Title,
        [string]$ProductId,
        [int]$Quantity,
        [string]$LocationId,
        [string]$InvoiceNo
    )
    $existing = Get-WorkItemByTitle $Title
    $workItemId = $null
    $workKey = $null
    if ($existing) {
        $workItemId = Get-RowId $existing
        $workKey = [string]$existing.key
        $stateName = Get-StateName $existing
        Write-Host "Mevcut giris: $workKey ($stateName)" -ForegroundColor Yellow
        $remain = @()
        if ($stateName -like "*Taslak*") { $remain = @("submit", "receive", "stock") }
        elseif ($stateName -like "*Mal kabul*") { $remain = @("receive", "stock") }
        elseif ($stateName -like "*Stoklandi*") { $remain = @("stock") }
        elseif ($stateName -like "*Kapali*") { $remain = @() }
        else { throw "Depo girisi durumu taninmadi: $Title / $stateName" }
        if ($remain.Count -gt 0) { Invoke-Chain -WorkItemId $workItemId -Keys $remain }
    }
    else {
        $fields = @{
            priorityId    = $prioId
            katalogUrunId = $ProductId
            miktar        = $Quantity
            depoId        = $master.ids.depolar.'DEP-ANA'
            lokasyonId    = $LocationId
            faturaNo      = $InvoiceNo
            girisTarihi   = $today
            kaynak        = "manuel"
        }
        if ($supplierId) { $fields.tedarikciId = $supplierId }
        $created = Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items" -Body @{
            workspaceId = $oc.workspaces.gir.id
            typeId      = $oc.types.depoGirisi
            boardId     = $oc.boards.girQueue
            title       = $Title
            fields      = $fields
        }
        $workItemId = [string]$created.workItem.id
        $workKey = [string]$created.workItem.key
        Write-Host "Giris acildi: $workKey" -ForegroundColor Green
        Invoke-Chain -WorkItemId $workItemId -Keys @("submit", "receive", "stock")
    }

    $rows = @(Get-Rows -Collection "zimmet_demirbaslar" -Filter "girisRef:eq:$workKey" -Limit 20 | Sort-Object { [string]$_.seriNo })
    if ($rows.Count -lt $Quantity) {
        throw "$workKey kapandi ama demirbas sayisi $($rows.Count); beklenen $Quantity"
    }
    return @{ key = $workKey; id = $workItemId; rows = $rows }
}

function Set-WarrantyIfEmpty {
    param($Row, [string]$When)
    if ($Row.garantiBitis) { return }
    $id = Get-RowId $Row
    Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_demirbaslar/$id" -Body @{ garantiBitis = $When } | Out-Null
}

$depoId = $master.ids.depolar.'DEP-ANA'
$lokLaptop = $master.ids.lokasyonlar.'DEP-ANA/A-01'
$lokMonitor = $master.ids.lokasyonlar.'DEP-ANA/A-02'
$lokBag = $master.ids.lokasyonlar.'DEP-ANA/B-01'

Write-Host "`n[Demo] Depo girisleri" -ForegroundColor Cyan
$girLaptop = Ensure-ClosedReceipt -Title "ABT Demo GIR laptop" -ProductId $master.ids.urunler.'PRD-DELL-5520' -Quantity 2 -LocationId $lokLaptop -InvoiceNo "FTR-ABT-LAP-001"
$girMonitor = Ensure-ClosedReceipt -Title "ABT Demo GIR monitor" -ProductId $master.ids.urunler.'PRD-DELL-U2722D' -Quantity 1 -LocationId $lokMonitor -InvoiceNo "FTR-ABT-MON-001"
$girBag = Ensure-ClosedReceipt -Title "ABT Demo GIR canta" -ProductId $master.ids.urunler.'PRD-LENOVO-BAG' -Quantity 1 -LocationId $lokBag -InvoiceNo "FTR-ABT-BAG-001"

$laptopAssigned = $girLaptop.rows[0]
$laptopShelf = $girLaptop.rows[1]
$monitor = $girMonitor.rows[0]
$bag = $girBag.rows[0]
Set-WarrantyIfEmpty $laptopAssigned $far
Set-WarrantyIfEmpty $laptopShelf $soon
Set-WarrantyIfEmpty $monitor $far
Set-WarrantyIfEmpty $bag $year
Write-Host "Garanti: raftaki laptop 21 gun, digerleri ileri tarih." -ForegroundColor Gray

function Ensure-Assignment {
    param(
        [string]$Title,
        [string]$AssetId,
        [string]$PersonId,
        [string]$PersonName,
        [string]$Department,
        [switch]$LeaveActive
    )
    $deliverFields = @{ teslimTarihi = $today; teslimDurumu = "iyi" }
    $existing = Get-WorkItemByTitle $Title
    if ($existing) {
        $id = Get-RowId $existing
        $key = [string]$existing.key
        $stateName = Get-StateName $existing
        Write-Host "Mevcut zimmet: $key ($stateName)" -ForegroundColor Yellow
        $remain = @()
        if ($stateName -like "*Talep*") { $remain = @("request", "approve", "deliver") }
        elseif ($stateName -like "*Onay*") { $remain = @("approve", "deliver") }
        elseif ($stateName -like "*Teslim edildi*") { $remain = @("deliver") }
        elseif ($stateName -like "*Aktif*") { $remain = @() }
        elseif ($stateName -like "*Kapali*") { $remain = @() }
        else { throw "Zimmet durumu taninmadi: $Title / $stateName" }
        if (-not $LeaveActive -and $stateName -like "*Aktif*") { $remain = @("close") }
        if ($remain.Count -gt 0) { Invoke-Chain -WorkItemId $id -Keys $remain -DeliverFields $deliverFields }
        return @{ id = $id; key = $key }
    }

    $created = Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items" -Body @{
        workspaceId = $oc.workspaces.zim.id
        typeId      = $oc.types.zimmetVerme
        boardId     = $oc.boards.zimQueue
        title       = $Title
        fields      = @{
            priorityId   = $prioId
            demirbasIds  = @($AssetId)
            personelId   = $PersonId
            departman    = $Department
            teslimDurumu = "yeni"
            zimmetNotu   = "Abt demo — $PersonName"
        }
    }
    $id = [string]$created.workItem.id
    $key = [string]$created.workItem.key
    Write-Host "Zimmet acildi: $key" -ForegroundColor Green
    $keys = @("request", "approve", "deliver")
    if (-not $LeaveActive) { $keys += "close" }
    Invoke-Chain -WorkItemId $id -Keys $keys -DeliverFields $deliverFields
    return @{ id = $id; key = $key }
}

function Ensure-Return {
    param([string]$Title, [string]$AssetId, [string]$PersonId, [hashtable]$ReturnFields)
    $existing = Get-WorkItemByTitle $Title
    if ($existing) {
        $id = Get-RowId $existing
        $key = [string]$existing.key
        $stateName = Get-StateName $existing
        Write-Host "Mevcut iade: $key ($stateName)" -ForegroundColor Yellow
        $remain = @()
        if ($stateName -like "*Iade acik*") { $remain = @("receive_return", "close_return") }
        elseif ($stateName -like "*Iade tamam*") { $remain = @("close_return") }
        elseif ($stateName -like "*Kapali*") { $remain = @() }
        else { throw "Iade durumu taninmadi: $Title / $stateName" }
        if ($remain.Count -gt 0) { Invoke-Chain -WorkItemId $id -Keys $remain -ReturnFields $ReturnFields }
        return @{ id = $id; key = $key }
    }

    $created = Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items" -Body @{
        workspaceId = $oc.workspaces.zim.id
        typeId      = $oc.types.zimmetIade
        boardId     = $oc.boards.zimIade
        title       = $Title
        fields      = @{
            priorityId  = $prioId
            demirbasId  = @($AssetId)
            personelId  = $PersonId
            depoId      = $ReturnFields.depoId
            lokasyonId  = $ReturnFields.lokasyonId
            iadeDurumu  = "tam"
        }
    }
    $id = [string]$created.workItem.id
    $key = [string]$created.workItem.key
    Write-Host "Iade acildi: $key" -ForegroundColor Green
    Invoke-Chain -WorkItemId $id -Keys @("receive_return", "close_return") -ReturnFields $ReturnFields
    return @{ id = $id; key = $key }
}

function Repair-AbtAssets {
    $uri = "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_demirbaslar?limit=200&expand=false"
    $rows = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $ctx -Method GET -Uri $uri))
    foreach ($row in $rows) {
        $id = Get-RowId $row
        if (-not $id) { continue }
        $patch = @{}
        if ([string]$row.durum -eq "zimmetli") {
            if ($row.depoId) { $patch.depoId = $null }
            if ($row.lokasyonId) { $patch.lokasyonId = $null }
        }
        $productId = $row.katalogUrunId
        if ($productId -is [pscustomobject]) { $productId = Get-RowId $productId }
        $needMarka = [string]::IsNullOrWhiteSpace([string]$row.marka)
        $needGroup = -not $row.urunGrubuId
        if ($productId -and ($needMarka -or $needGroup)) {
            $product = Invoke-ZimmetDg -Ctx $ctx -Method GET -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_urunler/$productId`?expand=false"
            $p = $product.data
            if (-not $p) { $p = $product }
            if ($needMarka -and $p.marka) { $patch.marka = [string]$p.marka }
            if ($needGroup -and $p.urunGrubuId) {
                $gid = $p.urunGrubuId
                if ($gid -is [pscustomobject]) { $gid = Get-RowId $gid }
                if ($gid) { $patch.urunGrubuId = [string]$gid }
            }
        }
        if ($patch.Count -eq 0) { continue }
        Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_demirbaslar/$id" -Body $patch | Out-Null
        $no = [string]$row.demirbasNo
        Write-Host "Demirbas guncellendi: $no" -ForegroundColor DarkGray
    }
}

Write-Host "`n[Demo] Zimmet" -ForegroundColor Cyan
$zimLaptop = Ensure-Assignment -Title "ABT Demo ZIM laptop" -AssetId (Get-RowId $laptopAssigned) -PersonId $p1Id -PersonName $p1Name -Department "Bilgi Islem"
$zimMonitor = Ensure-Assignment -Title "ABT Demo ZIM monitor" -AssetId (Get-RowId $monitor) -PersonId $p2Id -PersonName $p2Name -Department "Operasyon" -LeaveActive
$zimBag = Ensure-Assignment -Title "ABT Demo ZIM canta" -AssetId (Get-RowId $bag) -PersonId $p1Id -PersonName $p1Name -Department "Bilgi Islem"
$bagReturnFields = @{ depoId = $depoId; lokasyonId = $lokBag }
$zimBagReturn = Ensure-Return -Title "ABT Demo ZIM canta iade" -AssetId (Get-RowId $bag) -PersonId $p1Id -ReturnFields $bagReturnFields
Repair-AbtAssets

$summary = [ordered]@{
    seededAt = (Get-Date).ToUniversalTime().ToString("o")
    gateway  = $BaseUrl
    persons  = @(
        @{ username = $p1Name; id = $p1Id },
        @{ username = $p2Name; id = $p2Id }
    )
    receipts = @{
        laptop  = $girLaptop.key
        monitor = $girMonitor.key
        bag     = $girBag.key
    }
    assignments = @{
        laptopClosed = $zimLaptop.key
        monitorActive = $zimMonitor.key
        bagGiven = $zimBag.key
        bagReturned = $zimBagReturn.key
    }
    warrantySoonAsset = (Get-RowId $laptopShelf)
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "`nDemo ozeti: $OutputFile" -ForegroundColor Cyan
Write-Host "Tamamlandi." -ForegroundColor Green
