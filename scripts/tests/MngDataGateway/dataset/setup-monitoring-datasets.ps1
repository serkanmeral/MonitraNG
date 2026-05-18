# Monitoring Datasets Setup Script (Faz 0)
# DG Gateway arkasinda: -BaseUrl "https://localhost:5040" -UseGateway (varsayilan)
# DG direkt (dev): -BaseUrl "http://localhost:5010" -UseGateway:$false
#
# Creates 11 datasets for MonitraNG Monitoring:
#   mon_asset_type_family, mon_asset_types, mon_collectible_templates, mon_http_auth_configs,
#   mon_items, mon_assets, mon_collection_periods, mon_schedules, mon_engines, mon_agents, mon_metrics
# mon_metrics: Reactor dogrudan MongoDB Time Series yazar; DG dataset tanimi ile sadece okuma yapilir
# After mon_collectible_templates: seeds 2 SNMP templates (PDU (MngSim), Router (MngSim)) from MngSim simulator OIDs.
#
# Ref: docs/content/monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md
#      docs/content/monitoring_plans/MONITORING_ASSET_DATASETS.md
#      docs/content/monitoring_plans/MONITORING_AGENT_ARCHITECTURE.md
#      docs/content/monitoring_plans/MONITORING_ENGINE_ARCHITECTURE.md

param(
    [string]$BaseUrl = "https://localhost:5040",  # Gateway: 5040 | DG direkt: http://localhost:5010
    [switch]$UseGateway = $true   # true: BaseUrl/data/api/v1/datasets (Gateway); false: BaseUrl/api/v1/datasets (DG direct)
)
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$dataPath     = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

# Token: load-token.ps1 -> get-token.ps1 (Keeper: domain, username, password)
#   get-token.ps1 varsayilanlari: KeeperBaseUrl=https://localhost:5040, Domain=meral, Username=meral_admin, Password=Admin123!
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = Get-Location }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadi! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi! get-token.ps1 ile token alin (domain claim gerekli)." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# HTTPS + curl.exe varsa SSL atlamak icin curl kullan (get-token.ps1 ile ayni cozum)
$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a,$b,$c,$d) $true }

function Invoke-CreateDataset {
    param([string]$Name, [object]$Schema, [string]$Step)
    $uri = "$BaseUrl$datasetsPath"
    $body = $Schema | ConvertTo-Json -Depth 15 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  $Name olusturuldu" -ForegroundColor Green
                return $true
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already exists|zaten")) {
                Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow
                return $true
            }
            Write-Host "  HATA: HTTP $httpCode" -ForegroundColor Red
            if ($responseBody) { Write-Host "  $responseBody" -ForegroundColor Gray }
            return $false
        } catch {
            Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    try {
        $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $null = Invoke-RestMethod @irmParams
        Write-Host "  $Name olusturuldu" -ForegroundColor Green
        return $true
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already exists|zaten")) {
            Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow
            return $true
        }
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        return $false
    }
}

# Tek bir kayit ekler (dataset olusturmaz). mon_collectible_templates seed icin kullanilir.
function Invoke-CreateTemplateRecord {
    param([string]$DatasetName, [object]$Record, [string]$Label)
    $uri = "$BaseUrl$dataPath/$DatasetName"
    $body = $Record | ConvertTo-Json -Depth 15 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  $Label olusturuldu" -ForegroundColor Green
                return $true
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already exists|zaten|duplicate|unique")) {
                Write-Host "  $Label zaten mevcut" -ForegroundColor Yellow
                return $true
            }
            Write-Host "  HATA: HTTP $httpCode" -ForegroundColor Red
            if ($responseBody) { Write-Host "  $responseBody" -ForegroundColor Gray }
            return $false
        } catch {
            Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    try {
        $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $null = Invoke-RestMethod @irmParams
        Write-Host "  $Label olusturuldu" -ForegroundColor Green
        return $true
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already exists|zaten|duplicate|unique")) {
            Write-Host "  $Label zaten mevcut" -ForegroundColor Yellow
            return $true
        }
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        return $false
    }
}

Write-Host "`nMonitoring Datasets (Faz 0) - Olusturuluyor...`n" -ForegroundColor Cyan

# 0.1 mon_asset_type_family
Write-Host "0.1 mon_asset_type_family" -ForegroundColor Yellow
$schema = @{
    Name         = "mon_asset_type_family"
    Description  = "Monitoring - Asset type family (Operating Systems, Network, vb.)"
    ForceSchema  = $true
    Logging      = "none"
    PublishMode  = "none"
    Fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Aile adi"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "code"; title = "Kod (slug)"; mandatory = $false; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false }
    )
    IndexList    = @(
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true },
        @{ name = "idx_code"; fields = @{ code = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "mon_asset_type_family" $schema "0.1")) { exit 1 }

# 0.2 mon_asset_types
Write-Host "`n0.2 mon_asset_types" -ForegroundColor Yellow
$schema = @{
    Name         = "mon_asset_types"
    Description  = "Monitoring - Asset type (Linux, Windows, SNMP Generic, vb.)"
    ForceSchema  = $true
    Logging      = "none"
    PublishMode  = "none"
    Fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Tip adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "family"; title = "Aile"; mandatory = $true; relationDataset = "mon_asset_type_family"; isArray = $false },
        @{ fieldType = "text"; name = "collection_method"; title = "Toplama metodu"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "collectibles"; title = "Toplanacaklar"; mandatory = $true; isArray = $true }
    )
    IndexList    = @(
        @{ name = "idx_family_name"; fields = @{ family = 1; name = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "mon_asset_types" $schema "0.2")) { exit 1 }

# 0.3 mon_collectible_templates (Collectible sablonlari - SNMP/HTTP vb. icin)
# Ref: docs/content/Mng.Ui/support/specs/COLLECTIBLE_TEMPLATES_DESIGN.md
Write-Host "`n0.3 mon_collectible_templates" -ForegroundColor Yellow
$schema = @{
    Name         = "mon_collectible_templates"
    Description  = "Monitoring - Collectible sablonlari (toplama metoduna gore hazir collectibles listesi)"
    ForceSchema  = $true
    Logging      = "none"
    PublishMode  = "none"
    Fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Sablon adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "collection_method"; title = "Toplama metodu (SNMP, HTTP, SSH, vb.)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "collectibles"; title = "Toplanacaklar listesi"; mandatory = $true; isArray = $true }
    )
    IndexList    = @(
        @{ name = "idx_collection_method_name"; fields = @{ collection_method = 1; name = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "mon_collectible_templates" $schema "0.3")) { exit 1 }

# 0.3.1 Seed MngSim SNMP sablonlari (PDU, Router) - MngSim simulator OID agaclariyla uyumlu
# Ref: MngSim/Services/SnmpPduOids.cs, MngSim/Services/RouterSnmpTemplate.cs
Write-Host "`n0.3.1 mon_collectible_templates - MngSim PDU ve Router sablonlari" -ForegroundColor Yellow
$pduCollectibles = @(
    @{ code = "deviceName";      name = "Cihaz adi";            data_type = "string"; oid = "1.3.6.1.4.1.99999.1.1.1" },
    @{ code = "inputVoltage";    name = "Giris gerilimi (V)";   data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.2" },
    @{ code = "inputCurrentX10"; name = "Giris akimi (x10 A)";  data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.3" },
    @{ code = "activePowerW";    name = "Aktif guc (W)";        data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.4" },
    @{ code = "temperature";    name = "Sicaklik (C)";          data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.5" },
    @{ code = "outletCount";    name = "Priz sayisi";           data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.6" },
    @{ code = "outletStatus_1"; name = "Priz 1 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.1" },
    @{ code = "outletStatus_2"; name = "Priz 2 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.2" },
    @{ code = "outletStatus_3"; name = "Priz 3 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.3" },
    @{ code = "outletStatus_4"; name = "Priz 4 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.4" },
    @{ code = "outletStatus_5"; name = "Priz 5 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.5" },
    @{ code = "outletStatus_6"; name = "Priz 6 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.6" },
    @{ code = "outletStatus_7"; name = "Priz 7 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.7" },
    @{ code = "outletStatus_8"; name = "Priz 8 durumu";         data_type = "number"; oid = "1.3.6.1.4.1.99999.1.1.7.8" }
)
$pduTemplate = @{
    name              = "PDU (MngSim)"
    collection_method = "SNMP"
    description       = "MngSim PDU simulatoru ile uyumlu: gerilim, akim, guc, sicaklik, priz durumlari (1.3.6.1.4.1.99999.1.1)"
    collectibles      = $pduCollectibles
}
Invoke-CreateTemplateRecord -DatasetName "mon_collectible_templates" -Record $pduTemplate -Label "Sablon: PDU (MngSim)" | Out-Null

$routerCollectibles = @(
    @{ code = "sysDescr";    name = "Sistem aciklamasi"; data_type = "string"; oid = "1.3.6.1.2.1.1.1.0" },
    @{ code = "sysUpTime";   name = "Sistem calisma suresi"; data_type = "number"; oid = "1.3.6.1.2.1.1.3.0" },
    @{ code = "sysContact";  name = "Iletisim"; data_type = "string"; oid = "1.3.6.1.2.1.1.4.0" },
    @{ code = "sysName";     name = "Sistem adi"; data_type = "string"; oid = "1.3.6.1.2.1.1.5.0" },
    @{ code = "sysLocation"; name = "Konum"; data_type = "string"; oid = "1.3.6.1.2.1.1.6.0" },
    @{ code = "ifNumber";    name = "Arayuz sayisi"; data_type = "number"; oid = "1.3.6.1.2.1.2.1.0" },
    @{ code = "ifInOctets_1";  name = "Arayuz 1 giris oktet";  data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.10.1" },
    @{ code = "ifOutOctets_1"; name = "Arayuz 1 cikis oktet"; data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.16.1" },
    @{ code = "ifInOctets_2";  name = "Arayuz 2 giris oktet";  data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.10.2" },
    @{ code = "ifOutOctets_2"; name = "Arayuz 2 cikis oktet"; data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.16.2" },
    @{ code = "ifInOctets_3";  name = "Arayuz 3 giris oktet";  data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.10.3" },
    @{ code = "ifOutOctets_3"; name = "Arayuz 3 cikis oktet"; data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.16.3" },
    @{ code = "ifInOctets_4";  name = "Arayuz 4 giris oktet";  data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.10.4" },
    @{ code = "ifOutOctets_4"; name = "Arayuz 4 cikis oktet"; data_type = "number"; oid = "1.3.6.1.2.1.2.2.1.16.4" }
)
$routerTemplate = @{
    name              = "Router (MngSim)"
    collection_method = "SNMP"
    description       = "MngSim Router simulatoru ile uyumlu: MIB-II sysDescr, sysUpTime, ifTable (1.3.6.1.2.1)"
    collectibles      = $routerCollectibles
}
Invoke-CreateTemplateRecord -DatasetName "mon_collectible_templates" -Record $routerTemplate -Label "Sablon: Router (MngSim)" | Out-Null

# 0.3.5 mon_http_auth_configs (HTTP Collector icin Bearer token endpoint tanimlari)
Write-Host "`n0.3.5 mon_http_auth_configs" -ForegroundColor Yellow
$schema = @{
    Name         = "mon_http_auth_configs"
    Description  = "Monitoring - HTTP Collector icin token endpoint tanimlari (Bearer auth)"
    ForceSchema  = $true
    Logging      = "none"
    PublishMode  = "none"
    Fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Tanım adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "tokenUrl"; title = "Token endpoint URL"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "tokenMethod"; title = "HTTP metodu (GET, POST)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "tokenBodyType"; title = "Body tipi (json, form)"; mandatory = $true; isArray = $false },
        @{ fieldType = "object"; name = "tokenBody"; title = "Token istek body (username, password, grant_type vb.)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "tokenResponsePath"; title = "Token JSON path (ornegin $.access_token)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false }
    )
    IndexList    = @(
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "mon_http_auth_configs" $schema "0.3.5")) { exit 1 }

# 0.4 mon_items
Write-Host "`n0.4 mon_items" -ForegroundColor Yellow
$schema = @{
    Name         = "mon_items"
    Description  = "Monitoring - Organizasyon agaci (Item hiyerarsisi)"
    ForceSchema  = $true
    Logging      = "none"
    PublishMode  = "none"
    Fields       = @(
        @{ fieldType = "text"; name = "name"; title = "Item adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "parentId"; title = "Ust item"; mandatory = $false; relationDataset = "mon_items"; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "location"; title = "Konum (lat, lon)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "kind"; title = "Tur (city, building, room, cabinet, vb.)"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "tags"; title = "Etiketler (key-value)"; mandatory = $false; isArray = $true }
    )
    IndexList    = @(
        @{ name = "idx_parentId"; fields = @{ parentId = 1 }; unique = $false },
        @{ name = "idx_kind"; fields = @{ kind = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "mon_items" $schema "0.4")) { exit 1 }

# 0.5 mon_collection_periods
Write-Host "`n0.5 mon_collection_periods" -ForegroundColor Yellow
$schema = @{
    Name        = "mon_collection_periods"
    Description = "Monitoring - Toplama periyodu tanimlari (cron ifadeleri)"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Periyot adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "expression"; title = "Cron ifadesi"; mandatory = $true; isArray = $false }
    )
    IndexList   = @()
}
if (-not (Invoke-CreateDataset "mon_collection_periods" $schema "0.5")) { exit 1 }

# 0.6 mon_schedules
Write-Host "`n0.6 mon_schedules" -ForegroundColor Yellow
$schema = @{
    Name        = "mon_schedules"
    Description = "Monitoring - Izleme araligi (window) tanimlari"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Schedule adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "type"; title = "Tip (always | scheduled)"; mandatory = $true; isArray = $false },
        @{ fieldType = "object"; name = "config"; title = "Zamanlama config"; mandatory = $false; isArray = $false }
    )
    IndexList   = @()
}
if (-not (Invoke-CreateDataset "mon_schedules" $schema "0.6")) { exit 1 }

# 0.7 mon_engines
Write-Host "`n0.7 mon_engines" -ForegroundColor Yellow
$schema = @{
    Name        = "mon_engines"
    Description = "Monitoring - Engine tanimlari (veri toplama cihazlari)"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Engine adi"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "status"; title = "Durum (active|inactive|maintenance)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "domain"; title = "Tenant domain"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "username"; title = "Engine auth kullanici adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "password"; title = "Engine auth sifresi (sifrelenmis)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "sendSchedule"; title = "Veri gonderim cron ifadesi"; mandatory = $true; isArray = $false },
        @{ fieldType = "number"; name = "configSyncPeriodMinutes"; title = "Config sync periyodu (dakika)"; mandatory = $false; isArray = $false },
        @{ fieldType = "datetime"; name = "lastSeenAt"; title = "Son gorulme zamani"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "health"; title = "Saglik (ok|degraded|error)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "hostAddress"; title = "Son bilinen IP (Engine'den)"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "lastErrors"; title = "Son toplama hatalari (asset/agent, errorCode, message)"; mandatory = $false; isArray = $true }
    )
    IndexList   = @(
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true },
        @{ name = "idx_status"; fields = @{ status = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "mon_engines" $schema "0.7")) { exit 1 }

# 0.8 mon_assets
Write-Host "`n0.8 mon_assets" -ForegroundColor Yellow
$schema = @{
    Name        = "mon_assets"
    Description = "Monitoring - Asset (izlenen varlik) kayitlari. Her asset bir Item icindedir."
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Asset adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "type"; title = "Asset tipi"; mandatory = $true; relationDataset = "mon_asset_types"; isArray = $false },
        @{ fieldType = "relation"; name = "itemId"; title = "Icinde bulundugu Item"; mandatory = $true; relationDataset = "mon_items"; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "tags"; title = "Etiketler"; mandatory = $false; isArray = $true },
        @{ fieldType = "text"; name = "status"; title = "Durum (active|maintenance|decommissioned)"; mandatory = $true; isArray = $false },
        @{ fieldType = "object"; name = "connection_info"; title = "Baglanti (endpoint + auth)"; mandatory = $true; isArray = $false },
        @{ fieldType = "object"; name = "collectible_config"; title = "Collectible override"; mandatory = $false; isArray = $true }
    )
    IndexList   = @(
        @{ name = "idx_itemId"; fields = @{ itemId = 1 }; unique = $false },
        @{ name = "idx_itemId_name"; fields = @{ itemId = 1; name = 1 }; unique = $true },
        @{ name = "idx_type"; fields = @{ type = 1 }; unique = $false },
        @{ name = "idx_status"; fields = @{ status = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "mon_assets" $schema "0.8")) { exit 1 }

# 0.9 mon_agents
Write-Host "`n0.9 mon_agents" -ForegroundColor Yellow
$schema = @{
    Name        = "mon_agents"
    Description = "Monitoring - Agent tanimlari (veri toplama yapilandirmasi)"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Agent adi"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "status"; title = "Durum"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "engineId"; title = "Engine"; mandatory = $true; relationDataset = "mon_engines"; isArray = $false },
        @{ fieldType = "relation"; name = "defaultPeriodId"; title = "Varsayilan periyot"; mandatory = $false; relationDataset = "mon_collection_periods"; isArray = $false },
        @{ fieldType = "relation"; name = "defaultScheduleId"; title = "Varsayilan izleme araligi"; mandatory = $false; relationDataset = "mon_schedules"; isArray = $false },
        @{ fieldType = "object"; name = "tags"; title = "Etiketler"; mandatory = $false; isArray = $true },
        @{ fieldType = "object"; name = "asset_configs"; title = "Asset yapilandirmalari"; mandatory = $true; isArray = $true }
    )
    IndexList   = @(
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true },
        @{ name = "idx_engineId"; fields = @{ engineId = 1 }; unique = $false },
        @{ name = "idx_status"; fields = @{ status = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "mon_agents" $schema "0.9")) { exit 1 }

# 0.10 mon_metrics (Reactor tarafindan yazilan time series - DG uzerinden sadece okuma)
# Collection: mon_metrics (time series: timeField=timestamp, metaField=meta)
# Yapi: { timestamp, meta: { agentId, assetId, collectibleCode, domain, engineId }, value, _id }
# Not: Koleksiyon Reactor tarafindan olusturulur, DG sadece dataset tanimi ile okur.
# Index: Reactor ilk ingest'te idx_assetId_collectibleCode_timestamp olusturur (meta.assetId, meta.collectibleCode, timestamp).
#       Manuel: scripts/tests/MngReactor/create-mon-metrics-index.js
Write-Host "`n0.10 mon_metrics" -ForegroundColor Yellow
$schema = @{
    Name        = "mon_metrics"
    Description = "Monitoring - Metrik verileri (Reactor time series, sadece okuma). meta.assetId ile filtreleme."
    ForceSchema = $false
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "datetime"; name = "timestamp"; title = "Olcum zamani"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "meta"; title = "Meta (agentId, assetId, collectibleCode, domain, engineId)"; mandatory = $false; isArray = $false },
        @{ fieldType = "number"; name = "value"; title = "Deger"; mandatory = $false; isArray = $false }
    )
    IndexList   = @()
}
if (-not (Invoke-CreateDataset "mon_metrics" $schema "0.10")) { exit 1 }

Write-Host "`nFaz 0 tamamlandi - 10 monitoring dataset olusturuldu." -ForegroundColor Green
Write-Host "Not: Dataset'ler token'daki domain veritabaninda (mng_{domain}) olusturuldu." -ForegroundColor Gray
Write-Host "Oneri: 'Monitoring Datasets' adinda bir Dataset Kategori olusturup bu 8 dataset'i altina alin." -ForegroundColor Gray
Write-Host ""
