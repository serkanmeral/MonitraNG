# Dashboard ve Widget Seed Script
# DataGateway uzerinden @widget_categories, @widgets, @dashboards dataset'lerini (yoksa) olusturur;
# "Monitoring" kategorisi ve ornek monitoring widget'lari + "Tren ozet" dashboard ekler.
#
# Onkosul: load-token.ps1 (get-token.ps1 ile token alinmis olmali).
# Ref: docs/content/monitoring_plans/DASHBOARD_WIDGET_PLAN.md (Bolum 8)

param(
    [string]$BaseUrl = "https://localhost:5040",
    [switch]$UseGateway = $true
)
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$dataPath     = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"
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
$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a,$b,$c,$d) $true }

# DataGateway POST yaniti { "data": { "__dataId": "...", ... } } veya { "Data": ... } doner; dokumani cikartir.
function Unwrap-DataResponse {
    param([object]$Response)
    if (-not $Response) { return $null }
    if ($Response.data) { return $Response.data }
    if ($Response.Data) { return $Response.Data }
    return $Response
}

# POST bir kayit; olusturulan dokumani döndürür (__dataId almak icin). Wrapper varsa acar.
function Invoke-PostAndReturn {
    param([string]$DatasetName, [object]$Record)
    $uri = "$BaseUrl$dataPath/$DatasetName"
    $body = $Record | ConvertTo-Json -Depth 20 -Compress
    if ($useCurl) {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
        $output = & curl.exe -s -k -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
        Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        $json = $output.Trim()
        $response = $null
        if ($json.StartsWith("[")) { $arr = $json | ConvertFrom-Json; if ($arr.Count -gt 0) { $response = $arr[0] } }
        elseif ($json.StartsWith("{")) { $response = $json | ConvertFrom-Json }
        return (Unwrap-DataResponse $response)
    }
    $response = Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body $body
    if ($response -is [array] -and $response.Count -gt 0) { $response = $response[0] }
    return (Unwrap-DataResponse $response)
}

# GET tek sayfa; ilk elemani veya null
function Invoke-GetFirst {
    param([string]$DatasetName, [string]$Filter)
    $q = "limit=1"; if ($Filter) { $q += "&filter=$([System.Web.HttpUtility]::UrlEncode($Filter))" }
    $uri = "$BaseUrl$dataPath/$DatasetName`?$q"
    if ($useCurl) {
        $output = & curl.exe -s -k -H "Authorization: Bearer $token" $uri 2>&1 | Out-String
        $json = $output.Trim()
        if (-not $json -or $json -eq "[]") { return $null }
        $arr = $json | ConvertFrom-Json
        if ($arr.Count -gt 0) { return $arr[0] }; return $null
    }
    $response = Invoke-RestMethod -Uri $uri -Method GET -Headers $headers
    if ($response -is [array] -and $response.Count -gt 0) { return $response[0] }
    return $null
}

# Dokumandan __dataId / DataId / dataId cikart (JSON serilestirme farklari icin)
function Get-DataId {
    param([object]$Doc)
    if (-not $Doc) { return $null }
    if ($Doc.PSObject.Properties['__dataId']) { return $Doc.__dataId }
    if ($Doc.PSObject.Properties['DataId']) { return $Doc.DataId }
    if ($Doc.PSObject.Properties['dataId']) { return $Doc.dataId }
    return $null
}

# Dataset olustur (409/mevcut ise basarili say)
function Invoke-CreateDataset {
    param([string]$Name, [object]$Schema)
    $uri = "$BaseUrl$datasetsPath"
    $body = $Schema | ConvertTo-Json -Depth 15 -Compress
    try {
        if ($useCurl) {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") { Write-Host "  $Name olusturuldu" -ForegroundColor Green; return $true }
            if ($httpCode -eq "409" -or ($responseBody -match "already exists|zaten")) { Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow; return $true }
            Write-Host "  HATA: HTTP $httpCode" -ForegroundColor Red
            return $false
        }
        $null = Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body $body
        Write-Host "  $Name olusturuldu" -ForegroundColor Green
        return $true
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($errMsg -match "already exists|zaten")) { Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow; return $true }
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Write-Host "`nDashboard & Widget Seed - Basliyor...`n" -ForegroundColor Cyan

# 1) Dataset'ler (kucuk schema - sadece zorunlu alanlar)
$schemaCategories = @{
    name         = "@widget_categories"
    description  = "Widget kategorileri"
    forceSchema  = $true
    logging      = "none"
    publishMode  = "none"
    fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Kategori Adi"; mandatory = $true; unique = $true },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false },
        @{ fieldType = "text"; name = "icon"; title = "Icon"; mandatory = $false },
        @{ fieldType = "text"; name = "color"; title = "Renk"; mandatory = $false },
        @{ fieldType = "number"; name = "order"; title = "Sira"; mandatory = $false; defaultValue = 0 },
        @{ fieldType = "bool"; name = "isActive"; title = "Aktif"; mandatory = $true; defaultValue = $true }
    )
    indexList    = @( @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true } )
}
Invoke-CreateDataset "@widget_categories" $schemaCategories | Out-Null

$schemaWidgets = @{
    name         = "@widgets"
    description  = "Widget tanimlari"
    forceSchema  = $true
    logging      = "none"
    publishMode  = "none"
    fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Widget Adi"; mandatory = $true; unique = $true },
        @{ fieldType = "text"; name = "title"; title = "Baslik"; mandatory = $true },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false },
        @{ fieldType = "relation"; name = "category"; title = "Kategori"; mandatory = $true; relationDataset = "@widget_categories" },
        @{ fieldType = "text"; name = "type"; title = "Tip"; mandatory = $true },
        @{ fieldType = "object"; name = "dataSource"; title = "Data Source"; mandatory = $true },
        @{ fieldType = "object"; name = "layout"; title = "Layout"; mandatory = $false },
        @{ fieldType = "object"; name = "style"; title = "Stil"; mandatory = $false },
        @{ fieldType = "object"; name = "config"; title = "Config"; mandatory = $false },
        @{ fieldType = "bool"; name = "isActive"; title = "Aktif"; mandatory = $true; defaultValue = $true },
        @{ fieldType = "number"; name = "order"; title = "Sira"; mandatory = $false; defaultValue = 0 }
    )
    indexList    = @( @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true } )
}
Invoke-CreateDataset "@widgets" $schemaWidgets | Out-Null

$schemaDashboards = @{
    name         = "@dashboards"
    description  = "Dashboard tanimlari"
    forceSchema  = $true
    logging      = "none"
    publishMode  = "none"
    fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Dashboard Adi"; mandatory = $true; unique = $true },
        @{ fieldType = "text"; name = "title"; title = "Baslik"; mandatory = $true },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false },
        @{ fieldType = "text"; name = "slug"; title = "Slug"; mandatory = $false },
        @{ fieldType = "object"; name = "layout"; title = "Layout"; mandatory = $true },
        @{ fieldType = "bool"; name = "isDefault"; title = "Varsayilan"; mandatory = $false; defaultValue = $false },
        @{ fieldType = "bool"; name = "isActive"; title = "Aktif"; mandatory = $true; defaultValue = $true },
        @{ fieldType = "number"; name = "order"; title = "Sira"; mandatory = $false; defaultValue = 0 }
    )
    indexList    = @( @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true }, @{ name = "idx_slug"; fields = @{ slug = 1 } } )
}
Invoke-CreateDataset "@dashboards" $schemaDashboards | Out-Null

# 2) Monitoring kategorisi
Write-Host "`nMonitoring kategorisi" -ForegroundColor Yellow
$existingCat = Invoke-GetFirst -DatasetName "@widget_categories" -Filter "name:eq:Monitoring"
$categoryId = $null
if ($existingCat) {
    $categoryId = Get-DataId $existingCat
    Write-Host "  Monitoring kategorisi zaten mevcut" -ForegroundColor Green
} else {
    $catRecord = @{
        name        = "Monitoring"
        description = "Monitoring widget'lari - asset ve metrik bazli izleme"
        icon        = "mdi-chart-dashboard"
        color       = "primary"
        order       = 10
        isActive    = $true
    }
    $created = Invoke-PostAndReturn -DatasetName "@widget_categories" -Record $catRecord
    $categoryId = Get-DataId $created
    Write-Host "  Monitoring kategorisi olusturuldu" -ForegroundColor Green
}
if (-not $categoryId) { Write-Host "  HATA: Kategori id alinamadi" -ForegroundColor Red; exit 1 }

# 3) Seed widget'lar (assetScope manual, assetIds bos - kullanici sonra duzenleyip secsin)
$baseFilter = "meta.collectibleCode:eq:"
$baseDataSource = @{
    type       = "data"
    dataset    = "mon_metrics"
    getMethod  = "default"
    default    = @{ sort = "-timestamp"; limit = 500 }
}
$baseConfig = @{
    monitoring           = $true
    assetScope           = "manual"
    assetIds             = @()
    timeRangeMinutes     = 60
    limit                = 500
    refreshIntervalSeconds = 60
}

$widgetIds = @{}
$widgetList = @(
    @{
        name  = "seed_card_speed"
        title = "Hiz (km/h)"
        type  = "card"
        dataSource = @{
            type      = "data"
            dataset  = "mon_metrics"
            getMethod = "default"
            default  = @{ filter = $baseFilter + "speed"; sort = "-timestamp"; limit = 1 }
        }
        config = ($baseConfig.Clone() + @{ collectibleCode = "speed"; valueField = "value"; format = "text"; cardDisplay = "default"; icon = "mdi-speedometer"; color = "primary" })
    },
    @{
        name  = "seed_card_engine_temp"
        title = "Motor sicaklik (°C)"
        type  = "card"
        dataSource = @{
            type      = "data"
            dataset  = "mon_metrics"
            getMethod = "default"
            default  = @{ filter = $baseFilter + "sensors.engineTempC"; sort = "-timestamp"; limit = 1 }
        }
        config = ($baseConfig.Clone() + @{ collectibleCode = "sensors.engineTempC"; valueField = "value"; format = "text"; cardDisplay = "default"; icon = "mdi-thermometer"; color = "primary" })
    },
    @{
        name  = "seed_card_oil_pressure"
        title = "Yag basinci (bar)"
        type  = "card"
        dataSource = @{
            type      = "data"
            dataset  = "mon_metrics"
            getMethod = "default"
            default  = @{ filter = $baseFilter + "sensors.oilPressureBar"; sort = "-timestamp"; limit = 1 }
        }
        config = ($baseConfig.Clone() + @{ collectibleCode = "sensors.oilPressureBar"; valueField = "value"; format = "text"; cardDisplay = "default"; icon = "mdi-gauge"; color = "primary" })
    },
    @{
        name  = "seed_map_trains"
        title = "Tren konumlari"
        type  = "map"
        dataSource = @{ type = "data"; dataset = "mon_metrics"; getMethod = "default"; default = @{ limit = 1 } }
        config = @{
            monitoring = $true
            map        = $true
            assetScope = "byType"
            assetIds   = $null
            refreshIntervalSeconds = 60
            defaultZoom = 6
            defaultBaseLayer = "osm"
            defaultLayerVisibility = @{}
        }
    },
    @{
        name  = "seed_chart_speed"
        title = "Hiz - Zaman"
        type  = "chart"
        dataSource = @{
            type     = "data"
            dataset = "mon_metrics"
            getMethod = "default"
            default  = @{ filter = $baseFilter + "speed"; sort = "-timestamp"; limit = 500 }
        }
        config = ($baseConfig.Clone() + @{
            collectibleCode = "speed"
            type = "line"
            height = 300
            xAxis = @{ field = "timestamp"; label = "Zaman" }
            yAxis = @{ field = "value"; label = "Hiz (km/h)" }
        })
    },
    @{
        name  = "seed_gauge_engine_temp"
        title = "Motor sicaklik"
        type  = "gauge"
        dataSource = @{
            type     = "data"
            dataset = "mon_metrics"
            getMethod = "default"
            default  = @{ filter = $baseFilter + "sensors.engineTempC"; sort = "-timestamp"; limit = 1 }
        }
        config = ($baseConfig.Clone() + @{
            collectibleCode = "sensors.engineTempC"
            valueField = "value"
            min = 0
            max = 120
            unit = "°C"
            thresholds = @(
                @{ from = 0; to = 80; color = "success" }
                @{ from = 80; to = 100; color = "warning" }
                @{ from = 100; to = 120; color = "error" }
            )
        })
    }
)

Write-Host "`nSeed widget'lar olusturuluyor" -ForegroundColor Yellow
foreach ($w in $widgetList) {
    $rec = @{
        name        = $w.name
        title       = $w.title
        description = "Ornek monitoring widget (seed)"
        category    = $categoryId
        type        = $w.type
        dataSource  = $w.dataSource
        config      = $w.config
        isActive    = $true
        order       = 0
    }
    try {
        $created = Invoke-PostAndReturn -DatasetName "@widgets" -Record $rec
        $id = Get-DataId $created
        if ($id) { $widgetIds[$w.name] = $id; Write-Host "  $($w.name) -> $id" -ForegroundColor Green }
        else { Write-Host "  $($w.name) yanit id yok" -ForegroundColor Yellow }
    } catch {
        if ($_.Exception.Message -match "409|duplicate|unique|zaten") {
            $existing = Invoke-GetFirst -DatasetName "@widgets" -Filter "name:eq:$($w.name)"
            $id = Get-DataId $existing
            if ($id) { $widgetIds[$w.name] = $id; Write-Host "  $($w.name) zaten mevcut -> $id" -ForegroundColor Yellow }
            else { Write-Host "  $($w.name) zaten mevcut ama id alinamadi" -ForegroundColor Yellow }
        } else { Write-Host "  $($w.name) HATA: $($_.Exception.Message)" -ForegroundColor Red }
    }
}

# 4) Dashboard "Tren ozet"
$idCard1 = $widgetIds["seed_card_speed"]
$idCard2 = $widgetIds["seed_card_engine_temp"]
$idCard3 = $widgetIds["seed_card_oil_pressure"]
$idMap   = $widgetIds["seed_map_trains"]
$idChart = $widgetIds["seed_chart_speed"]
$idGauge = $widgetIds["seed_gauge_engine_temp"]

if (-not $idCard1 -or -not $idCard2 -or -not $idCard3 -or -not $idMap -or -not $idChart -or -not $idGauge) {
    Write-Host "`nUYARI: Bazi widget id'leri eksik; dashboard layout eksik olabilir." -ForegroundColor Yellow
}

$layout = @{
    type = "rows"
    rows = @(
        @{ cols = @(
            @{ widgetId = $idCard1; span = 4 }
            @{ widgetId = $idCard2; span = 4 }
            @{ widgetId = $idCard3; span = 4 }
        )}
        @{ cols = @( @{ widgetId = $idMap; span = 12 } ) }
        @{ cols = @(
            @{ widgetId = $idChart; span = 6 }
            @{ widgetId = $idGauge; span = 6 }
        )}
    )
}
$dashboardRecord = @{
    name        = "tren-ozet"
    title       = "Tren ozet"
    description = "Ornek dashboard: tek tren veya trenler icin hiz, motor sicaklik, yag basinci, harita, grafik ve gauge."
    slug        = "tren-ozet"
    layout      = $layout
    isDefault   = $false
    isActive    = $true
    order       = 0
}

# Dashboard: varsa PUT ile layout guncelle (widget id'leri düzgün olsun), yoksa POST ile olustur
Write-Host "`nDashboard 'Tren ozet'" -ForegroundColor Yellow
$existingDash = Invoke-GetFirst -DatasetName "@dashboards" -Filter "slug:eq:tren-ozet"
if ($existingDash -and $idCard1 -and $idCard2 -and $idCard3 -and $idMap -and $idChart -and $idGauge) {
    $dashId = Get-DataId $existingDash
    if ($dashId) {
        $uri = "$BaseUrl$dataPath/@dashboards/$([System.Web.HttpUtility]::UrlEncode($dashId))"
        $putBody = @{ layout = $layout } | ConvertTo-Json -Depth 15 -Compress
        try {
            if ($useCurl) {
                $bodyFile = [System.IO.Path]::GetTempFileName()
                $putBody | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
                $null = & curl.exe -s -k -X PUT -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1
                Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            } else { $null = Invoke-RestMethod -Uri $uri -Method PUT -Headers $headers -Body $putBody }
            Write-Host "  Dashboard layout guncellendi (widget id'leri eklendi): $dashId" -ForegroundColor Green
        } catch { Write-Host "  Dashboard guncelleme HATA: $($_.Exception.Message)" -ForegroundColor Red }
    }
} elseif ($existingDash) {
    Write-Host "  Dashboard 'tren-ozet' zaten mevcut; widget id'leri eksik oldugu icin guncelleme atlandi. Script'i tekrar calistirin." -ForegroundColor Yellow
} else {
    try {
        $dashCreated = Invoke-PostAndReturn -DatasetName "@dashboards" -Record $dashboardRecord
        $dashId = Get-DataId $dashCreated
        Write-Host "  Dashboard olusturuldu: $dashId" -ForegroundColor Green
    } catch {
        if ($_.Exception.Message -match "409|duplicate|unique|zaten") { Write-Host "  Dashboard 'tren-ozet' zaten mevcut. Layout bos widget id ile kayitli olabilir; UI'dan silip script'i tekrar calistirin." -ForegroundColor Yellow }
        else { Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red }
    }
}

Write-Host "`nSeed tamamlandi. UI'da /dashboards/tren-ozet ile goruntuleyebilirsiniz." -ForegroundColor Green
Write-Host "Widget'lara asset secmek icin Monitoring > Widget'lar'dan ilgili widget'i duzenleyin." -ForegroundColor Gray
Write-Host ""
