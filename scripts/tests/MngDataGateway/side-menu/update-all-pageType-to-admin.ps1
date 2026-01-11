# Update all menu items pageType to 'admin'

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
Write-Host "PageType Güncelleme (admin)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Tüm item'ları al
Write-Host "🔍 Tüm item'lar alınıyor..." -ForegroundColor Cyan
$allItems = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu?limit=10000" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop

Write-Host "📊 $($allItems.Count) item bulundu" -ForegroundColor Green
Write-Host ""

# pageType'ı 'admin' olmayan item'ları güncelle
$itemsToUpdate = $allItems | Where-Object { $_.pageType -ne "admin" }
$updateCount = 0

Write-Host "📝 Güncellenecek item sayısı: $($itemsToUpdate.Count)" -ForegroundColor Cyan
Write-Host ""

if ($itemsToUpdate.Count -eq 0) {
    Write-Host "✅ Tüm item'lar zaten 'admin' pageType'ına sahip!" -ForegroundColor Green
    exit 0
}

foreach ($item in $itemsToUpdate) {
    try {
        $updateBody = @{
            pageType = "admin"
        } | ConvertTo-Json
        
        Invoke-RestMethod `
            -Uri "$baseUrl/api/v1/data/@side_menu/$($item.__dataId)" `
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
        Write-Host "⚠️  Güncelleme hatası (Order: $($item.order), $($item.title ?? $item.header)): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "✅ $updateCount item'ın pageType'ı 'admin' olarak güncellendi!" -ForegroundColor Green
Write-Host ""
