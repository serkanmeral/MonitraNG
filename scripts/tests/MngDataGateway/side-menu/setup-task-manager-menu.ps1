# Task Manager yan menü: Apps altında üst öğe "Görevler" + alt öğeler "Bana atananlar", "Durum havuzu"
# DG Gateway: -BaseUrl "https://localhost:5040" -UseGateway
# DG doğrudan: -BaseUrl "https://localhost:5010" -UseGateway:$false
#
# Idempotent: "Bana atananlar" ve "Durum havuzu" kayıtları zaten varsa yeniden eklenmez.
# Mevcut düz "Görevler" (/apps/task-manager) satırı varsa üst öğe olarak pageCode ile güncellenir.

param(
    [string]$BaseUrl = "https://localhost:5010",
    [switch]$UseGateway = $false
)

$ErrorActionPreference = "Stop"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$menuUrl = "$($BaseUrl.TrimEnd('/'))$dataPath/@side_menu"

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = Get-Location }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Task Manager yan menü (side_menu)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Endpoint: $menuUrl" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadı: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alınamadı." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# HTTPS: curl -k yedek yolu (load-token / token curl ile çalışıyorsa burası da güvenilir olur)
# Ayrıca eski .NET TLS / sertifika zinciri için ServicePointManager (Invoke-RestMethod yedeği)
$useCurl = ($BaseUrl -match '^https://') -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
try {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a, $b, $c, $d) $true }
} catch { }
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
} catch { }

$skipCert = $false
$irm = Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue
if ($irm -and $irm.Parameters.ContainsKey("SkipCertificateCheck")) {
    $skipCert = $true
}

function Add-SkipCert {
    param([hashtable]$Params)
    if ($skipCert) { $Params["SkipCertificateCheck"] = $true }
    return $Params
}

function ConvertFrom-JsonToList {
    param([string]$JsonText)
    if ([string]::IsNullOrWhiteSpace($JsonText)) { return @() }
    $raw = $JsonText | ConvertFrom-Json
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return $raw }
    return @($raw)
}

function Get-MenuItems {
    $uri = "$menuUrl`?limit=10000"
    if ($useCurl) {
        $out = & curl.exe -s -S -k -H "Authorization: Bearer $token" -H "Accept: application/json" $uri 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "curl GET başarısız (exit $LASTEXITCODE): $out"
        }
        return ConvertFrom-JsonToList -JsonText ([string]$out)
    }
    $p = Add-SkipCert @{
        Uri         = $uri
        Headers     = $headers
        Method      = "GET"
        ErrorAction = "Stop"
    }
    $raw = Invoke-RestMethod @p
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return $raw }
    return @($raw)
}

function Invoke-MenuPut {
    param([string]$Id, [hashtable]$Body)
    $jsonBody = $Body | ConvertTo-Json -Depth 12
    $target = "$menuUrl/$Id"
    if ($useCurl) {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        try {
            $jsonBody | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -S -k -w "`n%{http_code}" -X PUT -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $target 2>&1 | Out-String
        } finally {
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        }
        $lines = $output.Trim() -split "`n"
        if ($lines.Count -lt 1) { throw "PUT yanıtı boş" }
        $httpCode = ($lines[-1] -replace '[^\d]', '').Trim()
        $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { "" }
        if ($httpCode -notin @('200', '201', '204')) {
            throw "PUT HTTP $httpCode : $responseBody"
        }
        if ([string]::IsNullOrWhiteSpace($responseBody)) { return $null }
        return $responseBody | ConvertFrom-Json
    }
    $p = Add-SkipCert @{
        Uri         = $target
        Method      = "PUT"
        Headers     = $headers
        Body        = $jsonBody
        ErrorAction = "Stop"
    }
    Invoke-RestMethod @p
}

function Invoke-MenuPost {
    param([hashtable]$Body)
    $jsonBody = $Body | ConvertTo-Json -Depth 12
    if ($useCurl) {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        try {
            $jsonBody | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -S -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $menuUrl 2>&1 | Out-String
        } finally {
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        }
        $lines = ($output.Trim() -split "`n")
        if ($lines.Count -lt 1) { throw "POST yanıtı boş" }
        $httpCode = ($lines[-1] -replace '[^\d]', '').Trim()
        $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { "" }
        if ($httpCode -notin @('200', '201')) {
            throw "POST HTTP $httpCode : $responseBody"
        }
        if ([string]::IsNullOrWhiteSpace($responseBody)) { return $null }
        return $responseBody | ConvertFrom-Json
    }
    $p = Add-SkipCert @{
        Uri         = $menuUrl
        Method      = "POST"
        Headers     = $headers
        Body        = $jsonBody
        ErrorAction = "Stop"
    }
    Invoke-RestMethod @p
}

try {
    $allItems = Get-MenuItems
} catch {
    Write-Host "Menu okunamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "Toplam $($allItems.Count) menü kaydı okundu." -ForegroundColor Green

$appsHeader = $allItems | Where-Object {
    $_.itemType -eq "header" -and ($_.header -eq "Apps" -or $_.header -eq "Applications")
} | Select-Object -First 1

if (-not $appsHeader) {
    Write-Host "Apps başlığı bulunamadı." -ForegroundColor Red
    exit 1
}

$appsHeaderId = $appsHeader.__dataId
Write-Host "Apps header: $appsHeaderId" -ForegroundColor Gray

$existingAssigned = $allItems | Where-Object {
    $_.itemType -eq "item" -and (
        $_.to -eq "/apps/task-manager/assigned" -or
        $_.pageCode -eq "apps-task-manager-assigned"
    )
} | Select-Object -First 1

$existingStatusesPool = $allItems | Where-Object {
    $_.itemType -eq "item" -and (
        $_.to -eq "/apps/task-manager/statuses" -or
        $_.pageCode -eq "apps-task-manager-statuses"
    )
} | Select-Object -First 1

if ($existingAssigned) {
    Write-Host "Bana atananlar menü öğesi zaten var (__dataId: $($existingAssigned.__dataId)). Çıkılıyor." -ForegroundColor Yellow
    exit 0
}

# Üst öğe: pageCode veya /apps/task-manager + Apps altında
$parent = $allItems | Where-Object {
    $_.itemType -eq "item" -and
    $_.parentId -eq $appsHeaderId -and
    (
        $_.pageCode -eq "apps-task-manager" -or
        ($_.to -eq "/apps/task-manager" -and ($null -eq $_.pageCode -or $_.pageCode -eq ""))
    )
} | Select-Object -First 1

# Düz satır: sadece route eşleşmesi (pageCode farklı olabilir)
if (-not $parent) {
    $parent = $allItems | Where-Object {
        $_.itemType -eq "item" -and
        $_.parentId -eq $appsHeaderId -and
        $_.to -eq "/apps/task-manager"
    } | Select-Object -First 1
}

$tmParentPermissions = @{
    groups = @{
        admins = @{
            view = $true; create = $true; update = $true; delete = $true; export = $true
        }
        managers = @{
            view = $true; create = $false; update = $false; delete = $false; export = $false
        }
    }
}

if ($parent) {
    Write-Host "Üst öğe mevcut, pageCode güncelleniyor (gerekirse): $($parent.__dataId)" -ForegroundColor Cyan
    $parentBody = @{
        order       = $parent.order
        itemType    = "item"
        title       = "Görevler"
        icon        = "LayoutKanbanIcon"
        iconType    = "tabler"
        to          = "/apps/task-manager"
        type        = "internal"
        level       = 1
        parentId    = $appsHeaderId
        pageType    = "manager"
        pageCode    = "apps-task-manager"
        disabled    = $false
        permissions = $tmParentPermissions
    }
    try {
        Invoke-MenuPut -Id $parent.__dataId -Body $parentBody | Out-Null
    } catch {
        Write-Host "Üst öğe güncellenemedi: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    $parentId = $parent.__dataId
} else {
    $appsItems = $allItems | Where-Object { $_.parentId -eq $appsHeaderId }
    $maxOrderInApps = if ($appsItems) {
        ($appsItems | Measure-Object -Property order -Maximum).Maximum
    } else {
        $appsHeader.order
    }
    $newOrder = $maxOrderInApps + 1
    Write-Host "Yeni üst öğe oluşturuluyor (order=$newOrder)..." -ForegroundColor Cyan
    $parentBody = @{
        order       = $newOrder
        itemType    = "item"
        title       = "Görevler"
        icon        = "LayoutKanbanIcon"
        iconType    = "tabler"
        to          = "/apps/task-manager"
        type        = "internal"
        level       = 1
        parentId    = $appsHeaderId
        pageType    = "manager"
        pageCode    = "apps-task-manager"
        disabled    = $false
        permissions = $tmParentPermissions
    }
    try {
        $created = Invoke-MenuPost -Body $parentBody
        $parentId = $created.__dataId
        if (-not $parentId) {
            $allItems = Get-MenuItems
            $p2 = $allItems | Where-Object { $_.pageCode -eq "apps-task-manager" -and $_.to -eq "/apps/task-manager" } | Select-Object -First 1
            $parentId = $p2.__dataId
        }
    } catch {
        Write-Host "Üst öğe oluşturulamadı: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

if (-not $parentId) {
    Write-Host "Üst öğe id alınamadı." -ForegroundColor Red
    exit 1
}

# Global max order (iç içe menülerde sıra genelde tüm öğeler arasında)
$allItems = Get-MenuItems
$maxOrder = ($allItems | Measure-Object -Property order -Maximum).Maximum
if ($null -eq $maxOrder) { $maxOrder = 0 }
$childOrder = [int]$maxOrder + 1

$childBody = @{
    order       = $childOrder
    itemType    = "item"
    title       = "Bana atananlar"
    icon        = "CircleDotIcon"
    iconType    = "tabler"
    to          = "/apps/task-manager/assigned"
    type        = "internal"
    level       = 2
    parentId    = $parentId
    pageType    = "manager"
    pageCode    = "apps-task-manager-assigned"
    disabled    = $false
    permissions = $tmParentPermissions
}

try {
    Write-Host "Alt öğe ekleniyor: /apps/task-manager/assigned (order=$childOrder)..." -ForegroundColor Cyan
    Invoke-MenuPost -Body $childBody | Out-Null
    Write-Host "Tamamlandı (assigned). Menüyü UI'da görmek için sayfayı yenileyin veya SignalR güncellemesini bekleyin." -ForegroundColor Green
} catch {
    Write-Host "Alt öğe eklenemedi: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message | Write-Host -ForegroundColor Gray }
    exit 1
}

if (-not $existingStatusesPool) {
    $allItems = Get-MenuItems
    $maxOrder2 = ($allItems | Measure-Object -Property order -Maximum).Maximum
    if ($null -eq $maxOrder2) { $maxOrder2 = 0 }
    $childOrder2 = [int]$maxOrder2 + 1
    $statusesBody = @{
        order       = $childOrder2
        itemType    = "item"
        title       = "Durum havuzu"
        icon        = "ListDetailsIcon"
        iconType    = "tabler"
        to          = "/apps/task-manager/statuses"
        type        = "internal"
        level       = 2
        parentId    = $parentId
        pageType    = "manager"
        pageCode    = "apps-task-manager-statuses"
        disabled    = $false
        permissions = $tmParentPermissions
    }
    try {
        Write-Host "Alt öğe ekleniyor: /apps/task-manager/statuses (order=$childOrder2)..." -ForegroundColor Cyan
        Invoke-MenuPost -Body $statusesBody | Out-Null
        Write-Host "Tamamlandı (durum havuzu)." -ForegroundColor Green
    } catch {
        Write-Host "Durum havuzu menü öğesi eklenemedi: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message | Write-Host -ForegroundColor Gray }
        exit 1
    }
} else {
    Write-Host "Durum havuzu menü öğesi zaten mevcut, atlanıyor." -ForegroundColor Yellow
}

exit 0
