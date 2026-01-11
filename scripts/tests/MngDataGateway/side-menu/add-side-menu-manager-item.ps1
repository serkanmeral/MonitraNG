# Add Side Menu Manager Item to @side_menu Dataset
# Side Menu Manager için menu item ekle

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Side Menu Manager Item Ekleme" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"

# Token'ı yükle
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "❌ load-token.ps1 bulunamadı! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Token yüklendi" -ForegroundColor Green
Write-Host ""

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "⚠️  SSL sertifika kontrolü devre dışı (development)" -ForegroundColor Yellow
Write-Host ""

# Mevcut item'ları al ve max order'ı bul
Write-Host "🔍 Mevcut menu items alınıyor..." -ForegroundColor Cyan
try {
    $allItems = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu?limit=10000" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    if (-not $allItems -or $allItems.Count -eq 0) {
        $maxOrder = 0
        Write-Host "ℹ️  Mevcut item bulunamadı, order 0'dan başlanacak" -ForegroundColor Yellow
    } else {
        $maxOrder = ($allItems | Measure-Object -Property order -Maximum).Maximum
        Write-Host "📊 Mevcut max order: $maxOrder" -ForegroundColor Green
    }
} catch {
    Write-Host "⚠️  Item'lar alınamadı, order 0'dan başlanacak: $($_.Exception.Message)" -ForegroundColor Yellow
    $maxOrder = 0
}

Write-Host ""

# Side Menu Manager item'ı oluştur
$newOrder = $maxOrder + 1

# "Apps" header'ını bul veya oluştur
$appsHeaderId = $null
$appsHeader = $allItems | Where-Object { $_.itemType -eq "header" -and ($_.header -eq "Apps" -or $_.header -eq "Applications") } | Select-Object -First 1

if ($appsHeader) {
    $appsHeaderId = $appsHeader.__dataId
    Write-Host "✅ 'Apps' header bulundu: $appsHeaderId" -ForegroundColor Green
} else {
    # Apps header'ı oluştur
    Write-Host "➕ 'Apps' header oluşturuluyor..." -ForegroundColor Cyan
    
    $appsHeaderData = @{
        order = $newOrder
        itemType = "header"
        header = "Apps"
        level = 0
        parentId = $null
        pageType = "admin"
        pageCode = "apps"
        disabled = $false
    }
    
    try {
        $appsHeaderResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu" -Headers $headers -Method "POST" -Body ($appsHeaderData | ConvertTo-Json -Depth 10) -SkipCertificateCheck -ErrorAction Stop
        
        if ($appsHeaderResponse.__dataId) {
            $appsHeaderId = $appsHeaderResponse.__dataId
            $newOrder = $newOrder + 1
            Write-Host "✅ 'Apps' header oluşturuldu: $appsHeaderId" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Apps header oluşturuldu ama __dataId bulunamadı" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "❌ Apps header oluşturma hatası: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "📦 Error Details:" -ForegroundColor Gray
            $_.ErrorDetails.Message | Write-Host
        }
        exit 1
    }
}

Write-Host ""

# Side Menu Manager item'ı oluştur
Write-Host "➕ Side Menu Manager item oluşturuluyor..." -ForegroundColor Cyan

$sideMenuManagerItem = @{
    order = $newOrder
    itemType = "item"
    title = "Side Menu Manager"
    icon = "SettingsIcon"
    iconType = "tabler"
    to = "/apps/side-menu-manager"
    type = "internal"
    level = 1
    parentId = $appsHeaderId
    pageType = "admin"
    pageCode = "apps-side-menu-manager"
    disabled = $false
    permissions = @{
        groups = @{
            admins = @{
                view = $true
                create = $true
                update = $true
                delete = $true
                export = $true
            }
            managers = @{
                view = $true
                create = $false
                update = $false
                delete = $false
                export = $false
            }
        }
    }
}

try {
    $jsonBody = $sideMenuManagerItem | ConvertTo-Json -Depth 10
    Write-Host "📤 Request gönderiliyor..." -ForegroundColor Gray
    Write-Host ""
    
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu" -Headers $headers -Method "POST" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "✅ Side Menu Manager item başarıyla eklendi!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Response:" -ForegroundColor Gray
    $response | ConvertTo-Json -Depth 10 | Write-Host
    Write-Host ""
    Write-Host "🎉 Side Menu Manager link'i menüye eklendi!" -ForegroundColor Green
    Write-Host "   Route: /apps/side-menu-manager" -ForegroundColor Cyan
    Write-Host "   Page Type: admin" -ForegroundColor Cyan
    Write-Host "   Parent: Apps header" -ForegroundColor Cyan
    Write-Host ""
    
} catch {
    Write-Host "❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "📦 Error Details:" -ForegroundColor Gray
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            $errorJson | ConvertTo-Json -Depth 10 | Write-Host
        } catch {
            $_.ErrorDetails.Message | Write-Host
        }
    }
    Write-Host ""
    exit 1
}
