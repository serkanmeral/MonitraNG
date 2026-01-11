# Update @side_menu Dataset - Add pageCode field
# Dataset'e pageCode field'ı ekle ve unique index oluştur

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "@side_menu Dataset Güncelleme" -ForegroundColor Cyan
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

# Dataset'i al
$datasetName = "@side_menu"
Write-Host "🔍 Mevcut dataset alınıyor: $datasetName" -ForegroundColor Cyan

try {
    $currentDataset = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets/$datasetName" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "✅ Dataset bulundu" -ForegroundColor Green
    Write-Host ""
    
    # pageCode field'ının zaten var olup olmadığını kontrol et
    $hasPageCode = $false
    if ($currentDataset.data.fields) {
        $hasPageCode = $currentDataset.data.fields | Where-Object { $_.name -eq "pageCode" } | Measure-Object | Select-Object -ExpandProperty Count -First 1
    }
    
    if ($hasPageCode -gt 0) {
        Write-Host "⚠️  pageCode field'ı zaten mevcut, güncelleniyor..." -ForegroundColor Yellow
    } else {
        Write-Host "➕ pageCode field'ı ekleniyor..." -ForegroundColor Cyan
    }
    
    # Mevcut fields'ı al (tümünü koru)
    $fields = @()
    if ($currentDataset.data.fields) {
        $fields = $currentDataset.data.fields | Where-Object { $_.name -ne "pageCode" }
    }
    
    # pageCode field'ını ekle (eğer yoksa)
    $hasPageCodeField = $fields | Where-Object { $_.name -eq "pageCode" } | Measure-Object | Select-Object -ExpandProperty Count
    if ($hasPageCodeField -eq 0) {
        $pageCodeField = @{
            fieldType = "text"
            name = "pageCode"
            title = "Sayfa Kodu"
            mandatory = $false
            validation = @{
                minLength = 1
                maxLength = 100
                pattern = "^[a-zA-Z0-9_-]+$"
            }
        }
        $fields += $pageCodeField
    }
    
    # pageCode için unique index ekle (fields Dictionary formatında: field name -> 1 for asc, -1 for desc)
    $pageCodeIndex = @{
        name = "idx_pageCode_unique"
        unique = $true
        fields = @{
            pageCode = 1
        }
    }
    
    # Mevcut index listesini al ve pageCode index'ini ekle (eğer yoksa)
    $existingIndexList = @()
    if ($currentDataset.data.indexList) {
        $existingIndexList = $currentDataset.data.indexList
    }
    
    # pageCode index'i zaten var mı kontrol et
    $hasPageCodeIndex = $existingIndexList | Where-Object { $_.name -eq "idx_pageCode_unique" } | Measure-Object | Select-Object -ExpandProperty Count
    
    if ($hasPageCodeIndex -eq 0) {
        $existingIndexList += $pageCodeIndex
        Write-Host "➕ pageCode unique index'i eklendi" -ForegroundColor Cyan
    } else {
        Write-Host "⚠️  pageCode index'i zaten mevcut" -ForegroundColor Yellow
    }
    
    # Dataset schema güncelleme (UpdateDatasetDto formatında - sadece güncellenecek field'lar)
    $updateSchema = @{
        Fields = $fields
        IndexList = $existingIndexList
    }
    
    # PUT request ile güncelle
    Write-Host "🔄 Dataset güncelleniyor..." -ForegroundColor Cyan
    
    $jsonBody = $updateSchema | ConvertTo-Json -Depth 20
    
    try {
        $updateResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets/$datasetName" -Headers $headers -Method "PUT" -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
        
        Write-Host "✅ Dataset başarıyla güncellendi!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📊 Eklenen field: pageCode (text, unique index)" -ForegroundColor Cyan
        Write-Host "📊 Eklenen index: idx_pageCode_unique (unique, pageCode)" -ForegroundColor Cyan
        Write-Host ""
        
    } catch {
        Write-Host "❌ Dataset güncelleme hatası: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "📦 Error Details:" -ForegroundColor Gray
            $_.ErrorDetails.Message | Write-Host
        }
        exit 1
    }
    
} catch {
    Write-Host "❌ Dataset alınamadı: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "📦 Error Details:" -ForegroundColor Gray
        $_.ErrorDetails.Message | Write-Host
    }
    exit 1
}

Write-Host "✅ Dataset güncelleme işlemi tamamlandı!" -ForegroundColor Green
Write-Host ""
