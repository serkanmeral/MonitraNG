# Load Menu Items to @side_menu Dataset
# Faz 1.4: Menu verilerini veritabanına yükle (bulk insert)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Menu Items Bulk Insert" -ForegroundColor Cyan
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

# Menu items JSON dosyası (export script'inin oluşturduğu dosya)
$menuItemsFile = Join-Path $scriptPath "menu-items-export.json"

if (-not (Test-Path $menuItemsFile)) {
    Write-Host "❌ menu-items-export.json dosyası bulunamadı: $menuItemsFile" -ForegroundColor Red
    Write-Host ""
    Write-Host "ℹ️  Menu items dosyasını oluşturmak için:" -ForegroundColor Yellow
    Write-Host "   1. export-menu-tsx.mjs script'ini çalıştırın: node scripts/tests/MngDataGateway/side-menu/export-menu-tsx.mjs" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "📂 Menu items dosyası bulundu: $menuItemsFile" -ForegroundColor Green
Write-Host ""

# JSON dosyasını oku
try {
    $menuItemsJson = Get-Content -Path $menuItemsFile -Raw -Encoding UTF8 | ConvertFrom-Json
    
    if (-not $menuItemsJson) {
        Write-Host "❌ menu-items-export.json dosyası boş veya geçersiz!" -ForegroundColor Red
        exit 1
    }
    
    # Array kontrolü
    if ($menuItemsJson -isnot [Array] -and $menuItemsJson.items -is [Array]) {
        $menuItems = $menuItemsJson.items
    } elseif ($menuItemsJson -is [Array]) {
        $menuItems = $menuItemsJson
    } else {
        Write-Host "❌ menu-items-export.json dosyası array formatında değil!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Menu items okundu: $($menuItems.Count) item bulundu" -ForegroundColor Green
    Write-Host ""
    
} catch {
    Write-Host "❌ menu-items.json dosyası okunamadı: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Menu items'ı bulk insert için hazırla
# Her item için __dataId field'ı yoksa eklenmemeli (server otomatik oluşturur)
# ParentId'ler henüz __dataId olmadığı için string olarak saklanmalı (sonra güncellenebilir)
# Veya parentId'leri null yapıp sonra güncelleyebiliriz

$itemsToInsert = @()
$order = 0
$parentIdMap = @{} # title -> __dataId mapping (insert sonrası)
$itemParentOrderMap = @{} # order -> parentOrder mapping (insert sonrası parentId güncellemesi için)

# İlk pass: Tüm item'ları level ve order'a göre sırala ve ekle
$sortedItems = $menuItems | Sort-Object { $_.level }, { $_.order }

foreach ($item in $sortedItems) {
    $menuItem = @{
        order = if ($null -ne $item.order) { $item.order } else { $order++ }
        itemType = $item.itemType
        level = if ($null -ne $item.level) { $item.level } else { 0 }
    }
    
    if ($item.header) {
        $menuItem.header = $item.header
    }
    
    if ($item.title) {
        $menuItem.title = $item.title
    }
    
    if ($item.icon) {
        $menuItem.icon = $item.icon
    }
    
    if ($item.iconType) {
        $menuItem.iconType = $item.iconType
    } else {
        $menuItem.iconType = "tabler"
    }
    
    if ($item.to) {
        $menuItem.to = $item.to
    }
    
    if ($item.type) {
        $menuItem.type = $item.type
    } else {
        $menuItem.type = "internal"
    }
    
    if ($item.pageType) {
        $menuItem.pageType = $item.pageType
    } else {
        $menuItem.pageType = "admin" # Default: admin (kullanıcı istediği gibi)
    }
    
    if ($item.pageCode) {
        $menuItem.pageCode = $item.pageCode
    }
    
    # ParentId: İlk insert'te null (relation field olduğu için __dataId gerekli, insert sonrası güncellenecek)
    $menuItem.parentId = $null # İlk insert'te null, sonra güncellenecek
    
    # Parent order referansını sakla (memory'de, insert'e dahil etme)
    if ($item.parentId -ne $null) {
        $itemParentOrderMap[$menuItem.order] = $item.parentId # Order -> parentOrder mapping
    }
    
    if ($item.chip) {
        $menuItem.chip = $item.chip
    }
    
    if ($item.chipBgColor) {
        $menuItem.chipBgColor = $item.chipBgColor
    }
    
    if ($item.chipColor) {
        $menuItem.chipColor = $item.chipColor
    }
    
    if ($item.chipVariant) {
        $menuItem.chipVariant = $item.chipVariant
    }
    
    if ($item.chipIcon) {
        $menuItem.chipIcon = $item.chipIcon
    }
    
    if ($null -ne $item.disabled) {
        $menuItem.disabled = $item.disabled
    } else {
        $menuItem.disabled = $false
    }
    
    if ($item.subCaption) {
        $menuItem.subCaption = $item.subCaption
    }
    
    if ($item.permissions) {
        $menuItem.permissions = $item.permissions
    }
    
    $itemsToInsert += $menuItem
}

    Write-Host "📦 $($itemsToInsert.Count) item bulk insert için hazırlandı" -ForegroundColor Green
    
    # Test: İlk 10 item ile test et
    if ($itemsToInsert.Count -gt 10) {
        Write-Host "⚠️  Test için ilk 10 item ile başlanıyor..." -ForegroundColor Yellow
        $itemsToInsert = $itemsToInsert | Select-Object -First 10
        Write-Host "📦 Test için $($itemsToInsert.Count) item hazırlandı" -ForegroundColor Yellow
    }
    Write-Host ""
    
    # Bulk insert request body
    $bulkInsertBody = @{
        items = $itemsToInsert
    }

# Bulk insert API call
Write-Host "🚀 Bulk insert başlatılıyor..." -ForegroundColor Cyan

try {
    $jsonBody = $bulkInsertBody | ConvertTo-Json -Depth 20
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/bulk" -Headers $headers -Method "POST" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "✅ Bulk insert başarılı!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Response:" -ForegroundColor Gray
    $response | ConvertTo-Json -Depth 10 | Write-Host
    Write-Host ""
    
    # Insert sonuçları
    if ($response.data) {
        # Response yapısı: data.Items (başarılı items), data.Errors (hata items)
        $successfulItems = $response.data.Items
        $successCount = if ($successfulItems) { $successfulItems.Count } else { 0 }
        $errorCount = if ($response.data.Errors) { $response.data.Errors.Count } else { 0 }
        
        Write-Host "📊 Özet:" -ForegroundColor Cyan
        Write-Host "   Başarılı: $successCount" -ForegroundColor Green
        if ($errorCount -gt 0) {
            Write-Host "   Hata: $errorCount" -ForegroundColor Red
            Write-Host ""
            Write-Host "❌ Hatalar:" -ForegroundColor Red
            foreach ($err in $response.data.Errors) {
                Write-Host "   - Index $($err.Index): $($err.Error)" -ForegroundColor Red
                if ($err.Details) {
                    foreach ($detail in $err.Details) {
                        if ($detail.field) {
                            Write-Host "     Field '$($detail.field)': $($detail.message)" -ForegroundColor DarkRed
                        }
                    }
                }
            }
        }
        Write-Host ""
        
        # Başarılı item'ların __dataId'lerini kaydet ve parentId'leri güncelle
        if ($successfulItems) {
            Write-Host "📝 ParentId'leri güncelleniyor..." -ForegroundColor Cyan
            
            # Order -> __dataId mapping oluştur
            $orderToDataIdMap = @{}
            foreach ($insertedItem in $successfulItems) {
                if ($null -ne $insertedItem.order -and $null -ne $insertedItem.__dataId) {
                    $orderToDataIdMap[$insertedItem.order] = $insertedItem.__dataId
                }
            }
            
            Write-Host "   Order -> __dataId mapping oluşturuldu: $($orderToDataIdMap.Count) item" -ForegroundColor Gray
            
            # Insert edilen item'ların parentId'lerini kontrol et ve güncelle
            # Export'ta parentId'ler order referansı olarak geliyor, _parentOrder field'ında saklandı
            $updatesNeeded = @()
            
            foreach ($insertedItem in $successfulItems) {
                # Item'ın parent order referansını kontrol et (memory'de saklanmış)
                if ($itemParentOrderMap.ContainsKey($insertedItem.order)) {
                    # parentId bir order referansı, __dataId'ye çevir
                    $parentOrder = $itemParentOrderMap[$insertedItem.order]
                    
                    if ($orderToDataIdMap.ContainsKey($parentOrder)) {
                        $parentDataId = $orderToDataIdMap[$parentOrder]
                        
                        # ParentId güncellemesi gerekiyor
                        $updatesNeeded += @{
                            dataId = $insertedItem.__dataId
                            parentId = $parentDataId
                            title = if ($insertedItem.title) { $insertedItem.title } else { $insertedItem.header }
                            order = $insertedItem.order
                        }
                    } else {
                        Write-Host "⚠️  Parent order bulunamadı: $parentOrder (item order: $($insertedItem.order))" -ForegroundColor Yellow
                    }
                }
            }
            
            Write-Host "   Güncellenecek item sayısı: $($updatesNeeded.Count)" -ForegroundColor Gray
            
            # ParentId güncellemelerini yap
            $updateCount = 0
            $updateErrorCount = 0
            
            foreach ($update in $updatesNeeded) {
                try {
                    $updateBody = @{
                        parentId = $update.parentId
                    } | ConvertTo-Json
                    
                    $updateResponse = Invoke-RestMethod `
                        -Uri "$baseUrl/api/v1/data/@side_menu/$($update.dataId)" `
                        -Headers $headers `
                        -Method "PUT" `
                        -Body $updateBody `
                        -SkipCertificateCheck `
                        -ErrorAction Stop
                    
                    $updateCount++
                    
                    if ($updateCount % 10 -eq 0) {
                        Write-Host "   $updateCount item güncellendi..." -ForegroundColor Gray
                    }
                } catch {
                    $updateErrorCount++
                    Write-Host "⚠️  ParentId güncelleme hatası (Order: $($update.order), $($update.title)): $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
            
            Write-Host ""
            Write-Host "✅ $updateCount item'ın parentId'si güncellendi" -ForegroundColor Green
            if ($updateErrorCount -gt 0) {
                Write-Host "⚠️  $updateErrorCount item güncellenemedi" -ForegroundColor Yellow
            }
            Write-Host ""
            
            $dataIdsFile = Join-Path $scriptPath "menu-items-dataids.json"
            $successfulItems | ConvertTo-Json -Depth 10 | Out-File -FilePath $dataIdsFile -Encoding UTF8
            Write-Host "💾 __dataId'ler kaydedildi: $dataIdsFile" -ForegroundColor Green
            Write-Host ""
        }
    }
    
    Write-Host "🎉 Menu items başarıyla yüklendi!" -ForegroundColor Green
    Write-Host ""
    
} catch {
    Write-Host "❌ Hata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "📦 Error Details:" -ForegroundColor Gray
        $_.ErrorDetails.Message | Write-Host
    }
    Write-Host ""
    exit 1
}
