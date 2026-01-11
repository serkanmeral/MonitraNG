# Clean all menu items and reload

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

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Menu Items Temizleme ve Yeniden Yükleme" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Tüm item'ları al
Write-Host "🔍 Mevcut item'lar alınıyor..." -ForegroundColor Cyan
try {
    $allItems = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu?limit=10000" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    if (-not $allItems -or $allItems.Count -eq 0) {
        Write-Host "ℹ️  Silinecek item yok" -ForegroundColor Yellow
    } else {
        Write-Host "📊 $($allItems.Count) item bulundu, siliniyor..." -ForegroundColor Cyan
        
        $deletedCount = 0
        foreach ($item in $allItems) {
            if ($item.__dataId) {
                try {
                    Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu/$($item.__dataId)" -Headers $headers -Method "DELETE" -SkipCertificateCheck -ErrorAction Stop | Out-Null
                    $deletedCount++
                } catch {
                    Write-Host "⚠️  Item silinemedi ($($item.__dataId)): $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
        }
        
        Write-Host "✅ $deletedCount item silindi" -ForegroundColor Green
        Write-Host ""
    }
} catch {
    Write-Host "⚠️  Item'lar alınamadı (devam ediliyor): $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "🔄 Export yeniden yapılıyor..." -ForegroundColor Cyan
node scripts/tests/MngDataGateway/side-menu/export-menu-tsx.mjs | Out-Null

Write-Host "✅ Export tamamlandı" -ForegroundColor Green
Write-Host ""

Write-Host "📤 Menu items yükleniyor..." -ForegroundColor Cyan
pwsh -ExecutionPolicy Bypass -File "scripts\tests\MngDataGateway\side-menu\load-menu-items-batch.ps1" -BatchSize 50

Write-Host ""
Write-Host "🎉 İşlem tamamlandı!" -ForegroundColor Green
