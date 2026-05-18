# HTTP Asset Type: Tren / Rota sensörleri
# Verilen JSON response yapisina uygun collectible sablonu ve asset type olusturur.
#
# Response ornegi:
# { "trainId": "T1", "routeId": "ANK-IST", "lat": 40.32, "lon": 31.38, "speed": 2191.3, "heading": 107.9,
#   "timestamp": "2026-03-08T12:40:31.9167078Z", "sensors": { "engineTempC": 92.5, "oilPressureBar": 5.05, ... } }
#
# Kullanim: .\add-train-http-asset-type.ps1
#         .\add-train-http-asset-type.ps1 -BaseUrl "http://localhost:5040"   # SSL hatasinda HTTP dene
# Onkosul: load-token.ps1, setup-monitoring-datasets.ps1 (mon_asset_type_family, mon_asset_types, mon_collectible_templates)

param(
    [string]$BaseUrl = "https://localhost:5040",
    [switch]$UseGateway = $true
)

# SSL: localhost/self-signed sertifika hatasini atla (sadece test/yerel ortam icin)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "..\MngDataGateway\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. get-token.ps1 ile token alin." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
# PowerShell 6+ (Core): Invoke-RestMethod -SkipCertificateCheck (HTTPS icin)
if (($PSEdition -eq "Core" -or $PSVersionTable.PSVersion.Major -ge 6) -and $BaseUrl.StartsWith("https://", "OrdinalIgnoreCase")) {
    try { $irmParams.SkipCertificateCheck = $true } catch { }
}

function Invoke-DgPost {
    param([string]$Collection, [object]$Body)
    $uri = "$BaseUrl$dataPath/$Collection"
    $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 15 -Compress }
    try {
        return Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body $json @irmParams
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
        return Invoke-RestMethod -Uri $uri -Method GET -Headers $headers @irmParams
    } catch {
        Write-Host "  DG GET $Collection hata: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Get-FirstItem {
    param($Response)
    if (-not $Response) { return $null }
    $items = $Response.data; if (-not $items) { $items = $Response.items }; if (-not $items) { $items = $Response }
    if ($items -is [Array] -and $items.Count -gt 0) { return $items[0] }
    return $null
}

# Engine HTTP collector: code = root key veya noktali path (ornegin sensors.engineTempC)
$collectibles = @(
    @{ code = "trainId";   name = "Tren ID";           data_type = "string"; path = "$.trainId" },
    @{ code = "routeId";   name = "Rota ID";           data_type = "string"; path = "$.routeId" },
    @{ code = "lat";       name = "Enlem";             data_type = "number"; path = "$.lat" },
    @{ code = "lon";       name = "Boylam";            data_type = "number"; path = "$.lon" },
    @{ code = "speed";     name = "Hiz";               data_type = "number"; path = "$.speed" },
    @{ code = "heading";   name = "Yon (derece)";      data_type = "number"; path = "$.heading" },
    @{ code = "timestamp"; name = "Zaman damgasi";     data_type = "string"; path = "$.timestamp" },
    @{ code = "sensors.engineTempC";       name = "Motor sicakligi (°C)";     data_type = "number"; path = "$.sensors.engineTempC" },
    @{ code = "sensors.oilPressureBar";    name = "Yag basinci (bar)";        data_type = "number"; path = "$.sensors.oilPressureBar" },
    @{ code = "sensors.coolantTempC";     name = "Devirdaim sicakligi (°C)"; data_type = "number"; path = "$.sensors.coolantTempC" },
    @{ code = "sensors.batteryVoltageV";   name = "Aku voltaji (V)";         data_type = "number"; path = "$.sensors.batteryVoltageV" },
    @{ code = "sensors.brakePipePressureBar"; name = "Fren borusu basinci (bar)"; data_type = "number"; path = "$.sensors.brakePipePressureBar" },
    @{ code = "sensors.cabTempC";          name = "Kabin sicakligi (°C)";      data_type = "number"; path = "$.sensors.cabTempC" },
    @{ code = "sensors.vibrationMs2";      name = "Titresim (m/s²)";          data_type = "number"; path = "$.sensors.vibrationMs2" },
    @{ code = "sensors.doorClosed";        name = "Kapi kapali";             data_type = "string"; path = "$.sensors.doorClosed" }
)

Write-Host "`n--- HTTP Asset Type: Tren / Rota ---`n" -ForegroundColor Cyan

# 1) Collectible sablonu (HTTP - Tren Rota Sensörleri)
Write-Host "[1] mon_collectible_templates (HTTP - Tren Rota Sensörleri)..." -ForegroundColor Yellow
$templateBody = @{
    name               = "HTTP - Tren Rota Sensörleri"
    collection_method  = "HTTP"
    description        = "Tren/rota konum ve sensör JSON yaniti. trainId, routeId, lat, lon, speed, heading, timestamp, sensors.*"
    collectibles       = $collectibles
}
$templateResp = Invoke-DgPost "mon_collectible_templates" $templateBody
$templateId = Get-DataId $templateResp
if ($templateId) {
    Write-Host "  Sablon olusturuldu (veya zaten mevcut)." -ForegroundColor Green
} else {
    $existing = Invoke-DgGet "mon_collectible_templates" -Filter "name:eq:HTTP - Tren Rota Sensörleri" -Limit 5
    $first = Get-FirstItem $existing
    if ($first) { Write-Host "  Sablon zaten mevcut." -ForegroundColor Gray }
    else { Write-Host "  UYARI: Sablon eklenemedi; asset type yine de deneniyor." -ForegroundColor Yellow }
}
Write-Host ""

# 2) Aile (Raylı Sistem)
Write-Host "[2] mon_asset_type_family (Raylı Sistem)..." -ForegroundColor Yellow
$familyId = $null
$existing = Invoke-DgGet "mon_asset_type_family" -Filter "name:eq:Raylı Sistem" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $familyId = $first.__dataId
    Write-Host "  Mevcut: Raylı Sistem (ID: $familyId)" -ForegroundColor Gray
} else {
    $body = @{ name = "Raylı Sistem"; code = "railway"; description = "Tren, raylı sistem sensörleri" }
    $r = Invoke-DgPost "mon_asset_type_family" $body
    $familyId = Get-DataId $r
    if ($familyId) { Write-Host "  Olusturuldu: Raylı Sistem (ID: $familyId)" -ForegroundColor Green }
}
if (-not $familyId) {
    Write-Host "  HATA: Aile olusturulamadi." -ForegroundColor Red
    exit 1
}
Write-Host ""

# 3) Asset type (Tren / Rota - HTTP)
Write-Host "[3] mon_asset_types (Tren / Rota)..." -ForegroundColor Yellow
$assetTypeId = $null
$existing = Invoke-DgGet "mon_asset_types" -Filter "name:eq:Tren / Rota" -Limit 5
$first = Get-FirstItem $existing
if ($first -and $first.__dataId) {
    $assetTypeId = $first.__dataId
    Write-Host "  Mevcut: Tren / Rota (ID: $assetTypeId)" -ForegroundColor Gray
} else {
    $body = @{
        name               = "Tren / Rota"
        family             = $familyId
        collection_method  = "HTTP"
        description        = "Tren konum ve sensör verisi (HTTP JSON: trainId, routeId, lat, lon, speed, heading, sensors.*)"
        collectibles       = $collectibles
    }
    $r = Invoke-DgPost "mon_asset_types" $body
    $assetTypeId = Get-DataId $r
    if ($assetTypeId) { Write-Host "  Olusturuldu: Tren / Rota (ID: $assetTypeId)" -ForegroundColor Green }
}
if (-not $assetTypeId) {
    Write-Host "  HATA: Asset type olusturulamadi." -ForegroundColor Red
    exit 1
}

Write-Host "`nBitti. Asset Type Tanimlari sayfasindan 'Tren / Rota' tipini gorebilir; Organizasyon'da bu tip ile HTTP asset ekleyebilirsiniz." -ForegroundColor Green
Write-Host "HTTP endpoint Base URL (ornegin tren API adresi) ve gerekirse Auth (None/Basic) asset baglanti bilgisinde girilir.`n" -ForegroundColor Gray
