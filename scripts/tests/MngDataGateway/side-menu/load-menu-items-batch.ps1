# Load Menu Items in Batches
# Küçük batch'ler halinde yükle ve hataları tespit et

param(
    [int]$BatchSize = 20
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Menu Items Batch Insert" -ForegroundColor Cyan
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
    Write-Host "❌ load-token.ps1 bulunamadı!" -ForegroundColor Red
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

# Menu items JSON dosyası
$menuItemsFile = Join-Path $scriptPath "menu-items-export.json"

if (-not (Test-Path $menuItemsFile)) {
    Write-Host "❌ menu-items-export.json dosyası bulunamadı!" -ForegroundColor Red
    exit 1
}

Write-Host "📂 Menu items dosyası bulundu: $menuItemsFile" -ForegroundColor Green
Write-Host ""

# JSON dosyasını oku
$menuItems = Get-Content -Path $menuItemsFile -Raw -Encoding UTF8 | ConvertFrom-Json

if (-not $menuItems -or $menuItems.Count -eq 0) {
    Write-Host "❌ Menu items bulunamadı!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Menu items okundu: $($menuItems.Count) item" -ForegroundColor Green
Write-Host "📦 Batch size: $BatchSize" -ForegroundColor Cyan
Write-Host ""

# Batch'ler halinde yükle
$totalBatches = [Math]::Ceiling($menuItems.Count / $BatchSize)
$totalInserted = 0
$totalErrors = 0
$itemParentOrderMap = @{} # order -> parentOrder mapping

Write-Host "🚀 Batch'ler halinde yükleme başlatılıyor..." -ForegroundColor Cyan
Write-Host ""

for ($batchIndex = 0; $batchIndex -lt $totalBatches; $batchIndex++) {
    $startIndex = $batchIndex * $BatchSize
    $endIndex = [Math]::Min($startIndex + $BatchSize - 1, $menuItems.Count - 1)
    $batchItems = $menuItems[$startIndex..$endIndex]
    
    Write-Host "📦 Batch $($batchIndex + 1)/$totalBatches (Items $($startIndex + 1)-$($endIndex + 1))..." -ForegroundColor Cyan
    
    # Batch'i hazırla
    $itemsToInsert = @()
    foreach ($item in $batchItems) {
        $menuItem = @{
            order = if ($null -ne $item.order) { $item.order } else { 0 }
            itemType = $item.itemType
            level = if ($null -ne $item.level) { $item.level } else { 0 }
            parentId = $null # İlk insert'te null
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
        }
        
        if ($item.to) {
            $menuItem.to = $item.to
        }
        
        if ($item.type) {
            $menuItem.type = $item.type
        }
        
        if ($item.pageType) {
            $menuItem.pageType = $item.pageType
        }
        
        if ($item.pageCode) {
            $menuItem.pageCode = $item.pageCode
        }
        
        # Parent order mapping
        if ($item.parentId -ne $null) {
            $itemParentOrderMap[$menuItem.order] = $item.parentId
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
        }
        
        if ($item.subCaption) {
            $menuItem.subCaption = $item.subCaption
        }
        
        $itemsToInsert += $menuItem
    }
    
    # Batch insert
    $bulkInsertBody = @{
        items = $itemsToInsert
    }
    
    try {
        $jsonBody = $bulkInsertBody | ConvertTo-Json -Depth 20
        $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/bulk" -Headers $headers -Method "POST" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
        
        if ($response.data) {
            $successfulItems = $response.data.Items
            $successCount = if ($successfulItems) { $successfulItems.Count } else { 0 }
            $errorCount = if ($response.data.Errors) { $response.data.Errors.Count } else { 0 }
            
            $totalInserted += $successCount
            $totalErrors += $errorCount
            
            Write-Host "   ✅ Başarılı: $successCount, ❌ Hata: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Yellow" } else { "Green" })
            
            if ($errorCount -gt 0) {
                foreach ($err in $response.data.Errors) {
                    Write-Host "      - Index $($err.Index): $($err.Error)" -ForegroundColor Red
                }
            }
        }
    } catch {
        Write-Host "   ❌ Batch hatası: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            try {
                $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
                if ($errorJson.error) {
                    Write-Host "      Code: $($errorJson.error.code)" -ForegroundColor Red
                    Write-Host "      Message: $($errorJson.error.message)" -ForegroundColor Red
                    if ($errorJson.error.details) {
                        Write-Host "      Details: $($errorJson.error.details)" -ForegroundColor Red
                    }
                }
            } catch {
                $_.ErrorDetails.Message | Write-Host
            }
        }
        Write-Host ""
        Write-Host "⚠️  Batch $($batchIndex + 1) başarısız, durduruluyor..." -ForegroundColor Yellow
        break
    }
    
    Write-Host ""
}

Write-Host "📊 Toplam özet:" -ForegroundColor Cyan
Write-Host "   Başarılı: $totalInserted" -ForegroundColor Green
Write-Host "   Hata: $totalErrors" -ForegroundColor $(if ($totalErrors -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($totalInserted -gt 0) {
    Write-Host "📝 ParentId'ler güncelleniyor..." -ForegroundColor Cyan
    
    # Tüm inserted item'ları al
    $allInsertedItems = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu?limit=10000" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    if ($allInsertedItems -and $allInsertedItems.Count -gt 0) {
        # Order -> __dataId mapping
        $orderToDataIdMap = @{}
        foreach ($item in $allInsertedItems) {
            if ($null -ne $item.order -and $null -ne $item.__dataId) {
                $orderToDataIdMap[$item.order] = $item.__dataId
            }
        }
        
        Write-Host "   Order -> __dataId mapping oluşturuldu: $($orderToDataIdMap.Count) item" -ForegroundColor Gray
        
        # ParentId güncellemeleri
        $updatesNeeded = @()
        foreach ($item in $allInsertedItems) {
            if ($itemParentOrderMap.ContainsKey($item.order)) {
                $parentOrder = $itemParentOrderMap[$item.order]
                
                if ($orderToDataIdMap.ContainsKey($parentOrder)) {
                    $parentDataId = $orderToDataIdMap[$parentOrder]
                    $updatesNeeded += @{
                        dataId = $item.__dataId
                        parentId = $parentDataId
                        title = if ($item.title) { $item.title } else { $item.header }
                        order = $item.order
                    }
                }
            }
        }
        
        Write-Host "   Güncellenecek item sayısı: $($updatesNeeded.Count)" -ForegroundColor Gray
        
        $updateCount = 0
        foreach ($update in $updatesNeeded) {
            try {
                $updateBody = @{
                    parentId = $update.parentId
                } | ConvertTo-Json
                
                Invoke-RestMethod `
                    -Uri "$baseUrl/api/v1/data/@side_menu/$($update.dataId)" `
                    -Headers $headers `
                    -Method "PUT" `
                    -Body $updateBody `
                    -SkipCertificateCheck `
                    -ErrorAction Stop | Out-Null
                
                $updateCount++
                
                if ($updateCount % 10 -eq 0) {
                    Write-Host "   $updateCount item güncellendi..." -ForegroundColor Gray
                }
            } catch {
                Write-Host "⚠️  Güncelleme hatası (Order: $($update.order)): $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
        
        Write-Host "✅ $updateCount item'ın parentId'si güncellendi" -ForegroundColor Green
        Write-Host ""
    }
}

Write-Host "🎉 İşlem tamamlandı!" -ForegroundColor Green
Write-Host ""
