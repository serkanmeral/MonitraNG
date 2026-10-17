# Üretim Takip workspace — production seed from PROJE_PLANLAMA_REV16.xlsx
# Usage (repo root):
#   .\docs\odak\uretim\scripts\seed-uretim-takip-prod.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$ExcelPath = ""
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "UTF-8 icin bu script pwsh 7 ile calistirilmali."
}
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($ExcelPath)) {
    $ExcelPath = Join-Path $repoRoot "docs/odak/uretim/PROJE_PLANLAMA_REV16.xlsx"
}
if (-not (Test-Path $ExcelPath)) { throw "Excel bulunamadi: $ExcelPath" }

$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"
$env:MNG_OC_USE_PROD_TOKEN = "1"
$token = & (Join-Path $ocScripts "load-operationcore-token.ps1") -AutoRefresh
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi." }

$dataPath = "/data/api/v1/data"
$workspaceName = "Üretim Takip"
$tag = "Üretim Takip"
$headers = @{ Authorization = "Bearer $($token.Trim())" }
$irm = @{ Headers = $headers; ErrorAction = "Stop" }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method } + $irm
    if ($null -ne $Body) {
        $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 30 -Compress }
        $p.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $p.ContentType = "application/json; charset=utf-8"
    }
    return Invoke-RestMethod @p
}

function Invoke-DgGet([string]$Collection, [string]$Filter = "", [int]$Limit = 20) {
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if ($Filter) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    return Invoke-Dg GET $uri
}

function Get-Items($Response) {
    if (-not $Response) { return @() }
    if ($Response -is [System.Array]) { return @($Response) }
    foreach ($prop in @("items", "data", "Items", "Data")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [System.Array]) { return @($items) }
            return @($items)
        }
    }
    return @($Response)
}

function Get-DataId($Response) {
    if (-not $Response) { return $null }
    $d = $Response
    if ($Response.data) { $d = $Response.data }
    elseif ($Response.Data) { $d = $Response.Data }
    $id = $d.__dataId
    if (-not $id) { $id = $d.dataId }
    return [string]$id
}

function Find-OrCreate([string]$Collection, [string]$Filter, [object]$Body, [string]$Label) {
    $existing = @(Get-Items (Invoke-DgGet $Collection $Filter 5))
    if ($existing.Count -gt 0) {
        $id = [string]$existing[0].__dataId
        if (-not $id) { $id = [string]$existing[0].dataId }
        Write-Host "  SKIP $Label ($id)"
        return $id
    }
    $created = Invoke-Dg POST "$BaseUrl$dataPath/$Collection" $Body
    $id = Get-DataId $created
    Write-Host "  OK $Label -> $id"
    return $id
}

function Sync-Record([string]$Collection, [string]$Id, [object]$Body, [string]$Label) {
    Invoke-Dg PUT "$BaseUrl$dataPath/$Collection/$Id" $Body | Out-Null
    Write-Host "  SYNC $Label"
}

# --- Excel ---
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $ExcelPath))
function Read-Zip([string]$Name) {
    $entry = $zip.Entries | Where-Object { $_.FullName -eq $Name }
    $reader = New-Object System.IO.StreamReader($entry.Open())
    $text = $reader.ReadToEnd()
    $reader.Close()
    return $text
}
$shared = New-Object System.Collections.Generic.List[string]
foreach ($m in [regex]::Matches((Read-Zip "xl/sharedStrings.xml"), '<si>(.*?)</si>', 'Singleline')) {
    $parts = [regex]::Matches($m.Groups[1].Value, '<t[^>]*>(.*?)</t>', 'Singleline') | ForEach-Object { $_.Groups[1].Value }
    $shared.Add([System.Net.WebUtility]::HtmlDecode(($parts -join "")))
}
function Get-ColNum([string]$col) {
    $n = 0
    foreach ($ch in $col.ToCharArray()) { $n = $n * 26 + ([int][char]$ch - 64) }
    return $n
}
function Read-SheetRows([string]$SheetName) {
    $xml = Read-Zip $SheetName
    $rows = @()
    foreach ($rm in [regex]::Matches($xml, '<row r="(\d+)"[^>]*>(.*?)</row>', 'Singleline')) {
        $vals = @{}
        foreach ($cm in [regex]::Matches($rm.Groups[2].Value, '<c r="([A-Z]+)(\d+)"([^>]*?)(?:/>|>(.*?)</c>)', 'Singleline')) {
            $inner = $cm.Groups[4].Value
            if (-not $inner) { continue }
            $vm = [regex]::Match($inner, '<v>(.*?)</v>')
            if (-not $vm.Success) { continue }
            $v = [System.Net.WebUtility]::HtmlDecode($vm.Groups[1].Value)
            if ($cm.Groups[3].Value -match 't="s"') { $v = $shared[[int]$v] }
            if ($v -ne "") { $vals[(Get-ColNum $cm.Groups[1].Value)] = $v }
        }
        $rows += [pscustomobject]@{ Row = [int]$rm.Groups[1].Value; Cells = $vals }
    }
    return $rows
}
$epoch = [datetime]'1899-12-30'
function Convert-ExcelDate($value) {
    if ($null -eq $value -or "$value" -eq "") { return $null }
    $text = [string]$value
    if ($text -match '^\d+(\.\d+)?$' -and [double]$text -gt 30000 -and [double]$text -lt 80000) {
        return $epoch.AddDays([double]$text).ToString("yyyy-MM-dd") + "T12:00:00"
    }
    return $null
}
function Cell($row, [int]$col) {
    if ($row.Cells.ContainsKey($col)) { return [string]$row.Cells[$col] }
    return ""
}

$shopRows = @(Read-SheetRows "xl/worksheets/sheet2.xml" | Where-Object { $_.Row -ge 3 -and (Cell $_ 2) })
$projectRows = @(Read-SheetRows "xl/worksheets/sheet1.xml" | Where-Object { $_.Row -ge 3 -and (Cell $_ 2) -and (Cell $_ 2) -ne "Durum Renk Kodları:" })
$zip.Dispose()
Write-Host "Excel is emri=$($shopRows.Count) proje=$($projectRows.Count)"

# --- Groups ---
$resolve = Join-Path $ocScripts "resolve-odak-group-ids-prod.ps1"
$groupMap = & $resolve -Names @("users", "MonitraNG Users", "admins")
$usersGroupId = [string]$groupMap.users
$editGroupId = [string]$groupMap.'MonitraNG Users'
$adminGroupId = [string]$groupMap.admins
if (-not $usersGroupId -or -not $editGroupId -or -not $adminGroupId) { throw "Grup id alinamadi." }

# --- States ---
Write-Host "[1] Durumlar"
$shopDefs = @(
    @{ slug = "baslamadi"; title = "Başlamadı"; category = "open"; color = "secondary"; initial = $true; start = $true; order = 10 },
    @{ slug = "tasarim"; title = "Tasarım"; category = "in_progress"; color = "info"; order = 20 },
    @{ slug = "ply"; title = "Ply Cutter"; category = "in_progress"; color = "info"; order = 30 },
    @{ slug = "uretim"; title = "Üretim"; category = "in_progress"; color = "warning"; order = 40 },
    @{ slug = "talasli"; title = "Talaşlı"; category = "in_progress"; color = "warning"; order = 50 },
    @{ slug = "montaj"; title = "Montaj"; category = "in_progress"; color = "primary"; order = 60 },
    @{ slug = "boya"; title = "Boya"; category = "in_progress"; color = "primary"; order = 70 },
    @{ slug = "kalite"; title = "Kalite"; category = "in_progress"; color = "purple"; order = 80 },
    @{ slug = "yarimamul"; title = "Yarı Mamül"; category = "in_progress"; color = "info"; order = 90 },
    @{ slug = "km"; title = "KM"; category = "in_progress"; color = "info"; order = 100 },
    @{ slug = "rework"; title = "Rework"; category = "on_hold"; color = "error"; order = 110 },
    @{ slug = "kapandi"; title = "Kapandı"; category = "closed"; color = "success"; closed = $true; terminal = $true; order = 120 },
    @{ slug = "sevk"; title = "Sevk edildi"; category = "closed"; color = "success"; closed = $true; terminal = $true; order = 130 }
)
$projectDefs = @(
    @{ slug = "atanmadi"; title = "Atanmadı"; category = "open"; color = "secondary"; initial = $true; start = $true; order = 210 },
    @{ slug = "devam"; title = "Devam ediyor"; category = "in_progress"; color = "warning"; order = 220 },
    @{ slug = "beklemede"; title = "Beklemede"; category = "on_hold"; color = "info"; order = 230 },
    @{ slug = "tamamlandi"; title = "Tamamlandı"; category = "closed"; color = "success"; closed = $true; terminal = $true; order = 240 },
    @{ slug = "iptal"; title = "İptal"; category = "closed"; color = "secondary"; closed = $true; terminal = $true; order = 250 }
)
function Get-RowId($row) {
    $id = [string]$row.__dataId
    if (-not $id) { $id = [string]$row.dataId }
    return $id
}
function Repair-Name([string]$Collection, [string]$Id, [string]$Name) {
    if (-not $Id) { return }
    Invoke-Dg PUT "$BaseUrl$dataPath/$Collection/$Id" @{ name = $Name } | Out-Null
    Write-Host "  REPAIR $Name"
}
$existingStates = @(Get-Items (Invoke-DgGet "op_states" "" 500))
foreach ($def in @($shopDefs + $projectDefs)) {
    $correct = "$tag - $($def.title)"
    $hits = @($existingStates | Where-Object { $_.sortOrder -eq $def.order -and [string]$_.name -like "*Takip*" -and [string]$_.name -ne $correct })
    foreach ($hit in $hits) { Repair-Name "op_states" (Get-RowId $hit) $correct }
}
Repair-Name "op_work_item_types" "d67f6045-e372-4db5-93c5-5cf3b2e7974b" "Üretim iş emri"
Repair-Name "op_work_item_types" "797192c5-be31-431e-a08e-5300e12c6f18" "Proje işi"
$existingWorkspaces = @(Get-Items (Invoke-DgGet "op_workspaces" "" 50))
$wsHit = @($existingWorkspaces | Where-Object { [string]$_.key -eq "WS-0025" } | Select-Object -First 1)
if ($wsHit) {
    $repairWsId = Get-RowId $wsHit
    if ([string]$wsHit.name -ne $workspaceName) {
        Invoke-Dg PUT "$BaseUrl$dataPath/op_workspaces/$repairWsId" @{
            name = $workspaceName
            description = "Kompozit üretim iş emri ve proje işi takibi. Kaynak: proje planlama dosyası."
        } | Out-Null
        Write-Host "  REPAIR workspace"
    }
    $related = @(
        @{ collection = "op_state_flows"; like = "* - Hat"; name = "$tag - Hat" },
        @{ collection = "op_state_flows"; like = "* - Proje"; name = "$tag - Proje" },
        @{ collection = "op_forms"; like = "*emri"; name = "$tag - İş emri" },
        @{ collection = "op_forms"; like = "*Proje*"; name = "$tag - Proje işi" },
        @{ collection = "op_boards"; like = "*emirleri"; name = "$tag - İş emirleri" },
        @{ collection = "op_boards"; like = "*Proje*"; name = "$tag - Proje işleri" }
    )
    foreach ($rel in $related) {
        $rows = @(Get-Items (Invoke-DgGet $rel.collection "workspaceId:eq:$repairWsId" 20))
        foreach ($row in $rows) {
            $current = [string]$row.name
            if ($current -like $rel.like -and $current -ne $rel.name) {
                if ($rel.collection -eq "op_forms" -and $rel.like -eq "*emri" -and $current -like "*emirleri*") { continue }
                if ($rel.collection -eq "op_forms" -and $rel.like -eq "*Proje*" -and $current -like "*emri*") { continue }
                if ($rel.collection -eq "op_boards" -and $rel.like -eq "*Proje*" -and $current -like "*emirleri*") { continue }
                Repair-Name $rel.collection (Get-RowId $row) $rel.name
            }
        }
    }
}

$shopIds = @{}
foreach ($def in $shopDefs) {
    $name = "$tag - $($def.title)"
    $body = @{ name = $name; category = $def.category; color = $def.color; sortOrder = $def.order }
    if ($def.initial) { $body.isInitial = $true; $body.isStart = $true }
    if ($def.closed) { $body.isClosed = $true; $body.isTerminal = $true; $body.allowReopen = $true }
    $shopIds[$def.slug] = Find-OrCreate "op_states" "name:eq:$name" $body $name
}
$projectIds = @{}
foreach ($def in $projectDefs) {
    $name = "$tag - $($def.title)"
    $body = @{ name = $name; category = $def.category; color = $def.color; sortOrder = $def.order }
    if ($def.initial) { $body.isInitial = $true; $body.isStart = $true }
    if ($def.closed) { $body.isClosed = $true; $body.isTerminal = $true; $body.allowReopen = $true }
    $projectIds[$def.slug] = Find-OrCreate "op_states" "name:eq:$name" $body $name
}

Write-Host "[2] Oncelik"
$priorityId = Find-OrCreate "op_priorities" "name:eq:P3 - Orta" @{
    name = "P3 - Orta"; level = "3"; sortOrder = 30; color = "info"
} "P3"

function New-Select([array]$Items) {
    return @{ lookup = @{ source = "static"; presentation = "dropdown"; staticItems = $Items } }
}
$gateOptions = New-Select @(
    @{ value = "uretim"; label = "Üretim sonrası" },
    @{ value = "talasli"; label = "Talaşlı sonrası" },
    @{ value = "montaj"; label = "Montaj sonrası" },
    @{ value = "boya"; label = "Boya sonrası" }
)
$reworkOptions = New-Select @(
    @{ value = "Ölçü hatası"; label = "Ölçü hatası" },
    @{ value = "Yüzey hatası (çizik, ezik, pürüz)"; label = "Yüzey hatası (çizik, ezik, pürüz)" },
    @{ value = "Boya hatası"; label = "Boya hatası" },
    @{ value = "Delaminasyon / hava boşluğu"; label = "Delaminasyon / hava boşluğu" },
    @{ value = "Talaşlı işleme hatası"; label = "Talaşlı işleme hatası" },
    @{ value = "Montaj hatası"; label = "Montaj hatası" },
    @{ value = "Malzeme hatası"; label = "Malzeme hatası" },
    @{ value = "Müşteri iadesi"; label = "Müşteri iadesi" },
    @{ value = "Diğer"; label = "Diğer" }
)

Write-Host "[3] Alanlar"
$fieldDefs = @(
    @{ key = "uttWorkOrderNo"; label = "İş emri no"; fieldType = "text"; category = "classification" },
    @{ key = "uttPartCode"; label = "Parça kodu"; fieldType = "text"; category = "classification" },
    @{ key = "uttPartName"; label = "Parça / iş adı"; fieldType = "text"; category = "classification" },
    @{ key = "uttQuantity"; label = "Adet"; fieldType = "number"; category = "technical" },
    @{ key = "uttOwner"; label = "Sorumlu"; fieldType = "text"; category = "assignment" },
    @{ key = "uttOpenedOn"; label = "İş emri tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageDesign"; label = "Tasarım tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStagePly"; label = "Ply Cutter tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageProduction"; label = "Üretim tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageQaProduction"; label = "Kalite (üretim sonrası)"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageMachining"; label = "Talaşlı tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageQaMachining"; label = "Kalite (talaşlı sonrası)"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageAssembly"; label = "Montaj tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageQaAssembly"; label = "Kalite (montaj sonrası)"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStagePaint"; label = "Boya tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageQaPaint"; label = "Kalite (boya sonrası)"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageWip"; label = "Yarı mamül tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttStageKm"; label = "KM tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttShippedOn"; label = "Sevk tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttClosedOn"; label = "Kapanış tarihi"; fieldType = "datetime"; category = "technical" },
    @{ key = "uttQualityGate"; label = "Kalite kapısı"; fieldType = "select"; category = "classification"; options = $gateOptions },
    @{ key = "uttElapsedDays"; label = "Geçen süre (gün)"; fieldType = "number"; category = "technical" },
    @{ key = "uttReworkQty"; label = "Rework adedi"; fieldType = "number"; category = "resolution" },
    @{ key = "uttReworkReason"; label = "Rework nedeni"; fieldType = "select"; category = "resolution"; options = $reworkOptions },
    @{ key = "uttReworkNote"; label = "Rework açıklaması"; fieldType = "text"; category = "resolution" },
    @{ key = "uttReworkOpenedOn"; label = "Rework açılış"; fieldType = "datetime"; category = "resolution" },
    @{ key = "uttReworkClosedOn"; label = "Rework bitiş"; fieldType = "datetime"; category = "resolution" },
    @{ key = "uttNotes"; label = "Notlar"; fieldType = "text"; category = "technical" },
    @{ key = "utpOdakNo"; label = "Odak numarası"; fieldType = "text"; category = "classification" },
    @{ key = "utpCustomerNo"; label = "Müşteri numarası"; fieldType = "text"; category = "classification" },
    @{ key = "utpFolder"; label = "Klasör"; fieldType = "text"; category = "classification" },
    @{ key = "utpRefFolders"; label = "Referans klasörler"; fieldType = "text"; category = "classification" },
    @{ key = "utpOwner"; label = "Yapan kişi"; fieldType = "text"; category = "assignment" },
    @{ key = "utpDesignDue"; label = "Tasarım termini"; fieldType = "datetime"; category = "technical" },
    @{ key = "utpProjectDue"; label = "Proje termini"; fieldType = "datetime"; category = "technical" },
    @{ key = "utpNotes"; label = "Proje notu"; fieldType = "text"; category = "technical" }
)
$fieldIds = @{}
foreach ($field in $fieldDefs) {
    $body = @{
        key = $field.key; label = $field.label; fieldType = $field.fieldType
        scope = "pool"; category = $field.category
    }
    if ($field.options) { $body.options = $field.options }
    $fieldId = Find-OrCreate "op_fields" "key:eq:$($field.key)" $body $field.key
    Sync-Record "op_fields" $fieldId $body $field.key
    $fieldIds[$field.key] = $fieldId
}

Write-Host "[4] Is tipleri"
$typeShopId = Find-OrCreate "op_work_item_types" "name:eq:Üretim iş emri" @{
    name = "Üretim iş emri"; category = "operational"; color = "warning"; icon = "PackageIcon"; sortOrder = 10
} "Üretim iş emri"
$typeProjectId = Find-OrCreate "op_work_item_types" "name:eq:Proje işi" @{
    name = "Proje işi"; category = "task"; color = "info"; icon = "ClipboardIcon"; sortOrder = 20
} "Proje işi"

Write-Host "[5] Workspace"
$enabledStates = @($shopIds.Values + $projectIds.Values)
$enabledFields = @($fieldIds.Values)
$workspaceId = Find-OrCreate "op_workspaces" "name:eq:$workspaceName" @{
    name = $workspaceName
    workspaceType = "operational"
    description = "Kompozit üretim iş emri ve proje işi takibi. Kaynak: proje planlama dosyası."
    workItemKeyPrefix = "UT"
    workItemKeyFormat = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    viewGroups = @($usersGroupId)
    editGroups = @($editGroupId)
    adminGroups = @($adminGroupId)
    enabledTypeIds = @($typeShopId, $typeProjectId)
    enabledPriorityIds = @($priorityId)
    enabledFieldIds = $enabledFields
    enabledStateIds = $enabledStates
} "Workspace"
$ws = Invoke-Dg GET "$BaseUrl$dataPath/op_workspaces/$workspaceId"
$wsRow = if ($ws.data) { $ws.data } else { $ws }
$workspaceKey = [string]$wsRow.key
if (-not $workspaceKey) { $workspaceKey = "UT" }
Write-Host "  workspaceKey=$workspaceKey"

function New-MoveTransitions($idMap, $defs) {
    $list = @()
    $order = 0
    foreach ($from in $defs) {
        foreach ($to in $defs) {
            if ($from.slug -eq $to.slug) { continue }
            $order++
            $list += @{
                transitionKey = "$($from.slug)__$($to.slug)"
                fromStateId = $idMap[$from.slug]
                toStateId = $idMap[$to.slug]
                label = $to.title
                order = $order
            }
        }
    }
    return $list
}

Write-Host "[6] Akislar"
$shopFlowName = "$tag - Hat"
$shopFlowId = Find-OrCreate "op_state_flows" "name:eq:$shopFlowName" @{
    name = $shopFlowName; workspaceId = $workspaceId; initialStateId = $shopIds.baslamadi
    isDefault = $true; isActive = $true; transitions = @(New-MoveTransitions $shopIds $shopDefs)
} "Hat akisi"
Sync-Record "op_state_flows" $shopFlowId @{
    name = $shopFlowName; workspaceId = $workspaceId; initialStateId = $shopIds.baslamadi
    isDefault = $true; isActive = $true; transitions = @(New-MoveTransitions $shopIds $shopDefs)
} "Hat akisi"
$projectFlowName = "$tag - Proje"
$projectFlowId = Find-OrCreate "op_state_flows" "name:eq:$projectFlowName" @{
    name = $projectFlowName; workspaceId = $workspaceId; initialStateId = $projectIds.atanmadi
    isDefault = $false; isActive = $true; transitions = @(New-MoveTransitions $projectIds $projectDefs)
} "Proje akisi"
Sync-Record "op_state_flows" $projectFlowId @{
    name = $projectFlowName; workspaceId = $workspaceId; initialStateId = $projectIds.atanmadi
    isDefault = $false; isActive = $true; transitions = @(New-MoveTransitions $projectIds $projectDefs)
} "Proje akisi"
Sync-Record "op_work_item_types" $typeShopId @{
    name = "Üretim iş emri"; category = "operational"; color = "warning"; icon = "PackageIcon"
    sortOrder = 10; workspaceId = $workspaceId; defaultStateFlowId = $shopFlowId
} "tip is emri"
Sync-Record "op_work_item_types" $typeProjectId @{
    name = "Proje işi"; category = "task"; color = "info"; icon = "ClipboardIcon"
    sortOrder = 20; workspaceId = $workspaceId; defaultStateFlowId = $projectFlowId
} "tip proje"

Write-Host "[7] Formlar"
$shopFormName = "$tag - İş emri"
$shopForm = @{
    name = $shopFormName; workspaceId = $workspaceId; defaultTypeId = $typeShopId
    defaultStateFlowId = $shopFlowId; defaultStateId = $shopIds.baslamadi; isDefault = $true
    layout = @{
        sections = @(
            @{ key = "identity"; title = "İş emri"; fields = @("title", "uttWorkOrderNo", "uttPartCode", "uttPartName", "uttQuantity", "uttOwner", "uttOpenedOn", "priorityId") },
            @{ key = "stages"; title = "Aşamalar"; fields = @("uttStageDesign", "uttStagePly", "uttStageProduction", "uttStageQaProduction", "uttStageMachining", "uttStageQaMachining", "uttStageAssembly", "uttStageQaAssembly", "uttStagePaint", "uttStageQaPaint", "uttStageWip", "uttStageKm", "uttQualityGate", "uttShippedOn", "uttClosedOn", "uttElapsedDays") },
            @{ key = "rework"; title = "Rework"; fields = @("uttReworkQty", "uttReworkReason", "uttReworkNote", "uttReworkOpenedOn", "uttReworkClosedOn", "uttNotes") }
        )
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        uttWorkOrderNo = @{ visible = $true; required = $true }
        uttPartName = @{ visible = $true; required = $true }
    }
}
$shopFormId = Find-OrCreate "op_forms" "name:eq:$shopFormName" $shopForm "Form is emri"
Sync-Record "op_forms" $shopFormId $shopForm "Form is emri"
$projectFormName = "$tag - Proje işi"
$projectForm = @{
    name = $projectFormName; workspaceId = $workspaceId; defaultTypeId = $typeProjectId
    defaultStateFlowId = $projectFlowId; defaultStateId = $projectIds.atanmadi; isDefault = $false
    layout = @{
        sections = @(
            @{ key = "project"; title = "Proje işi"; fields = @("title", "utpOdakNo", "utpCustomerNo", "utpFolder", "utpRefFolders", "utpOwner", "utpDesignDue", "utpProjectDue", "utpNotes", "priorityId") }
        )
    }
    fieldBehaviors = @{ title = @{ visible = $true; required = $true } }
}
$projectFormId = Find-OrCreate "op_forms" "name:eq:$projectFormName" $projectForm "Form proje"
Sync-Record "op_forms" $projectFormId $projectForm "Form proje"

function New-Columns($idMap, $defs) {
    return @($defs | ForEach-Object {
        @{ stateId = $idMap[$_.slug]; title = $_.title; queryKey = "wi_board_column" }
    })
}
$listColumns = @(
    @{ key = "key"; label = "No"; sortable = $true; filterable = $true },
    @{ key = "title"; label = "Başlık"; sortable = $true; filterable = $true },
    @{ key = "stateId"; label = "Durum"; sortable = $true; filterable = $true },
    @{ key = "uttWorkOrderNo"; label = "İş emri no"; sortable = $true; filterable = $true },
    @{ key = "uttPartCode"; label = "Parça kodu"; sortable = $true; filterable = $true },
    @{ key = "uttQuantity"; label = "Adet"; sortable = $true; filterable = $false },
    @{ key = "uttOwner"; label = "Sorumlu"; sortable = $true; filterable = $true },
    @{ key = "uttOpenedOn"; label = "İş emri tarihi"; sortable = $true; filterable = $true; format = "date" },
    @{ key = "uttElapsedDays"; label = "Geçen süre"; sortable = $true; filterable = $false },
    @{ key = "uttQualityGate"; label = "Kalite kapısı"; sortable = $true; filterable = $true }
)
Write-Host "[8] Panolar"
$shopBoardName = "$tag - İş emirleri"
$shopBoard = @{
    name = $shopBoardName; workspaceId = $workspaceId; viewType = "list"
    defaultStateFlowId = $shopFlowId; defaultFormId = $shopFormId; defaultTypeId = $typeShopId
    defaultPriorityId = $priorityId; defaultCategory = "operational"; isDefault = $true
    viewGroups = @($usersGroupId); editGroups = @($editGroupId)
    visibleFields = @("key", "title", "stateId", "uttWorkOrderNo", "uttPartCode", "uttQuantity", "uttOwner", "uttOpenedOn", "uttElapsedDays")
    config = @{
        columns = @(New-Columns $shopIds $shopDefs)
        listColumns = $listColumns
        defaultSort = @{ field = "uttOpenedOn"; direction = "desc" }
    }
}
$shopBoardId = Find-OrCreate "op_boards" "name:eq:$shopBoardName" $shopBoard "Is emirleri"
Sync-Record "op_boards" $shopBoardId $shopBoard "Is emirleri"
$projectBoardName = "$tag - Proje işleri"
$projectListColumns = @(
    @{ key = "key"; label = "No"; sortable = $true; filterable = $true },
    @{ key = "title"; label = "İş"; sortable = $true; filterable = $true },
    @{ key = "stateId"; label = "Durum"; sortable = $true; filterable = $true },
    @{ key = "utpOwner"; label = "Yapan"; sortable = $true; filterable = $true },
    @{ key = "utpOdakNo"; label = "Odak no"; sortable = $true; filterable = $true },
    @{ key = "utpDesignDue"; label = "Tasarım termin"; sortable = $true; filterable = $true; format = "date" },
    @{ key = "utpProjectDue"; label = "Proje termin"; sortable = $true; filterable = $true; format = "date" }
)
$projectBoard = @{
    name = $projectBoardName; workspaceId = $workspaceId; viewType = "list"
    defaultStateFlowId = $projectFlowId; defaultFormId = $projectFormId; defaultTypeId = $typeProjectId
    defaultPriorityId = $priorityId; defaultCategory = "task"; isDefault = $false
    viewGroups = @($usersGroupId); editGroups = @($editGroupId)
    visibleFields = @("key", "title", "stateId", "utpOwner", "utpOdakNo", "utpDesignDue", "utpProjectDue")
    config = @{
        columns = @(New-Columns $projectIds $projectDefs)
        listColumns = $projectListColumns
        defaultSort = @{ field = "title"; direction = "asc" }
    }
}
$projectBoardId = Find-OrCreate "op_boards" "name:eq:$projectBoardName" $projectBoard "Proje isleri"
Sync-Record "op_boards" $projectBoardId $projectBoard "Proje isleri"

function Map-ShopSlug([string]$status) {
    if ([string]::IsNullOrWhiteSpace($status)) { return "baslamadi" }
    if ($status -eq "Sevk Edildi") { return "sevk" }
    if ($status -eq "Başlamadı") { return "baslamadi" }
    if ($status -eq "Reworkte") { return "rework" }
    if ($status -like "Kapandı*") { return "kapandi" }
    if ($status -eq "Üretimde") { return "uretim" }
    if ($status -eq "Talaşlıda") { return "talasli" }
    if ($status -eq "Montajda") { return "montaj" }
    if ($status -eq "Boyada") { return "boya" }
    if ($status -like "Ply Cutter*") { return "ply" }
    if ($status -like "Tasarım*") { return "tasarim" }
    if ($status -like "Yarı Mamül*") { return "yarimamul" }
    if ($status -like "KM*") { return "km" }
    if ($status -like "Kalitede*") { return "kalite" }
    return "baslamadi"
}
function Map-Gate([string]$status) {
    if ($status -match "Üretim sonrası") { return "uretim" }
    if ($status -match "Talaşlı sonrası") { return "talasli" }
    if ($status -match "Montaj sonrası") { return "montaj" }
    if ($status -match "Boya sonrası") { return "boya" }
    return $null
}
function Map-ProjectSlug([string]$status) {
    switch ($status) {
        "Tamamlandı" { return "tamamlandi" }
        "Devam Ediyor" { return "devam" }
        "Beklemede" { return "beklemede" }
        "İptal Edildi" { return "iptal" }
        default { return "atanmadi" }
    }
}
function Split-Part([string]$raw, [string]$qtyText) {
    $code = $null; $name = $raw; $qty = $null
    if ($raw -match '^(.*?)\s+-\s+(\d+)\s+-\s+(.+)$') {
        $code = $Matches[1].Trim()
        $qty = [int]$Matches[2]
        $name = $Matches[3].Trim()
    }
    if ($qtyText -match '^\d+$') { $qty = [int]$qtyText }
    return @{ code = $code; name = $name; qty = $qty }
}
function Add-Date($bag, [string]$key, $raw) {
    $iso = Convert-ExcelDate $raw
    if ($iso) { $bag[$key] = $iso }
}

Write-Host "[9] Kayitlar"
$known = @{}
$existingItems = @(Get-Items (Invoke-DgGet "op_work_items" "workspaceId:eq:$workspaceId" 1000))
foreach ($item in $existingItems) {
    if ($item.key) { $known[[string]$item.key] = $true }
    $extra = $item.extraFields
    if (-not $extra) { continue }
    if ($extra.uttSourceRow) { $known["shop:$($extra.uttSourceRow)"] = $true }
    if ($extra.utpSourceRow) { $known["prj:$($extra.utpSourceRow)"] = $true }
}
$sample = $shopRows | Select-Object -First 1
$sampleOrder = Cell $sample 2
Write-Host ("  ornek satir {0}: emir={1} parca={2} adet={3} sorumlu={4} durum={5}" -f $sample.Row, $sampleOrder, (Cell $sample 3), (Cell $sample 4), (Cell $sample 5), (Cell $sample 26))
if ($sampleOrder -notmatch '^M\d+') { throw "Is emri sutunu beklenen formatta degil: $sampleOrder" }
$orderCounts = @{}
foreach ($row in $shopRows) {
    $n = (Cell $row 2).Trim()
    if (-not $n) { continue }
    if (-not $orderCounts.ContainsKey($n)) { $orderCounts[$n] = 0 }
    $orderCounts[$n]++
}
$orderUse = @{}

$created = 0; $skipped = 0; $failed = 0
foreach ($row in $shopRows) {
    $orderNo = (Cell $row 2).Trim()
    if (-not $orderNo) { continue }
    $sourceKey = "shop:$($row.Row)"
    if (-not $orderUse.ContainsKey($orderNo)) { $orderUse[$orderNo] = 0 }
    $orderUse[$orderNo]++
    $itemKey = if ($orderCounts[$orderNo] -gt 1 -and $orderUse[$orderNo] -gt 1) { "$orderNo-$($orderUse[$orderNo])" } else { $orderNo }
    if ($known.ContainsKey($sourceKey) -or $known.ContainsKey($itemKey)) { $skipped++; continue }
    $part = Split-Part (Cell $row 3) (Cell $row 4)
    $status = Cell $row 26
    $slug = Map-ShopSlug $status
    $extra = @{ uttSourceRow = $row.Row; uttWorkOrderNo = $orderNo; uttPartName = $part.name }
    if ($part.code) { $extra.uttPartCode = $part.code }
    if ($null -ne $part.qty) { $extra.uttQuantity = $part.qty }
    $owner = (Cell $row 5).Trim()
    if ($owner) { $extra.uttOwner = $owner }
    Add-Date $extra "uttOpenedOn" (Cell $row 6)
    Add-Date $extra "uttStageDesign" (Cell $row 7)
    Add-Date $extra "uttStagePly" (Cell $row 8)
    Add-Date $extra "uttStageProduction" (Cell $row 9)
    Add-Date $extra "uttStageQaProduction" (Cell $row 10)
    Add-Date $extra "uttStageMachining" (Cell $row 11)
    Add-Date $extra "uttStageQaMachining" (Cell $row 12)
    Add-Date $extra "uttStageAssembly" (Cell $row 13)
    Add-Date $extra "uttStageQaAssembly" (Cell $row 14)
    Add-Date $extra "uttStagePaint" (Cell $row 15)
    Add-Date $extra "uttStageQaPaint" (Cell $row 16)
    Add-Date $extra "uttStageWip" (Cell $row 17)
    Add-Date $extra "uttStageKm" (Cell $row 18)
    Add-Date $extra "uttShippedOn" (Cell $row 19)
    Add-Date $extra "uttClosedOn" (Cell $row 20)
    $gate = Map-Gate $status
    if ($gate) { $extra.uttQualityGate = $gate }
    $elapsed = Cell $row 27
    if ($elapsed -match '^-?\d+$') { $extra.uttElapsedDays = [int]$elapsed }
    $reworkQty = Cell $row 21
    if ($reworkQty -match '^\d+$' -and [int]$reworkQty -gt 0) { $extra.uttReworkQty = [int]$reworkQty }
    $reason = (Cell $row 22).Trim()
    if ($reason) { $extra.uttReworkReason = $reason }
    $reworkNote = (Cell $row 23).Trim()
    if ($reworkNote) { $extra.uttReworkNote = $reworkNote }
    Add-Date $extra "uttReworkOpenedOn" (Cell $row 24)
    Add-Date $extra "uttReworkClosedOn" (Cell $row 25)
    foreach ($col in 28, 29, 30, 31) {
        $note = (Cell $row $col).Trim()
        if ($note -and $note -notmatch '^\d+(\.\d+)?$') { $extra.uttNotes = $note; break }
    }
    $titleName = if ($part.name) { $part.name } else { $orderNo }
    $payload = @{
        key = $itemKey
        workspaceKey = $workspaceKey
        workspaceId = $workspaceId
        boardId = $shopBoardId
        typeId = $typeShopId
        category = "operational"
        title = "$orderNo · $titleName"
        stateId = $shopIds[$slug]
        stateFlowId = $shopFlowId
        priorityId = $priorityId
        extraFields = $extra
    }
    if ($slug -in @("kapandi", "sevk")) {
        $closedOn = $extra.uttClosedOn
        if (-not $closedOn) { $closedOn = $extra.uttShippedOn }
        if ($closedOn) { $payload.closedAt = $closedOn }
    }
    try {
        Invoke-Dg POST "$BaseUrl$dataPath/op_work_items" $payload | Out-Null
        $known[$sourceKey] = $true
        $created++
        if ($created % 50 -eq 0) { Write-Host "  is emri $created" }
    }
    catch {
        $failed++
        Write-Host "  FAIL $orderNo $($_.Exception.Message)"
        if ($_.ErrorDetails.Message) { Write-Host "    $($_.ErrorDetails.Message)" }
        if ($failed -ge 5) { throw "Cok fazla is emri hatasi." }
    }
}

$projectCreated = 0
foreach ($row in $projectRows) {
    $no = (Cell $row 2).Trim()
    $title = (Cell $row 3).Trim()
    if ($no -notmatch '^\d+$' -or -not $title) { continue }
    $sourceKey = "prj:$($row.Row)"
    if ($known.ContainsKey($sourceKey)) { $skipped++; continue }
    $status = Cell $row 11
    $slug = Map-ProjectSlug $status
    $extra = @{ utpSourceRow = $row.Row }
    $odak = (Cell $row 4).Trim(); if ($odak) { $extra.utpOdakNo = $odak }
    $customer = (Cell $row 5).Trim(); if ($customer) { $extra.utpCustomerNo = $customer }
    $folder = (Cell $row 6).Trim(); if ($folder) { $extra.utpFolder = $folder }
    $refs = (Cell $row 7).Trim(); if ($refs) { $extra.utpRefFolders = $refs }
    $owner = (Cell $row 8).Trim(); if ($owner) { $extra.utpOwner = $owner }
    Add-Date $extra "utpDesignDue" (Cell $row 9)
    Add-Date $extra "utpProjectDue" (Cell $row 10)
    $note = (Cell $row 12).Trim(); if ($note) { $extra.utpNotes = $note }
    $itemKey = "PRJ-$no"
    if ($known.ContainsKey($itemKey)) { $skipped++; continue }
    $payload = @{
        key = $itemKey
        workspaceKey = $workspaceKey
        workspaceId = $workspaceId
        boardId = $projectBoardId
        typeId = $typeProjectId
        category = "task"
        title = $title
        stateId = $projectIds[$slug]
        stateFlowId = $projectFlowId
        priorityId = $priorityId
        extraFields = $extra
    }
    try {
        Invoke-Dg POST "$BaseUrl$dataPath/op_work_items" $payload | Out-Null
        $known[$sourceKey] = $true
        $projectCreated++
    }
    catch {
        $failed++
        Write-Host "  FAIL PRJ-$no $($_.Exception.Message)"
        if ($_.ErrorDetails.Message) { Write-Host "    $($_.ErrorDetails.Message)" }
    }
}

Write-Host "[10] Metadata cache"
try {
    Invoke-Dg POST "$BaseUrl/operations/api/v1/workspaces/$workspaceId/metadata-cache/reload" @{} | Out-Null
    Write-Host "  cache yenilendi"
}
catch {
    Write-Host "  cache uyarisi: $($_.Exception.Message)"
}

Write-Host "BITTI workspace=$workspaceId isEmriYeni=$created projeYeni=$projectCreated atlanan=$skipped hata=$failed"
