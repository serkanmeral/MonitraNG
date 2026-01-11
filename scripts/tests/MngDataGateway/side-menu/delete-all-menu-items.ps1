# Delete All Menu Items from @side_menu Dataset
# Tüm menu item'larını sil (temizlik için)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Menu Items Silme" -ForegroundColor Cyan
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

# Tüm menu items'ı al
$datasetName = "@side_menu"
Write-Host "🔍 Tüm menu items alınıyor..." -ForegroundColor Cyan

try {
    # Tüm item'ları al (limit yok, tümünü al)
    $allItems = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu?limit=10000" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    # Response formatını kontrol et
    $itemsToDelete = @()
    if ($allItems -is [Array]) {
        $itemsToDelete = $allItems
    } elseif ($allItems.data -and $allItems.data -is [Array]) {
        $itemsToDelete = $allItems.data
    } elseif ($allItems.items -and $allItems.items -is [Array]) {
        $itemsToDelete = $allItems.items
    }
    
    if (-not $itemsToDelete -or $itemsToDelete.Count -eq 0) {
        Write-Host "ℹ️  Silinecek menu item bulunamadı" -ForegroundColor Yellow
        Write-Host "   Response formatı: $($allItems.GetType().Name)" -ForegroundColor Gray
        if ($allItems | Get-Member -MemberType Properties) {
            Write-Host "   Response keys: $($allItems | Get-Member -MemberType Properties | Select-Object -ExpandProperty Name | Join-String -Separator ', ')" -ForegroundColor Gray
        }
        exit 0
    }
    
    Write-Host "📊 Bulunan item sayısı: $($itemsToDelete.Count)" -ForegroundColor Cyan
    Write-Host ""
    
    # Onay iste
    Write-Host "⚠️  UYARI: $($itemsToDelete.Count) menu item silinecek!" -ForegroundColor Red
    Write-Host "   Devam etmek istiyor musunuz? (E/H)" -ForegroundColor Yellow
    $confirmation = Read-Host
    
    if ($confirmation -ne "E" -and $confirmation -ne "e" -and $confirmation -ne "Y" -and $confirmation -ne "y") {
        Write-Host "❌ İşlem iptal edildi" -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    Write-Host "🗑️  Menu items siliniyor..." -ForegroundColor Cyan
    
    $deletedCount = 0
    $errorCount = 0
    
    foreach ($item in $itemsToDelete) {
        $itemId = $item.__dataId
        if (-not $itemId) {
            Write-Host "⚠️  Item'da __dataId bulunamadı, atlanıyor" -ForegroundColor Yellow
            continue
        }
        
        try {
            $deleteResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/$itemId" -Headers $headers -Method "DELETE" -SkipCertificateCheck -ErrorAction Stop
            $deletedCount++
            
            if ($deletedCount % 10 -eq 0) {
                Write-Host "   $deletedCount item silindi..." -ForegroundColor Gray
            }
        } catch {
            $errorCount++
            Write-Host "⚠️  Item silinemedi ($itemId): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "✅ Silme işlemi tamamlandı!" -ForegroundColor Green
    Write-Host "   Başarılı: $deletedCount" -ForegroundColor Green
    if ($errorCount -gt 0) {
        Write-Host "   Hata: $errorCount" -ForegroundColor Red
    }
    Write-Host ""
    
} catch {
    Write-Host "❌ Menu items alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "📦 Error Details:" -ForegroundColor Gray
        $_.ErrorDetails.Message | Write-Host
    }
    exit 1
}
