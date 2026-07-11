# Operation Core — Zimmet workspace seed (GIR + ZIM) + demo
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\setup-zimmet-datasets-and-forms.ps1
#   .\docs\odak\zimmet\scripts\seed-zimmet-master-data.ps1
#   .\docs\odak\zimmet\scripts\seed-operation-core-zimmet.ps1
#   .\docs\odak\zimmet\scripts\seed-operation-core-zimmet.ps1 -SeedDemo -ReloadMetadataCache

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$UseGateway = $true,
    [switch]$SeedDemo = $false,
    [switch]$ReloadMetadataCache = $false,
    [string]$MasterIdsFile = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/ZimmetDgCommon.ps1")

if ([string]::IsNullOrEmpty($MasterIdsFile)) {
    $MasterIdsFile = Join-Path $repoRoot "docs/odak/zimmet/seed/zimmet_master_ids.json"
}
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $repoRoot "docs/odak/zimmet/seed/zimmet-oc-seed.json"
}

$ctx = Initialize-ZimmetDgSession -BaseUrl $BaseUrl -UseGateway:$UseGateway -RepoRoot $repoRoot
$moParams = @{ Headers = $ctx.Headers; ErrorAction = "Stop" }
if ($MoBaseUrl.StartsWith("https://") -and $ctx.IrmParams.ContainsKey("SkipCertificateCheck")) {
    $moParams.SkipCertificateCheck = $true
}

$tagGir = "Zimmet Depo"
$tagZim = "Personel Zimmet"
$wsGirName = "Zimmet Depo"
$wsZimName = "Personel Zimmet"

function Invoke-MoPost {
    param([string]$Uri, [object]$Body)
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -TimeoutSec 120 @moParams
}

function Invoke-MoPut {
    param([string]$Uri, [object]$Body)
    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    return Invoke-RestMethod -Uri $Uri -Method PUT -Body $json @moParams
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Zimmet — Operation Core seed" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- Shared priorities ---
Write-Host "[1] op_priorities..." -ForegroundColor Yellow
$prioNormalId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_priorities" -Filter "name:eq:ZIM - Normal" -Label "ZIM Normal" -Body @{
    name = "ZIM - Normal"; level = "3"; sortOrder = 30; color = "info"
}
$prioHighId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_priorities" -Filter "name:eq:ZIM - Yuksek" -Label "ZIM Yuksek" -Body @{
    name = "ZIM - Yuksek"; level = "2"; sortOrder = 20; color = "warning"
}
$sharedPriorityIds = @($prioNormalId, $prioHighId)

# --- Pool fields (GIR) ---
Write-Host "[2] op_fields (depo + zimmet)..." -ForegroundColor Yellow
$optKaynak = New-ZimmetStaticSelectOptions @(
    @{ value = "manuel"; label = "Manuel giris" },
    @{ value = "satinalma"; label = "Satinalma (F4)" }
)
$optTeslimDurumu = New-ZimmetStaticSelectOptions @(
    @{ value = "yeni"; label = "Yeni" },
    @{ value = "iyi"; label = "Iyi" },
    @{ value = "orta"; label = "Orta" },
    @{ value = "hasarli"; label = "Hasarli" }
)
$optIadeDurumu = New-ZimmetStaticSelectOptions @(
    @{ value = "tam"; label = "Tam ve saglam" },
    @{ value = "hasarli"; label = "Hasarli" },
    @{ value = "eksik"; label = "Eksik parca" }
)

$fieldKatalogUrunId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:katalogUrunId" -Label "katalogUrunId" -Body @{
    key = "katalogUrunId"; label = "Katalog urun"; fieldType = "relation"; scope = "pool"; category = "reference"
    relationDatasetName = "zimmet_urunler"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_urunler" -LabelField "ad" -SearchFields @("ad", "kod", "marka") -Filter "aktif:eq:true")
}
$fieldMiktar = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:miktar" -Label "miktar" -Body @{
    key = "miktar"; label = "Miktar"; fieldType = "number"; scope = "pool"; category = "quantity"
}
$fieldDepoId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:depoId" -Label "depoId" -Body @{
    key = "depoId"; label = "Depo"; fieldType = "relation"; scope = "pool"; category = "location"
    relationDatasetName = "zimmet_depolar"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_depolar" -LabelField "ad" -SearchFields @("ad", "kod"))
}
$fieldLokasyonId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:lokasyonId" -Label "lokasyonId" -Body @{
    key = "lokasyonId"; label = "Lokasyon"; fieldType = "relation"; scope = "pool"; category = "location"
    relationDatasetName = "zimmet_depo_lokasyonlari"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_depo_lokasyonlari" -LabelField "ad" -SearchFields @("ad", "kod") -DependsOn @{
        fieldKey = "depoId"; filterTemplate = "depoId={{parentValue}}"
    })
}
$fieldTedarikciId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:tedarikciId" -Label "tedarikciId" -Body @{
    key = "tedarikciId"; label = "Tedarikci"; fieldType = "relation"; scope = "pool"; category = "reference"
    relationDatasetName = "tedarikciler"
    options = (New-ZimmetLookupOptions -DatasetName "tedarikciler" -LabelField "unvan" -SearchFields @("unvan", "kod") -Filter $null)
}
$fieldFaturaNo = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:faturaNo" -Label "faturaNo" -Body @{
    key = "faturaNo"; label = "Fatura no"; fieldType = "text"; scope = "pool"; category = "finance"
}
$fieldGirisTarihi = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:girisTarihi" -Label "girisTarihi" -Body @{
    key = "girisTarihi"; label = "Giris tarihi"; fieldType = "date"; scope = "pool"; category = "date"
}
$fieldKaynak = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:kaynak" -Label "kaynak" -Body @{
    key = "kaynak"; label = "Kaynak"; fieldType = "select"; scope = "pool"; category = "classification"; options = $optKaynak
}
$fieldSeriNoListesi = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:seriNoListesi" -Label "seriNoListesi" -Body @{
    key = "seriNoListesi"; label = "Seri no listesi"; fieldType = "text"; scope = "pool"; category = "technical"
}

$demirbasPickerColumns = @(
    @{ field = "demirbasNo"; title = "Demirbas no"; sortable = $true; filterable = $true; width = 120 }
    @{ field = "seriNo"; title = "Seri no"; sortable = $true; filterable = $true }
    @{ field = "durum"; title = "Durum"; filterable = $true; format = "enum"; enumMap = @{
            depoda = "Depoda"; zimmetli = "Zimmetli"; bakimda = "Bakimda"; hurda = "Hurda"; kayip = "Kayip"
        }
    }
    @{ field = "katalogUrunId"; title = "Urun"; format = "relationLabel" }
    @{ field = "depoId"; title = "Depo"; format = "relationLabel" }
)

# Iade: multi demirbas (zimmetli + personel dependsOn). Key stays demirbasId for existing WIs.
$fieldDemirbasId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:demirbasId" -Label "demirbasId" -Body @{
    key = "demirbasId"; label = "Demirbaslar"; fieldType = "relation"; scope = "pool"; category = "reference"
    cardinality = "multi"
    relationDatasetName = "zimmet_demirbaslar"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_demirbaslar" -LabelField "demirbasNo" `
        -SearchFields @("demirbasNo", "seriNo") -Filter "durum:eq:zimmetli" -Presentation "picker" `
        -Columns $demirbasPickerColumns -DefaultSort @{ field = "demirbasNo"; dir = "asc" } `
        -DependsOn @{ fieldKey = "personelId"; filterTemplate = "zimmetliPersonelId:eq:{{parentValue}}" } `
        -Selection @{ mode = "multi"; min = 1; max = 50; displayFields = @("katalogUrunId", "demirbasNo"); displaySeparator = " · " })
}
# Force-refresh lookup options (Find-ZimmetOrCreate may skip body on existing)
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_fields/$fieldDemirbasId" -Body @{
    key = "demirbasId"; label = "Demirbaslar"; fieldType = "relation"; scope = "pool"; category = "reference"
    cardinality = "multi"
    relationDatasetName = "zimmet_demirbaslar"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_demirbaslar" -LabelField "demirbasNo" `
        -SearchFields @("demirbasNo", "seriNo") -Filter "durum:eq:zimmetli" -Presentation "picker" `
        -Columns $demirbasPickerColumns -DefaultSort @{ field = "demirbasNo"; dir = "asc" } `
        -DependsOn @{ fieldKey = "personelId"; filterTemplate = "zimmetliPersonelId:eq:{{parentValue}}" } `
        -Selection @{ mode = "multi"; min = 1; max = 50; displayFields = @("katalogUrunId", "demirbasNo"); displaySeparator = " · " })
} | Out-Null

$fieldDemirbasIds = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:demirbasIds" -Label "demirbasIds" -Body @{
    key = "demirbasIds"; label = "Demirbaslar"; fieldType = "relation"; scope = "pool"; category = "reference"
    cardinality = "multi"
    relationDatasetName = "zimmet_demirbaslar"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_demirbaslar" -LabelField "demirbasNo" `
        -SearchFields @("demirbasNo", "seriNo") -Filter "durum:eq:depoda" -Presentation "picker" `
        -Columns $demirbasPickerColumns -DefaultSort @{ field = "demirbasNo"; dir = "asc" } `
        -Selection @{ mode = "multi"; min = 1; max = 50; displayFields = @("katalogUrunId", "demirbasNo"); displaySeparator = " · " })
}
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_fields/$fieldDemirbasIds" -Body @{
    key = "demirbasIds"; label = "Demirbaslar"; fieldType = "relation"; scope = "pool"; category = "reference"
    cardinality = "multi"
    relationDatasetName = "zimmet_demirbaslar"
    options = (New-ZimmetLookupOptions -DatasetName "zimmet_demirbaslar" -LabelField "demirbasNo" `
        -SearchFields @("demirbasNo", "seriNo") -Filter "durum:eq:depoda" -Presentation "picker" `
        -Columns $demirbasPickerColumns -DefaultSort @{ field = "demirbasNo"; dir = "asc" } `
        -Selection @{ mode = "multi"; min = 1; max = 50; displayFields = @("katalogUrunId", "demirbasNo"); displaySeparator = " · " })
} | Out-Null
$fieldPersonelId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:personelId" -Label "personelId" -Body @{
    key = "personelId"; label = "Personel"; fieldType = "persons"; scope = "pool"; category = "assignment"; cardinality = "single"
}
$fieldDepartman = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:departman" -Label "departman" -Body @{
    key = "departman"; label = "Departman"; fieldType = "text"; scope = "pool"; category = "organization"
}
$fieldTeslimTarihi = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:teslimTarihi" -Label "teslimTarihi" -Body @{
    key = "teslimTarihi"; label = "Teslim tarihi"; fieldType = "date"; scope = "pool"; category = "date"
}
$fieldPlanliIadeTarihi = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:planliIadeTarihi" -Label "planliIadeTarihi" -Body @{
    key = "planliIadeTarihi"; label = "Planli iade tarihi"; fieldType = "date"; scope = "pool"; category = "date"
}
$fieldTeslimDurumu = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:teslimDurumu" -Label "teslimDurumu" -Body @{
    key = "teslimDurumu"; label = "Teslim durumu"; fieldType = "select"; scope = "pool"; category = "condition"; options = $optTeslimDurumu
}
$fieldZimmetNotu = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:zimmetNotu" -Label "zimmetNotu" -Body @{
    key = "zimmetNotu"; label = "Zimmet notu"; fieldType = "text"; scope = "pool"; category = "notes"
}
$fieldIadeDurumu = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:iadeDurumu" -Label "iadeDurumu" -Body @{
    key = "iadeDurumu"; label = "Iade durumu"; fieldType = "select"; scope = "pool"; category = "condition"; options = $optIadeDurumu
}
$fieldHasarAciklamasi = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_fields" -Filter "key:eq:hasarAciklamasi" -Label "hasarAciklamasi" -Body @{
    key = "hasarAciklamasi"; label = "Hasar aciklamasi"; fieldType = "text"; scope = "pool"; category = "notes"
}

$girFieldIds = @(
    $fieldKatalogUrunId, $fieldMiktar, $fieldDepoId, $fieldLokasyonId, $fieldTedarikciId,
    $fieldFaturaNo, $fieldGirisTarihi, $fieldKaynak, $fieldSeriNoListesi
)
$zimFieldIds = @(
    $fieldDemirbasId, $fieldDemirbasIds, $fieldPersonelId, $fieldDepartman, $fieldTeslimTarihi, $fieldPlanliIadeTarihi,
    $fieldTeslimDurumu, $fieldZimmetNotu, $fieldIadeDurumu, $fieldHasarAciklamasi
)
$allFieldIds = @($girFieldIds + $zimFieldIds | Select-Object -Unique)

# --- Work item types ---
Write-Host "[3] op_work_item_types..." -ForegroundColor Yellow
$typeGirisId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_work_item_types" -Filter "name:eq:Depo girisi" -Label "Depo girisi" -Body @{
    name = "Depo girisi"; category = "operational"; color = "primary"; icon = "PackageIcon"; sortOrder = 50
}
$typeZimmetVermeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_work_item_types" -Filter "name:eq:Zimmet verme" -Label "Zimmet verme" -Body @{
    name = "Zimmet verme"; category = "operational"; color = "info"; icon = "UserCheckIcon"; sortOrder = 51
}
$typeZimmetIadeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_work_item_types" -Filter "name:eq:Zimmet iade" -Label "Zimmet iade" -Body @{
    name = "Zimmet iade"; category = "service_request"; color = "warning"; icon = "ArrowBackIcon"; sortOrder = 52
}

# --- States GIR ---
Write-Host "[4] op_states (GIR)..." -ForegroundColor Yellow
$girDraft = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagGir - Taslak" -Label "GIR Taslak" -Body @{
    name = "$tagGir - Taslak"; category = "open"; isInitial = $true; isStart = $true; color = "secondary"; sortOrder = 10
}
$girReceive = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagGir - Mal kabul" -Label "GIR Mal kabul" -Body @{
    name = "$tagGir - Mal kabul"; category = "in_progress"; color = "warning"; sortOrder = 20
}
$girStocked = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagGir - Stoklandi" -Label "GIR Stoklandi" -Body @{
    name = "$tagGir - Stoklandi"; category = "in_progress"; color = "info"; sortOrder = 30
}
$girClosed = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagGir - Kapali" -Label "GIR Kapali" -Body @{
    name = "$tagGir - Kapali"; category = "closed"; isClosed = $true; isTerminal = $true; color = "success"; sortOrder = 40
}
$girStateIds = @($girDraft, $girReceive, $girStocked, $girClosed)

# --- States ZIM verme ---
Write-Host "[5] op_states (ZIM)..." -ForegroundColor Yellow
$zimRequest = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Talep" -Label "ZIM Talep" -Body @{
    name = "$tagZim - Talep"; category = "open"; isInitial = $true; isStart = $true; color = "info"; sortOrder = 50
}
$zimApproval = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Onay bekliyor" -Label "ZIM Onay" -Body @{
    name = "$tagZim - Onay bekliyor"; category = "on_hold"; color = "warning"; sortOrder = 60
}
$zimDelivered = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Teslim edildi" -Label "ZIM Teslim" -Body @{
    name = "$tagZim - Teslim edildi"; category = "in_progress"; color = "primary"; sortOrder = 70
}
$zimActive = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Aktif" -Label "ZIM Aktif" -Body @{
    name = "$tagZim - Aktif"; category = "in_progress"; color = "success"; sortOrder = 80
}
$zimClosed = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Kapali" -Label "ZIM Kapali" -Body @{
    name = "$tagZim - Kapali"; category = "closed"; isClosed = $true; isTerminal = $true; color = "secondary"; sortOrder = 90
}
$zimIadeOpen = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Iade acik" -Label "ZIM Iade acik" -Body @{
    name = "$tagZim - Iade acik"; category = "open"; isInitial = $true; color = "warning"; sortOrder = 100
}
$zimIadeDone = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_states" -Filter "name:eq:$tagZim - Iade tamam" -Label "ZIM Iade tamam" -Body @{
    name = "$tagZim - Iade tamam"; category = "closed"; allowReopen = $true; color = "success"; sortOrder = 110
}
$zimStateIds = @($zimRequest, $zimApproval, $zimDelivered, $zimActive, $zimClosed, $zimIadeOpen, $zimIadeDone)

# --- Workspaces ---
Write-Host "[6] op_workspaces..." -ForegroundColor Yellow
$wsGirId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_workspaces" -Filter "name:eq:$wsGirName" -Label "WS GIR" -Body @{
    name                  = $wsGirName
    workspaceType         = "operational"
    description           = "Demirbas depo giris islemleri"
    workItemKeyPrefix     = "GIR"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    enabledTypeIds        = @($typeGirisId)
    enabledStateIds       = $girStateIds
    enabledPriorityIds    = $sharedPriorityIds
    enabledFieldIds       = $girFieldIds
}
$wsZimId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_workspaces" -Filter "name:eq:$wsZimName" -Label "WS ZIM" -Body @{
    name                  = $wsZimName
    workspaceType         = "operational"
    description           = "Personele demirbas zimmet verme ve iade"
    workItemKeyPrefix     = "ZIM"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    enabledTypeIds        = @($typeZimmetVermeId, $typeZimmetIadeId)
    enabledStateIds       = $zimStateIds
    enabledPriorityIds    = $sharedPriorityIds
    enabledFieldIds       = $zimFieldIds
}
$wsZimBody = @{
    name                  = $wsZimName
    workspaceType         = "operational"
    description           = "Personele demirbas zimmet verme ve iade"
    workItemKeyPrefix     = "ZIM"
    workItemKeyFormat     = "{prefix}-{seq:D4}"
    workItemSequenceStart = 1
    enabledTypeIds        = @($typeZimmetVermeId, $typeZimmetIadeId)
    enabledStateIds       = $zimStateIds
    enabledPriorityIds    = $sharedPriorityIds
    enabledFieldIds       = $zimFieldIds
}
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_workspaces/$wsZimId" -Body $wsZimBody | Out-Null
Write-Host "  UPDATED: WS ZIM enabledFieldIds (demirbasIds)" -ForegroundColor Green

# --- Flows ---
Write-Host "[7] op_state_flows..." -ForegroundColor Yellow
$flowGirName = "$tagGir — Ana Akis"
$flowGirId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_state_flows" -Filter "name:eq:$flowGirName" -Label "GIR flow" -Body @{
    name           = $flowGirName
    workspaceId    = $wsGirId
    initialStateId = $girDraft
    isDefault      = $true
    isActive       = $true
    transitions    = @(
        (New-ZimmetTransition "submit" $girDraft $girReceive "Gonder" 0),
        (New-ZimmetTransition "receive" $girReceive $girStocked "Mal kabul et" 1 @("katalogUrunId", "miktar", "depoId")),
        (New-ZimmetTransition "stock" $girStocked $girClosed "Stokla ve kapat" 2)
    )
}

$flowZimVermeName = "$tagZim — Verme"
$flowZimVermeBody = @{
    name           = $flowZimVermeName
    workspaceId    = $wsZimId
    initialStateId = $zimRequest
    isDefault      = $true
    isActive       = $true
    transitions    = @(
        (New-ZimmetTransition "request" $zimRequest $zimApproval "Onaya gonder" 0 @("demirbasIds", "personelId")),
        (New-ZimmetTransition "approve" $zimApproval $zimDelivered "Onayla" 1),
        (New-ZimmetTransition "reject" $zimApproval $zimClosed "Reddet" 2),
        (New-ZimmetTransition "deliver" $zimDelivered $zimActive "Teslim et" 3 @("teslimTarihi", "teslimDurumu")),
        (New-ZimmetTransition "close" $zimActive $zimClosed "Tamamla" 4)
    )
}
$flowZimVermeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_state_flows" -Filter "name:eq:$flowZimVermeName" -Label "ZIM verme flow" -Body $flowZimVermeBody
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_state_flows/$flowZimVermeId" -Body $flowZimVermeBody | Out-Null
Write-Host "  UPDATED: ZIM verme flow (demirbasIds)" -ForegroundColor Green

$flowZimIadeName = "$tagZim — Iade"
$flowZimIadeBody = @{
    name           = $flowZimIadeName
    workspaceId    = $wsZimId
    initialStateId = $zimIadeOpen
    isDefault      = $false
    isActive       = $true
    transitions    = @(
        (New-ZimmetTransition "receive_return" $zimIadeOpen $zimIadeDone "Iade al" 0),
        (New-ZimmetTransition "close_return" $zimIadeDone $zimClosed "Kapat" 1)
    )
}
$flowZimIadeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_state_flows" -Filter "name:eq:$flowZimIadeName" -Label "ZIM iade flow" -Body $flowZimIadeBody
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_state_flows/$flowZimIadeId" -Body $flowZimIadeBody | Out-Null
Write-Host "  UPDATED: ZIM iade flow" -ForegroundColor Green

Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_work_item_types/$typeGirisId" -Body @{
    name = "Depo girisi"; category = "operational"; defaultStateFlowId = $flowGirId
} | Out-Null
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_work_item_types/$typeZimmetVermeId" -Body @{
    name = "Zimmet verme"; category = "operational"; defaultStateFlowId = $flowZimVermeId
} | Out-Null
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_work_item_types/$typeZimmetIadeId" -Body @{
    name = "Zimmet iade"; category = "service_request"; defaultStateFlowId = $flowZimIadeId
} | Out-Null

# --- Forms ---
Write-Host "[8] op_forms..." -ForegroundColor Yellow
$formGirName = "$tagGir — Yeni giris"
$formGirId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_forms" -Filter "name:eq:$formGirName" -Label "GIR form" -Body @{
    name = $formGirName; workspaceId = $wsGirId; defaultTypeId = $typeGirisId
    defaultStateFlowId = $flowGirId; defaultStateId = $girDraft; isDefault = $true
    layout = @{
        sections = @(@{
            key = "main"; title = "Depo giris bilgileri"
            fields = @("title", "description", "typeId", "priorityId", "katalogUrunId", "miktar", "depoId", "lokasyonId", "tedarikciId", "faturaNo", "girisTarihi", "kaynak", "seriNoListesi")
        })
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        description = @{ visible = $true }
        typeId = @{ visible = $true; readonly = $true }
        priorityId = @{ visible = $true }
        katalogUrunId = @{ visible = $true; required = $true }
        miktar = @{ visible = $true; required = $true }
        depoId = @{ visible = $true; required = $true }
        lokasyonId = @{ visible = $true }
        tedarikciId = @{ visible = $true }
        faturaNo = @{ visible = $true }
        girisTarihi = @{ visible = $true; required = $true }
        kaynak = @{ visible = $true }
        seriNoListesi = @{ visible = $true }
    }
    defaultValues = @{ kaynak = "manuel"; miktar = 1 }
}

$formZimVermeName = "$tagZim — Zimmet verme"
$formZimVermeBody = @{
    name = $formZimVermeName; workspaceId = $wsZimId; defaultTypeId = $typeZimmetVermeId
    defaultStateFlowId = $flowZimVermeId; defaultStateId = $zimRequest; isDefault = $true
    layout = @{
        sections = @(@{
            key = "main"; title = "Zimmet bilgileri"
            fields = @("title", "description", "typeId", "priorityId", "demirbasIds", "personelId", "departman", "teslimTarihi", "planliIadeTarihi", "teslimDurumu", "zimmetNotu")
        })
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        description = @{ visible = $true }
        typeId = @{ visible = $true; readonly = $true }
        priorityId = @{ visible = $true }
        demirbasIds = @{ visible = $true; required = $true }
        personelId = @{ visible = $true; required = $true }
        departman = @{ visible = $true }
        teslimTarihi = @{ visible = $true }
        planliIadeTarihi = @{ visible = $true }
        teslimDurumu = @{ visible = $true }
        zimmetNotu = @{ visible = $true }
    }
    defaultValues = @{ teslimDurumu = "yeni" }
}
$formZimVermeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_forms" -Filter "name:eq:$formZimVermeName" -Label "ZIM verme form" -Body $formZimVermeBody
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_forms/$formZimVermeId" -Body $formZimVermeBody | Out-Null
Write-Host "  UPDATED: ZIM verme form (demirbasIds picker)" -ForegroundColor Green

$formZimIadeName = "$tagZim — Zimmet iade"
$formZimIadeBody = @{
    name = $formZimIadeName; workspaceId = $wsZimId; defaultTypeId = $typeZimmetIadeId
    defaultStateFlowId = $flowZimIadeId; defaultStateId = $zimIadeOpen; isDefault = $false
    layout = @{
        sections = @(@{
            key = "main"; title = "Iade bilgileri"
            fields = @("title", "description", "typeId", "personelId", "demirbasId", "iadeDurumu", "hasarAciklamasi", "zimmetNotu")
        })
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        description = @{ visible = $true }
        typeId = @{ visible = $true; readonly = $true }
        personelId = @{ visible = $true; required = $true }
        demirbasId = @{ visible = $true; required = $true }
        iadeDurumu = @{ visible = $true; required = $true }
        hasarAciklamasi = @{ visible = $true }
        zimmetNotu = @{ visible = $true }
    }
}
$formZimIadeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_forms" -Filter "name:eq:$formZimIadeName" -Label "ZIM iade form" -Body $formZimIadeBody
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_forms/$formZimIadeId" -Body $formZimIadeBody | Out-Null
Write-Host "  UPDATED: ZIM iade form (demirbasId picker)" -ForegroundColor Green

# --- Boards ---
Write-Host "[9] op_boards..." -ForegroundColor Yellow
$boardGirQueueName = "$tagGir — Giris kuyrugu"
$boardGirId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_boards" -Filter "name:eq:$boardGirQueueName" -Label "GIR board" -Body @{
    name = $boardGirQueueName; workspaceId = $wsGirId; viewType = "list"
    defaultStateFlowId = $flowGirId; defaultFormId = $formGirId; defaultTypeId = $typeGirisId
    defaultPriorityId = $prioNormalId; defaultStateId = $girDraft
    visibleFields = @("key", "title", "stateId", "priorityId", "assignee")
    config = @{
        columns = @(
            @{ stateId = $girDraft; title = "Taslak"; queryKey = "wi_board_column" },
            @{ stateId = $girReceive; title = "Mal kabul"; queryKey = "wi_board_column" },
            @{ stateId = $girStocked; title = "Stoklandi"; queryKey = "wi_board_column" },
            @{ stateId = $girClosed; title = "Kapali"; queryKey = "wi_board_column" }
        )
    }
}

$boardZimQueueName = "$tagZim — Zimmet kuyrugu"
$boardZimId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_boards" -Filter "name:eq:$boardZimQueueName" -Label "ZIM board" -Body @{
    name = $boardZimQueueName; workspaceId = $wsZimId; viewType = "list"
    defaultStateFlowId = $flowZimVermeId; defaultFormId = $formZimVermeId; defaultTypeId = $typeZimmetVermeId
    defaultPriorityId = $prioNormalId; defaultStateId = $zimRequest
    visibleFields = @("key", "title", "typeId", "stateId", "priorityId", "assignee")
    config = @{
        columns = @(
            @{ stateId = $zimRequest; title = "Talep"; queryKey = "wi_board_column" },
            @{ stateId = $zimApproval; title = "Onay"; queryKey = "wi_board_column" },
            @{ stateId = $zimDelivered; title = "Teslim"; queryKey = "wi_board_column" },
            @{ stateId = $zimActive; title = "Aktif"; queryKey = "wi_board_column" },
            @{ stateId = $zimClosed; title = "Kapali"; queryKey = "wi_board_column" }
        )
    }
}

$boardZimIadeName = "$tagZim — Iade kuyrugu"
$boardZimIadeId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_boards" -Filter "name:eq:$boardZimIadeName" -Label "ZIM iade board" -Body @{
    name = $boardZimIadeName; workspaceId = $wsZimId; viewType = "list"
    defaultStateFlowId = $flowZimIadeId; defaultFormId = $formZimIadeId; defaultTypeId = $typeZimmetIadeId
    defaultPriorityId = $prioNormalId; defaultStateId = $zimIadeOpen
    visibleFields = @("key", "title", "stateId", "assignee")
    config = @{
        columns = @(
            @{ stateId = $zimIadeOpen; title = "Iade acik"; queryKey = "wi_board_column" },
            @{ stateId = $zimIadeDone; title = "Tamam"; queryKey = "wi_board_column" }
        )
    }
}

# --- Profiles ---
Write-Host "[10] op_profiles..." -ForegroundColor Yellow
$zimProfileName = "$tagZim — Kayit profili"
# Profile layout drives readonly display (not default verme form). Include both verme
# (demirbasIds) and iade (demirbasId) fields; UI hideEmptySections hides unused ones.
$zimProfileBody = @{
    name = $zimProfileName; workspaceId = $wsZimId; defaultTypeId = $typeZimmetVermeId; isDefault = $true
    layout = @{
        sections = @(
            @{
                key = "main"; title = "Zimmet ozeti"
                fields = @(
                    "title", "description", "typeId", "priorityId",
                    "personelId", "demirbasIds", "demirbasId",
                    "teslimTarihi", "planliIadeTarihi", "teslimDurumu",
                    "iadeDurumu", "hasarAciklamasi", "zimmetNotu", "departman"
                )
            }
        )
    }
    fieldBehaviors = @{
        title = @{ visible = $true; required = $true }
        description = @{ visible = $true }
        typeId = @{ visible = $true }
        priorityId = @{ visible = $true }
        demirbasIds = @{ visible = $true }
        demirbasId = @{ visible = $true }
        personelId = @{ visible = $true }
        teslimTarihi = @{ visible = $true }
        planliIadeTarihi = @{ visible = $true }
        teslimDurumu = @{ visible = $true }
        iadeDurumu = @{ visible = $true }
        hasarAciklamasi = @{ visible = $true }
        zimmetNotu = @{ visible = $true }
        departman = @{ visible = $true }
    }
    actions = @(
        @{ transitionKey = "request"; order = 0; label = "Onaya gonder" },
        @{ transitionKey = "approve"; order = 1; label = "Onayla" },
        @{ transitionKey = "deliver"; order = 2; label = "Teslim et" },
        @{ transitionKey = "close"; order = 3; label = "Tamamla" },
        @{ transitionKey = "receive_return"; order = 10; label = "Iade al" },
        @{ transitionKey = "close_return"; order = 11; label = "Kapat" }
    )
}
$zimProfileId = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_profiles" -Filter "name:eq:$zimProfileName" -Label "ZIM profile" -Body $zimProfileBody
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_profiles/$zimProfileId" -Body $zimProfileBody | Out-Null
Write-Host "  UPDATED: ZIM profile (verme + iade actions)" -ForegroundColor Green

$summary = [ordered]@{
    workspaces = @{
        gir = @{ id = $wsGirId; name = $wsGirName; prefix = "GIR" }
        zim = @{ id = $wsZimId; name = $wsZimName; prefix = "ZIM" }
    }
    boards = @{
        girQueue = $boardGirId
        zimQueue = $boardZimId
        zimIade  = $boardZimIadeId
    }
    types = @{
        depoGirisi   = $typeGirisId
        zimmetVerme  = $typeZimmetVermeId
        zimmetIade   = $typeZimmetIadeId
    }
    flows = @{
        gir       = $flowGirId
        zimVerme  = $flowZimVermeId
        zimIade   = $flowZimIadeId
    }
    seededAt = (Get-Date).ToUniversalTime().ToString("o")
}

# --- AF-1: GIR stock → createDatasetRows (zimmet_demirbaslar) ---
Write-Host "`n[Rules] GIR createDatasetRows (AF-1)..." -ForegroundColor Yellow
$ruleAf1Name = "$tagGir — Stokla demirbas uret"
$ruleAf1Body = @{
    name           = $ruleAf1Name
    workspaceId    = $wsGirId
    ruleType       = "automation"
    trigger        = "WorkItemTransitioned"
    isActive       = $true
    priority       = 100
    typeId         = $typeGirisId
    transitionKey  = "stock"
    actions        = @(
        @{
            type         = "createDatasetRows"
            dataset      = "zimmet_demirbaslar"
            onError      = "failTransition"
            cardinality  = @{
                mode      = "count"
                countFrom = "fields.miktar"
            }
            idempotency  = @{
                mode        = "one_per_source"
                lookupField = "girisRef"
                lookupFrom  = "key"
            }
            fieldMappings = @(
                @{ target = "katalogUrunId"; source = "field"; path = "fields.katalogUrunId" }
                @{ target = "depoId"; source = "field"; path = "fields.depoId" }
                @{ target = "lokasyonId"; source = "field"; path = "fields.lokasyonId" }
                @{ target = "durum"; source = "static"; value = "depoda" }
                @{ target = "girisTarihi"; source = "field"; path = "fields.girisTarihi" }
                @{ target = "girisRef"; source = "field"; path = "key" }
                @{
                    target    = "seriNo"
                    source    = "sequence"
                    template  = "{{source.key}}-{000}"
                    startFrom = 1
                }
                @{ target = "tedarikciId"; source = "field"; path = "fields.tedarikciId" }
            )
        }
    )
}
$ruleAf1Id = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_rules" -Filter "name:eq:$ruleAf1Name" -Label "AF-1 rule" -Body $ruleAf1Body
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_rules/$ruleAf1Id" -Body $ruleAf1Body | Out-Null
Write-Host "  OK: $ruleAf1Name ($ruleAf1Id)" -ForegroundColor Green

# --- OC-1: ZIM deliver → updateDatasetRows (demirbas zimmetli) ---
Write-Host "`n[Rules] ZIM updateDatasetRows (OC-1)..." -ForegroundColor Yellow
$ruleOc1Name = "$tagZim — Teslimde demirbas zimmetle"
$ruleOc1Body = @{
    name           = $ruleOc1Name
    workspaceId    = $wsZimId
    ruleType       = "automation"
    trigger        = "WorkItemTransitioned"
    isActive       = $true
    priority       = 100
    typeId         = $typeZimmetVermeId
    transitionKey  = "deliver"
    actions        = @(
        @{
            type         = "updateDatasetRows"
            dataset      = "zimmet_demirbaslar"
            onError      = "failTransition"
            cardinality  = @{
                mode      = "expand"
                itemsFrom = "fields.demirbasIds"
                itemAs    = "id"
            }
            targetId     = @{
                source = "item"
                path   = "value"
            }
            fieldMappings = @(
                @{ target = "durum"; source = "static"; value = "zimmetli" }
                @{ target = "zimmetliPersonelId"; source = "field"; path = "fields.personelId" }
                @{ target = "zimmetRef"; source = "field"; path = "key" }
            )
        }
    )
}
$ruleOc1Id = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_rules" -Filter "name:eq:$ruleOc1Name" -Label "OC-1 rule" -Body $ruleOc1Body
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_rules/$ruleOc1Id" -Body $ruleOc1Body | Out-Null
Write-Host "  OK: $ruleOc1Name ($ruleOc1Id)" -ForegroundColor Green

# --- OC-2: ZIM receive_return → updateDatasetRows (demirbas depoda) ---
Write-Host "`n[Rules] ZIM updateDatasetRows (OC-2)..." -ForegroundColor Yellow
$ruleOc2Name = "$tagZim — Iadede demirbas depoya don"
$ruleOc2Body = @{
    name           = $ruleOc2Name
    workspaceId    = $wsZimId
    ruleType       = "automation"
    trigger        = "WorkItemTransitioned"
    isActive       = $true
    priority       = 100
    typeId         = $typeZimmetIadeId
    transitionKey  = "receive_return"
    actions        = @(
        @{
            type         = "updateDatasetRows"
            dataset      = "zimmet_demirbaslar"
            onError      = "failTransition"
            cardinality  = @{
                mode      = "expand"
                itemsFrom = "fields.demirbasId"
                itemAs    = "id"
            }
            targetId     = @{
                source = "item"
                path   = "value"
            }
            fieldMappings = @(
                @{ target = "durum"; source = "static"; value = "depoda" }
            )
            clearFields = @("zimmetliPersonelId", "zimmetRef")
        }
    )
}
$ruleOc2Id = Find-ZimmetOrCreate -Ctx $ctx -Collection "op_rules" -Filter "name:eq:$ruleOc2Name" -Label "OC-2 rule" -Body $ruleOc2Body
Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/op_rules/$ruleOc2Id" -Body $ruleOc2Body | Out-Null
Write-Host "  OK: $ruleOc2Name ($ruleOc2Id)" -ForegroundColor Green
$summary.rules = @{
    girCreateDemirbas         = $ruleAf1Id
    zimDeliverUpdateDemirbas  = $ruleOc1Id
    zimReturnUpdateDemirbas   = $ruleOc2Id
}

if ($ReloadMetadataCache) {
    Write-Host "`n[MO] metadata cache reload..." -ForegroundColor Yellow
    foreach ($wsId in @($wsGirId, $wsZimId)) {
        try {
            Invoke-MoPost -Uri "$MoBaseUrl/api/v1/workspaces/$wsId/metadata-cache/reload" -Body @{} | Out-Null
            Write-Host "  OK: workspace $wsId cache reload" -ForegroundColor Green
        }
        catch {
            Write-Host "  WARN: workspace $wsId cache reload — $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }
}

# --- Demo ---
if ($SeedDemo) {
    Write-Host "`n[Demo] Keeper kullanicilari + ornek WI..." -ForegroundColor Yellow
    if (-not (Test-Path $MasterIdsFile)) {
        throw "Master ID dosyasi yok. Once seed-zimmet-master-data.ps1 calistirin: $MasterIdsFile"
    }
    $mids = Get-Content $MasterIdsFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $productDellId = $mids.ids.urunler.'PRD-DELL-5520'
    $productMonId = $mids.ids.urunler.'PRD-DELL-U2722D'
    $depoId = $mids.ids.depolar.'DEP-ANA'
    $lokLapId = $mids.ids.lokasyonlar.'DEP-ANA/A-01'
    $dmbLap1 = $mids.ids.demirbaslar.'DMB-LAP-001'
    $dmbLap2 = $mids.ids.demirbaslar.'DMB-LAP-002'
    $dmbMon1 = $mids.ids.demirbaslar.'DMB-MON-001'

    $keeperUsers = @(Get-KeeperActiveUsers -Ctx $ctx -PageSize 25)
    if ($keeperUsers.Count -lt 2) { throw "Keeper'dan yeterli kullanici alinamadi (min 2)." }
    $user1 = $keeperUsers[0]
    $user2 = if ($keeperUsers.Count -gt 1) { $keeperUsers[1] } else { $keeperUsers[0] }
    $person1Id = [string]($(if ($user1.userId) { $user1.userId } elseif ($user1.id) { $user1.id } else { "" }))
    $person2Id = [string]($(if ($user2.userId) { $user2.userId } elseif ($user2.id) { $user2.id } else { "" }))
    $person1Name = [string]($(if ($user1.username) { $user1.username } elseif ($user1.userName) { $user1.userName } elseif ($user1.email) { $user1.email } else { $person1Id }))
    $person2Name = [string]($(if ($user2.username) { $user2.username } elseif ($user2.userName) { $user2.userName } elseif ($user2.email) { $user2.email } else { $person2Id }))
    Write-Host "  Personel 1: $person1Name ($person1Id)" -ForegroundColor Gray
    Write-Host "  Personel 2: $person2Name ($person2Id)" -ForegroundColor Gray

    $today = (Get-Date).ToUniversalTime().ToString("o")

    # GIR demo — kapali giris kaydi
    $girBody = @{
        workspaceId = $wsGirId
        typeId      = $typeGirisId
        boardId     = $boardGirId
        title       = "Demo — Laptop ve monitor depo girisi"
        fields      = @{
            priorityId    = $prioNormalId
            katalogUrunId = $productDellId
            miktar        = 2
            depoId        = $depoId
            lokasyonId    = $lokLapId
            faturaNo      = "FTR-DEMO-2026-001"
            girisTarihi   = $today
            kaynak        = "manuel"
        }
    }
    $girWi = Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items" -Body $girBody
    $girWiId = $girWi.workItem.id
    $girKey = $girWi.workItem.key
    Write-Host "  OK: $girKey olusturuldu" -ForegroundColor Green
    foreach ($tk in @("submit", "receive", "stock")) {
        Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$girWiId/transitions/$tk" -Body "{}" | Out-Null
    }
    Write-Host "  OK: $girKey kapandi (AF-1 createDatasetRows tetiklendi)" -ForegroundColor Green

    # AF-1 uretilen demirbaslari dogrula (girisRef = GIR key)
    try {
        $createdDemirbas = Invoke-ZimmetDg -Ctx $ctx -Method GET -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_demirbaslar?filter=$([uri]::EscapeDataString("girisRef:eq:$girKey"))&limit=10"
        $createdCount = @($createdDemirbas).Count
        Write-Host "  OK: AF-1 demirbas sayisi=$createdCount (beklenen 2)" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARN: AF-1 demirbas dogrulama — $($_.Exception.Message)" -ForegroundColor DarkYellow
    }

    # ZIM-1 — laptop personel 1
    $zim1Body = @{
        workspaceId = $wsZimId
        typeId      = $typeZimmetVermeId
        boardId     = $boardZimId
        title       = "Demo zimmet — laptop ($person1Name)"
        fields      = @{
            priorityId     = $prioNormalId
            demirbasIds    = @($dmbLap1)
            personelId     = $person1Id
            departman      = "Uretim"
            teslimDurumu   = "yeni"
            zimmetNotu     = "Demo — yeni personel laptop zimmeti"
        }
    }
    $zim1 = Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items" -Body $zim1Body
    $zim1Id = $zim1.workItem.id
    $zim1Key = $zim1.workItem.key
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim1Id/transitions/request" -Body "{}" | Out-Null
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim1Id/transitions/approve" -Body "{}" | Out-Null
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim1Id/transitions/deliver" -Body (@{
        fields = @{ teslimTarihi = $today; teslimDurumu = "iyi" }
    }) | Out-Null
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim1Id/transitions/close" -Body "{}" | Out-Null
    Write-Host "  OK: $zim1Key -> kapali (laptop -> $person1Name)" -ForegroundColor Green

    Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_demirbaslar/$dmbLap1" -Body @{
        durum               = "zimmetli"
        zimmetliPersonelId  = $person1Id
        zimmetRef           = $zim1Key
        depoId              = $null
        lokasyonId          = $null
    } | Out-Null

    # ZIM-2 — monitor personel 2 (aktif birak)
    $zim2Body = @{
        workspaceId = $wsZimId
        typeId      = $typeZimmetVermeId
        boardId     = $boardZimId
        title       = "Demo zimmet — monitor ($person2Name)"
        fields      = @{
            priorityId   = $prioNormalId
            demirbasIds  = @($dmbMon1)
            personelId   = $person2Id
            departman    = "Kalite"
            teslimDurumu = "yeni"
        }
    }
    $zim2 = Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items" -Body $zim2Body
    $zim2Id = $zim2.workItem.id
    $zim2Key = $zim2.workItem.key
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim2Id/transitions/request" -Body "{}" | Out-Null
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim2Id/transitions/approve" -Body "{}" | Out-Null
    Invoke-MoPost -Uri "$MoBaseUrl/api/v1/work-items/$zim2Id/transitions/deliver" -Body (@{
        fields = @{ teslimTarihi = $today; teslimDurumu = "iyi" }
    }) | Out-Null
    Write-Host "  OK: $zim2Key -> Aktif (monitor -> $person2Name)" -ForegroundColor Green

    Invoke-ZimmetDg -Ctx $ctx -Method PUT -Uri "$($ctx.BaseUrl)$($ctx.DataPath)/zimmet_demirbaslar/$dmbMon1" -Body @{
        durum              = "zimmetli"
        zimmetliPersonelId = $person2Id
        zimmetRef          = $zim2Key
        depoId             = $null
        lokasyonId         = $null
    } | Out-Null

    $summary.demo = @{
        keeperUsers = @(
            @{ id = $person1Id; name = $person1Name },
            @{ id = $person2Id; name = $person2Name }
        )
        gir       = @{ id = $girWiId; key = $girKey }
        zimmet1   = @{ id = $zim1Id; key = $zim1Key; demirbas = "DMB-LAP-001"; status = "closed" }
        zimmet2   = @{ id = $zim2Id; key = $zim2Key; demirbas = "DMB-MON-001"; status = "active" }
        depoda    = @("DMB-LAP-002", "DMB-LAP-003", "DMB-BAG-001")
    }
}

$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "`nOC seed ozeti: $OutputFile" -ForegroundColor Cyan
Write-Host "Tamamlandi." -ForegroundColor Green
Write-Host "OC: /apps/operation-core/workspace — '$wsGirName', '$wsZimName'" -ForegroundColor Cyan
