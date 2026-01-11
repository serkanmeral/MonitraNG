# Create System Datasets Category
# Faz 1.1: System Datasets kategorisini oluştur

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "System Datasets Kategorisi Oluşturma" -ForegroundColor Cyan
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

# Kategori bilgileri
$categoryName = "System Datasets"
$categoryDescription = "System-level datasets for application configuration (e.g., side menu, settings)"

# Önce kategorinin var olup olmadığını kontrol et
Write-Host "🔍 Mevcut kategoriler kontrol ediliyor..." -ForegroundColor Cyan
try {
    $listResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/dataset-categories?pageNumber=1&pageSize=100" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    $existingCategory = $listResponse.data | Where-Object { $_.categoryName -eq $categoryName }
    
    if ($existingCategory) {
        Write-Host "✅ Kategori zaten mevcut: $categoryName" -ForegroundColor Green
        Write-Host "   __dataId: $($existingCategory.__dataId)" -ForegroundColor Gray
        Write-Host ""
        
        # Category ID'yi dosyaya kaydet (sonraki script'ler için)
        $categoryIdFile = Join-Path $scriptPath "system-datasets-category-id.txt"
        $existingCategory.__dataId | Out-File -FilePath $categoryIdFile -Encoding UTF8 -NoNewline
        Write-Host "💾 Category ID kaydedildi: $categoryIdFile" -ForegroundColor Green
        Write-Host ""
        
        exit 0
    }
} catch {
    Write-Host "⚠️  Kategori listesi alınamadı, yeni kategori oluşturulacak: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
}

# Yeni kategori oluştur
Write-Host "📝 Yeni kategori oluşturuluyor: $categoryName" -ForegroundColor Cyan
$createBody = @{
    CategoryName = $categoryName
    CategoryDescription = $categoryDescription
}

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/v1/dataset-categories" -Headers $headers -Method "POST" -Body ($createBody | ConvertTo-Json -Depth 10) -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "✅ Kategori başarıyla oluşturuldu!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Response:" -ForegroundColor Gray
    $response | ConvertTo-Json -Depth 10 | Write-Host
    Write-Host ""
    
    # Category ID'yi dosyaya kaydet (sonraki script'ler için)
    $categoryId = $response.dataId
    $categoryIdFile = Join-Path $scriptPath "system-datasets-category-id.txt"
    $categoryId | Out-File -FilePath $categoryIdFile -Encoding UTF8 -NoNewline
    Write-Host "💾 Category ID kaydedildi: $categoryIdFile" -ForegroundColor Green
    Write-Host "   Category ID: $categoryId" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "🎉 System Datasets kategorisi hazır!" -ForegroundColor Green
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
