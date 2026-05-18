# MngReactor Monitoring Test Verisi Olusturma
# DG uzerinden mon_* koleksiyonlarina test kayitlari ekler.
#
# Onkosullar:
#   1. setup-monitoring-datasets.ps1 calistirilmis olmali (dataset semalari mevcut)
#   2. Domain Init yapilmis veya script otomatik yapar (mon_schedules, mon_collection_periods)
#   3. Token: load-token.ps1 (domain claim iceren JWT)
#
# Olusturulan zincir:
#   mon_asset_type_family -> mon_asset_types -> mon_items -> mon_assets
#   mon_collection_periods, mon_schedules (Domain Init)
#   mon_engines -> mon_agents (asset_configs ile)
#
# Ref: docs/content/monitoring_plans/MNGREACTOR_TEST_PLAN.md
#      docs/content/monitoring_plans/MONITORING_ASSET_DATASETS.md

param(
    [string]$BaseUrl = "https://localhost:5040",   # Gateway: 5040 | DG direkt: https://localhost:5010
    [string]$ReactorBaseUrl = "http://localhost:15010",  # MngReactor API (Domain Init icin)
    [string]$Domain = "meral",                     # Token'daki domain (mng_{domain} DB)
    [switch]$UseGateway = $true,                   # true: /data/api/v1/data | false: /api/v1/data
    [switch]$RunDomainInit = $true                 # mon_schedules, mon_collection_periods olustur
)

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

# Token
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "auth\load-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Hata: load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Hata: Token alinamadi. get-token.ps1 ile token alin (domain claim gerekli)." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
    $irmParams.SkipCertificateCheck = $true
}

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection"
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 15 -Compress }
    try {
        $r = Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body $json @irmParams
        return $r
    } catch {
        Write-Host "  DG POST $Collection hata: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        return $null
    }
}

function Invoke-DgGet {
    param([string]$Collection, [string]$Filter = "", [int]$Limit = 100)
    $uri = "$BaseUrl$dataPath/$Collection`?limit=$Limit"
    if (-not [string]::IsNullOrEmpty($Filter)) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    try {
        $r = Invoke-RestMethod -Uri $uri -Method GET -Headers $headers @irmParams
        return $r
    } catch {
        Write-Host "  DG GET $Collection hata: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data
    if (-not $d) { $d = $Response.Data }
    if (-not $d) { $d = $Response }
    $id = $d.__dataId
    if (-not $id) { $id = $d.dataId }
    if (-not $id) { $id = $d.DataId }
    return $id
}

function Get-FirstItem {
    param($Response)
    if (-not $Response) { return $null }
    $items = $Response.data
    if (-not $items) { $items = $Response.items }
    if (-not $items) { $items = $Response }
    if ($items -is [Array] -and $items.Count -gt 0) { return $items[0] }
    return $null
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "MngReactor Monitoring Test Verisi" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 0. Domain Init (opsiyonel) ---
if ($RunDomainInit) {
    Write-Host "[0] Domain Init (mon_schedules, mon_collection_periods)..." -ForegroundColor Yellow
    try {
        $initUrl = "$ReactorBaseUrl/api/v1/admin/domain/$Domain/init"
        $initParams = @{ Uri = $initUrl; Method = "POST"; Headers = $headers }
        if ($irmParams.SkipCertificateCheck) { $initParams.SkipCertificateCheck = $true }
        Invoke-RestMethod @initParams | Out-Null
        Write-Host "  OK: Domain init tamamlandi" -ForegroundColor Green
    } catch {
        Write-Host "  UYARI: Domain init basarisiz (Reactor calisiyor mu?): $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  mon_schedules ve mon_collection_periods manuel veya zaten mevcut olmali." -ForegroundColor Gray
    }
    Write-Host ""
}

# --- 1. mon_asset_type_family ---
Write-Host "[1] mon_asset_type_family..." -ForegroundColor Yellow
$familyId = $null
$existing = Invoke-DgGet "mon_asset_type_family" -Filter "name:eq:Operating Systems" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $familyId = $first.__dataId
    Write-Host "  Mevcut: Operating Systems (ID: $familyId)" -ForegroundColor Gray
} else {
    $body = @{ name = "Operating Systems"; code = "operating_systems"; description = "Isletim sistemi tabanli host'lar" }
    $r = Invoke-DgPost "mon_asset_type_family" $body
    $familyId = Get-DataId $r
    if ($familyId) { Write-Host "  Olusturuldu: Operating Systems (ID: $familyId)" -ForegroundColor Green }
}
if (-not $familyId) {
    Write-Host "  HATA: mon_asset_type_family olusturulamadi. setup-monitoring-datasets.ps1 calistirildi mi?" -ForegroundColor Red
    exit 1
}
Write-Host ""

# --- 2. mon_asset_types ---
Write-Host "[2] mon_asset_types..." -ForegroundColor Yellow
$assetTypeId = $null
$existing = Invoke-DgGet "mon_asset_types" -Filter "name:eq:Linux Host" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $assetTypeId = $first.__dataId
    Write-Host "  Mevcut: Linux Host (ID: $assetTypeId)" -ForegroundColor Gray
} else {
    $body = @{
        name              = "Linux Host"
        family            = $familyId
        collection_method = "ssh"
        description       = "SSH ile Linux sunucu metrikleri"
        collectibles      = @(
            @{ code = "cpu"; enabled = $true },
            @{ code = "memory"; enabled = $true }
        )
    }
    $r = Invoke-DgPost "mon_asset_types" $body
    $assetTypeId = Get-DataId $r
    if ($assetTypeId) { Write-Host "  Olusturuldu: Linux Host (ID: $assetTypeId)" -ForegroundColor Green }
}
if (-not $assetTypeId) {
    Write-Host "  HATA: mon_asset_types olusturulamadi" -ForegroundColor Red
    exit 1
}
Write-Host ""

# --- 3. mon_items ---
Write-Host "[3] mon_items..." -ForegroundColor Yellow
$itemId = $null
$existing = Invoke-DgGet "mon_items" -Filter "name:eq:Test Sunucu" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $itemId = $first.__dataId
    Write-Host "  Mevcut: Test Sunucu (ID: $itemId)" -ForegroundColor Gray
} else {
    $body = @{ name = "Test Sunucu"; description = "Test icin root item"; kind = "server" }
    $r = Invoke-DgPost "mon_items" $body
    $itemId = Get-DataId $r
    if ($itemId) { Write-Host "  Olusturuldu: Test Sunucu (ID: $itemId)" -ForegroundColor Green }
}
if (-not $itemId) {
    Write-Host "  HATA: mon_items olusturulamadi" -ForegroundColor Red
    exit 1
}
Write-Host ""

# --- 4. mon_schedules, mon_collection_periods ---
Write-Host "[4] mon_schedules, mon_collection_periods..." -ForegroundColor Yellow
$scheduleId = $null
$periodId = $null

$schedResp = Invoke-DgGet "mon_schedules" -Filter "name:eq:Sürekli" -Limit 5
$schedFirst = Get-FirstItem $schedResp
if ($schedFirst -and $schedFirst.__dataId) {
    $scheduleId = $schedFirst.__dataId
    Write-Host "  Mevcut: mon_schedules 'Sürekli'" -ForegroundColor Gray
} else {
    $body = @{ __dataId = (New-Guid).ToString(); name = "Sürekli"; description = "7/24 izleme"; type = "always" }
    $r = Invoke-DgPost "mon_schedules" $body
    $scheduleId = Get-DataId $r
    if (-not $scheduleId) { $scheduleId = $body.__dataId }
    if ($scheduleId) { Write-Host "  Olusturuldu: mon_schedules 'Sürekli'" -ForegroundColor Green }
}

$periodResp = Invoke-DgGet "mon_collection_periods" -Filter "name:eq:1 dakika" -Limit 5
$periodFirst = Get-FirstItem $periodResp
if ($periodFirst -and $periodFirst.__dataId) {
    $periodId = $periodFirst.__dataId
    Write-Host "  Mevcut: mon_collection_periods '1 dakika'" -ForegroundColor Gray
} else {
    $body = @{ __dataId = (New-Guid).ToString(); name = "1 dakika"; description = "Her dakika toplama"; expression = "*/1 * * * *" }
    $r = Invoke-DgPost "mon_collection_periods" $body
    $periodId = Get-DataId $r
    if (-not $periodId) { $periodId = $body.__dataId }
    if ($periodId) { Write-Host "  Olusturuldu: mon_collection_periods '1 dakika'" -ForegroundColor Green }
}
Write-Host ""

# --- 5. mon_assets ---
Write-Host "[5] mon_assets..." -ForegroundColor Yellow
$assetId = $null
$existing = Invoke-DgGet "mon_assets" -Filter "name:eq:Test Linux Host" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $assetId = $first.__dataId
    Write-Host "  Mevcut: Test Linux Host (ID: $assetId)" -ForegroundColor Gray
} else {
    $body = @{
        name           = "Test Linux Host"
        type           = $assetTypeId
        itemId         = $itemId
        description    = "Test icin Linux asset"
        status         = "active"
        connection_info = @{
            endpoint = @{ host = "192.168.1.10"; port = 22 }
            auth     = @{ username = "monitor"; password = "test123" }
        }
    }
    $r = Invoke-DgPost "mon_assets" $body
    $assetId = Get-DataId $r
    if ($assetId) { Write-Host "  Olusturuldu: Test Linux Host (ID: $assetId)" -ForegroundColor Green }
}
if (-not $assetId) {
    Write-Host "  HATA: mon_assets olusturulamadi" -ForegroundColor Red
    exit 1
}
Write-Host ""

# --- 6. mon_engines ---
Write-Host "[6] mon_engines..." -ForegroundColor Yellow
$engineId = $null
$existing = Invoke-DgGet "mon_engines" -Filter "name:eq:Test Engine" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $engineId = $first.__dataId
    Write-Host "  Mevcut: Test Engine (ID: $engineId)" -ForegroundColor Gray
} else {
    $body = @{
        name                     = "Test Engine"
        description              = "MngReactor test icin"
        status                   = "active"
        username                 = "engine_test"
        password                 = "EnginePass123!"
        sendSchedule             = "0 */2 * * *"
        configSyncPeriodMinutes  = 10
    }
    $r = Invoke-DgPost "mon_engines" $body
    $engineId = Get-DataId $r
    if ($engineId) { Write-Host "  Olusturuldu: Test Engine (ID: $engineId)" -ForegroundColor Green }
}
if (-not $engineId) {
    Write-Host "  HATA: mon_engines olusturulamadi" -ForegroundColor Red
    exit 1
}
Write-Host ""

# --- 7. mon_agents ---
Write-Host "[7] mon_agents..." -ForegroundColor Yellow
$agentId = $null
$existing = Invoke-DgGet "mon_agents" -Filter "name:eq:Test Agent" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $agentId = $first.__dataId
    Write-Host "  Mevcut: Test Agent (ID: $agentId)" -ForegroundColor Gray
} else {
    $body = @{
        name            = "Test Agent"
        description     = "Test icin agent"
        status          = "active"
        engineId        = $engineId
        defaultPeriodId = $periodId
        defaultScheduleId = $scheduleId
        asset_configs   = @(
            @{ assetId = $assetId; active = $true }
        )
    }
    # null olan alanlari cikar
    $clean = @{}
    foreach ($k in $body.Keys) {
        if ($null -ne $body[$k]) { $clean[$k] = $body[$k] }
    }
    $r = Invoke-DgPost "mon_agents" $clean
    $agentId = Get-DataId $r
    if ($agentId) { Write-Host "  Olusturuldu: Test Agent (ID: $agentId)" -ForegroundColor Green }
}
if (-not $agentId) {
    Write-Host "  HATA: mon_agents olusturulamadi" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "Test verisi hazir!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Ozet:" -ForegroundColor Cyan
Write-Host "  Engine ID:  $engineId" -ForegroundColor White
Write-Host "  Agent ID:   $agentId" -ForegroundColor White
Write-Host "  Asset ID:   $assetId" -ForegroundColor White
Write-Host ""
Write-Host "Config Sync testi:" -ForegroundColor Gray
Write-Host "  .\test-config-sync.ps1 -EngineId '$engineId'" -ForegroundColor Gray
Write-Host ""
