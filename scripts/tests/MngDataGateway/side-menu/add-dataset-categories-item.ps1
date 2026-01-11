# Add Dataset Categories Menu Item to @side_menu Dataset
# Dataset Categories için menu item ekle

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Dataset Categories Menu Item Ekleme" -ForegroundColor Cyan
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

# Mevcut item'ları al
Write-Host "🔍 Mevcut menu items alınıyor..." -ForegroundColor Cyan
try {
    $allItems = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu?limit=10000" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    if (-not $allItems -or $allItems.Count -eq 0) {
        Write-Host "❌ Menu items bulunamadı!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ $($allItems.Count) menu item bulundu" -ForegroundColor Green
} catch {
    Write-Host "❌ Item'lar alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "📦 Error Details:" -ForegroundColor Gray
        $_.ErrorDetails.Message | Write-Host
    }
    exit 1
}

Write-Host ""

# "Apps" header'ını bul
$appsHeaderId = $null
$appsHeader = $allItems | Where-Object { $_.itemType -eq "header" -and ($_.header -eq "Apps" -or $_.header -eq "Applications") } | Select-Object -First 1

if ($appsHeader) {
    $appsHeaderId = $appsHeader.__dataId
    Write-Host "✅ 'Apps' header bulundu: $appsHeaderId (order: $($appsHeader.order))" -ForegroundColor Green
} else {
    Write-Host "❌ 'Apps' header bulunamadı! Lütfen önce Apps header'ını oluşturun." -ForegroundColor Red
    exit 1
}

# Apps header'ı altındaki item'ları bul ve max order'ı belirle
$appsItems = $allItems | Where-Object { $_.parentId -eq $appsHeaderId } | Sort-Object -Property order
$maxOrderInApps = if ($appsItems) {
    ($appsItems | Measure-Object -Property order -Maximum).Maximum
} else {
    # Apps header'ının order'ından sonra başla (genellikle header order + 1)
    $appsHeader.order
}

Write-Host "📊 Apps header altında $($appsItems.Count) item bulundu" -ForegroundColor Gray
if ($appsItems) {
    Write-Host "   Mevcut item'lar:" -ForegroundColor Gray
    foreach ($item in $appsItems) {
        Write-Host "   - $($item.title) (order: $($item.order))" -ForegroundColor Gray
    }
}

# Yeni order belirle (Apps altındaki max order + 1)
$newOrder = $maxOrderInApps + 1

Write-Host ""
Write-Host "📌 Yeni item order: $newOrder" -ForegroundColor Cyan
Write-Host ""

# Mevcut "Dataset Kategorileri" veya "Dataset Categories" item'ı var mı kontrol et
$existingItem = $allItems | Where-Object { 
    ($_.title -eq "Dataset Kategorileri" -or $_.title -eq "Dataset Categories") -and
    $_.itemType -eq "item" -and
    $_.parentId -eq $appsHeaderId
} | Select-Object -First 1

if ($existingItem) {
    Write-Host "⚠️  'Dataset Kategorileri' menu item zaten mevcut!" -ForegroundColor Yellow
    Write-Host "   DataId: $($existingItem.__dataId)" -ForegroundColor Gray
    Write-Host "   Order: $($existingItem.order)" -ForegroundColor Gray
    Write-Host "   Route: $($existingItem.to)" -ForegroundColor Gray
    Write-Host ""
    $update = Read-Host "Güncellemek ister misiniz? (E/H)"
    if ($update -ne "E" -and $update -ne "e" -and $update -ne "Y" -and $update -ne "y") {
        Write-Host "İşlem iptal edildi." -ForegroundColor Yellow
        exit 0
    }
    
    # Güncelleme için mevcut item'ı kullan
    $datasetCategoriesItem = @{
        order = $existingItem.order  # Order'ı koru (veya değiştirmek isterseniz $newOrder kullanın)
        itemType = "item"
        title = "Dataset Kategorileri"
        icon = "TagIcon"
        iconType = "tabler"
        to = "/apps/dataset-categories"
        type = "internal"
        level = 1
        parentId = $appsHeaderId
        pageType = "admin"
        pageCode = "apps-dataset-categories"
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
        $jsonBody = $datasetCategoriesItem | ConvertTo-Json -Depth 10
        Write-Host "📤 Güncelleme request'i gönderiliyor..." -ForegroundColor Gray
        Write-Host ""
        
        $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/$($existingItem.__dataId)" -Headers $headers -Method "PUT" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
        
        Write-Host "✅ Dataset Categories menu item başarıyla güncellendi!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📦 Response:" -ForegroundColor Gray
        $response | ConvertTo-Json -Depth 10 | Write-Host
        Write-Host ""
        Write-Host "🎉 Menu item güncellendi!" -ForegroundColor Green
        Write-Host "   Route: /apps/dataset-categories" -ForegroundColor Cyan
        Write-Host "   Page Type: admin" -ForegroundColor Cyan
        Write-Host "   Parent: Apps header" -ForegroundColor Cyan
        Write-Host ""
        
    } catch {
        Write-Host "❌ Güncelleme hatası: $($_.Exception.Message)" -ForegroundColor Red
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
} else {
    # Yeni item oluştur
    Write-Host "➕ Dataset Categories menu item oluşturuluyor..." -ForegroundColor Cyan
    
    $datasetCategoriesItem = @{
        order = $newOrder
        itemType = "item"
        title = "Dataset Kategorileri"
        icon = "TagIcon"
        iconType = "tabler"
        to = "/apps/dataset-categories"
        type = "internal"
        level = 1
        parentId = $appsHeaderId
        pageType = "admin"
        pageCode = "apps-dataset-categories"
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
        $jsonBody = $datasetCategoriesItem | ConvertTo-Json -Depth 10
        Write-Host "📤 Request gönderiliyor..." -ForegroundColor Gray
        Write-Host ""
        Write-Host "📋 Item Bilgileri:" -ForegroundColor Gray
        Write-Host "   Title: Dataset Kategorileri" -ForegroundColor Gray
        Write-Host "   Route: /apps/dataset-categories" -ForegroundColor Gray
        Write-Host "   Icon: TagIcon (tabler)" -ForegroundColor Gray
        Write-Host "   Page Type: admin" -ForegroundColor Gray
        Write-Host "   Parent: Apps header ($appsHeaderId)" -ForegroundColor Gray
        Write-Host "   Order: $newOrder" -ForegroundColor Gray
        Write-Host ""
        
        $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu" -Headers $headers -Method "POST" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
        
        Write-Host "✅ Dataset Categories menu item başarıyla eklendi!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📦 Response:" -ForegroundColor Gray
        $response | ConvertTo-Json -Depth 10 | Write-Host
        Write-Host ""
        Write-Host "🎉 Dataset Categories link'i menüye eklendi!" -ForegroundColor Green
        Write-Host "   Route: /apps/dataset-categories" -ForegroundColor Cyan
        Write-Host "   Page Type: admin" -ForegroundColor Cyan
        Write-Host "   Parent: Apps header" -ForegroundColor Cyan
        Write-Host "   Icon: TagIcon" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "💡 Not: Menu'yi görmek için sayfayı yenileyin veya SignalR ile otomatik güncellenmesini bekleyin." -ForegroundColor Yellow
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
}