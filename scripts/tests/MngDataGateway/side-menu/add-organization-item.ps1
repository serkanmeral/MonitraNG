# Add Organization Menu Item to @side_menu Dataset
# Organizasyon sayfası için menü öğesi ekler

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Organization Menu Item Ekleme" -ForegroundColor Cyan
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

# "Apps" veya "Assets" header'ını bul
$targetHeaderId = $null
$targetHeader = $allItems | Where-Object {
    $_.itemType -eq "header" -and
    ($_.header -eq "Apps" -or $_.header -eq "Applications" -or $_.header -eq "Assets")
} | Select-Object -First 1

if ($targetHeader) {
    $targetHeaderId = $targetHeader.__dataId
    Write-Host "✅ Header bulundu: '$($targetHeader.header)' ($targetHeaderId, order: $($targetHeader.order))" -ForegroundColor Green
    $headerItems = $allItems | Where-Object { $_.parentId -eq $targetHeaderId } | Sort-Object -Property order
    $maxOrderInHeader = if ($headerItems) {
        ($headerItems | Measure-Object -Property order -Maximum).Maximum
    } else {
        $targetHeader.order
    }
    $newOrder = $maxOrderInHeader + 1
} else {
    # Root level'a ekle (parentId null)
    Write-Host "⚠️  Apps/Assets header bulunamadı. Root level'a eklenecek." -ForegroundColor Yellow
    $maxRootOrder = ($allItems | Where-Object { $null -eq $_.parentId } | Measure-Object -Property order -Maximum).Maximum
    $newOrder = $maxRootOrder + 1
}

Write-Host "📌 Yeni item order: $newOrder" -ForegroundColor Cyan
Write-Host ""

# Mevcut "Organizasyon" / "Organization" item'ı var mı?
$existingItem = $allItems | Where-Object {
    ($_.title -eq "Organizasyon" -or $_.title -eq "Organization") -and
    $_.itemType -eq "item" -and
    $_.to -eq "/apps/monitoring/organization"
} | Select-Object -First 1

if ($existingItem) {
    Write-Host "⚠️  'Organizasyon' menu item zaten mevcut!" -ForegroundColor Yellow
    Write-Host "   DataId: $($existingItem.__dataId)" -ForegroundColor Gray
    Write-Host "   Route: $($existingItem.to)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "✅ Organizasyon menüde zaten tanımlı. Sayfayı yenileyin." -ForegroundColor Green
    exit 0
}

# Yeni item oluştur
$organizationItem = @{
    order = $newOrder
    itemType = "item"
    title = "Organizasyon"
    icon = "SitemapIcon"
    iconType = "tabler"
    to = "/apps/monitoring/organization"
    type = "internal"
    level = if ($targetHeaderId) { 1 } else { 0 }
    parentId = if ($targetHeaderId) { $targetHeaderId } else { $null }
    pageType = "manager"
    pageCode = "apps-organization"
    disabled = $false
}

try {
    $jsonBody = $organizationItem | ConvertTo-Json -Depth 10
    Write-Host "📤 Organizasyon menu item ekleniyor..." -ForegroundColor Gray
    Write-Host "   Title: Organizasyon" -ForegroundColor Gray
    Write-Host "   Route: /apps/monitoring/organization" -ForegroundColor Gray
    Write-Host "   Icon: SitemapIcon (tabler)" -ForegroundColor Gray
    Write-Host "   Page Type: manager" -ForegroundColor Gray
    Write-Host ""

    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/data/@side_menu" -Headers $headers -Method "POST" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop

    Write-Host "✅ Organizasyon menu item başarıyla eklendi!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🎉 Organizasyon link'i menüye eklendi!" -ForegroundColor Green
    Write-Host "   Route: /apps/monitoring/organization" -ForegroundColor Cyan
    Write-Host "   Sayfayı yenileyerek menüde görebilirsiniz." -ForegroundColor Cyan
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
