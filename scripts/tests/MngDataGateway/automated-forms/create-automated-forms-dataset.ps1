# Create @automated_forms Dataset
# Automated Forms dataset'ini oluştur

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "@automated_forms Dataset Oluşturma" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "http://localhost:5010"

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
# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
Write-Host ""

# System Datasets category ID'yi yükle
$categoryIdFile = Join-Path (Split-Path -Parent $scriptPath) "side-menu\system-datasets-category-id.txt"
if (-not (Test-Path $categoryIdFile)) {
    Write-Host "❌ System Datasets category ID dosyası bulunamadı: $categoryIdFile" -ForegroundColor Red
    Write-Host "   Önce create-system-datasets-category.ps1 script'ini çalıştırın!" -ForegroundColor Yellow
    exit 1
}

$categoryId = Get-Content -Path $categoryIdFile -Raw
$categoryId = $categoryId.Trim()

Write-Host "✅ Category ID yüklendi: $categoryId" -ForegroundColor Green
Write-Host ""

# Dataset'in var olup olmadığını kontrol et
$datasetName = "@automated_forms"
Write-Host "🔍 Dataset kontrol ediliyor: $datasetName" -ForegroundColor Cyan

try {
    $getResponse = Invoke-RestMethod -Uri "$baseUrl/api/v1/datasets/$datasetName" -Headers $headers -Method "GET" -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "✅ Dataset zaten mevcut: $datasetName" -ForegroundColor Green
    Write-Host "   Dataset oluşturulmuş, güncelleme yapılıyor..." -ForegroundColor Yellow
    Write-Host ""
    
    # Var olan dataset'i güncelle (PUT)
    $updateDataset = $true
} catch {
    if ($_.Exception.Message -like "*404*" -or $_.Exception.Message -like "*not found*") {
        Write-Host "📝 Dataset bulunamadı, yeni dataset oluşturulacak" -ForegroundColor Cyan
        $updateDataset = $false
    } else {
        Write-Host "⚠️  Dataset kontrolü başarısız: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "   Yeni dataset oluşturulacak..." -ForegroundColor Yellow
        $updateDataset = $false
    }
}

# Dataset schema
$datasetSchema = @{
    Name = "@automated_forms"
    Description = "Automated Forms dataset - stores form configurations for dynamic form generation"
    Category = $categoryId
    ForceSchema = $true
    Logging = "none"
    PublishMode = "none"
    fields = @(
        @{
            fieldType = "text"
            name = "formName"
            title = "Form Adı"
            mandatory = $true
            unique = $false
            validation = @{
                minLength = 3
                maxLength = 100
            }
        },
        @{
            fieldType = "text"
            name = "formCode"
            title = "Form Kodu"
            mandatory = $true
            unique = $true
            validation = @{
                pattern = "^[a-zA-Z0-9_-]+$"
                minLength = 3
                maxLength = 50
            }
        },
        @{
            fieldType = "text"
            name = "description"
            title = "Açıklama"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "datasetName"
            title = "Dataset Adı"
            mandatory = $true
            validation = @{
                pattern = "^@?[a-zA-Z][a-zA-Z0-9_-]*$"
            }
        },
        @{
            fieldType = "object"
            name = "sideMenuConfig"
            title = "Side Menu Ayarları"
            mandatory = $false
            isArray = $false
        },
        @{
            fieldType = "object"
            name = "listConfig"
            title = "Liste Ayarları"
            mandatory = $false
            isArray = $false
        },
        @{
            fieldType = "object"
            name = "formConfig"
            title = "Form Ayarları"
            mandatory = $false
            isArray = $false
        },
        @{
            fieldType = "bool"
            name = "isActive"
            title = "Aktif"
            mandatory = $true
        }
    )
    indexList = @(
        @{
            name = "idx_formCode"
            fields = @{
                formCode = 1
            }
            unique = $true
        },
        @{
            name = "idx_datasetName"
            fields = @{
                datasetName = 1
            }
            unique = $false
        },
        @{
            name = "idx_isActive"
            fields = @{
                isActive = 1
            }
            unique = $false
        }
    )
}

# Dataset oluştur veya güncelle
if ($updateDataset) {
    Write-Host "🔄 Dataset güncelleniyor..." -ForegroundColor Cyan
    $method = "PUT"
    $url = "$baseUrl/api/v1/datasets/$datasetName"
} else {
    Write-Host "📝 Dataset oluşturuluyor..." -ForegroundColor Cyan
    $method = "POST"
    $url = "$baseUrl/api/v1/datasets"
}

try {
    $jsonBody = $datasetSchema | ConvertTo-Json -Depth 20
    Write-Host "📤 Request gönderiliyor..." -ForegroundColor Gray
    Write-Host "   URL: $url" -ForegroundColor Gray
    Write-Host "   Method: $method" -ForegroundColor Gray
    Write-Host ""
    
    $response = Invoke-RestMethod -Uri $url -Headers $headers -Method $method -Body $jsonBody -SkipCertificateCheck -ErrorAction Stop
    
    Write-Host "✅ Dataset başarıyla $($method -eq 'POST' ? 'oluşturuldu' : 'güncellendi')!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Response:" -ForegroundColor Gray
    $response | ConvertTo-Json -Depth 10 | Write-Host
    Write-Host ""
    
    Write-Host "🎉 @automated_forms dataset hazır!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📋 Oluşturulan Field'lar:" -ForegroundColor Cyan
    Write-Host "   - formName (text, mandatory)" -ForegroundColor Gray
    Write-Host "   - formCode (text, mandatory, unique)" -ForegroundColor Gray
    Write-Host "   - description (text, optional)" -ForegroundColor Gray
    Write-Host "   - datasetName (text, mandatory)" -ForegroundColor Gray
    Write-Host "   - sideMenuConfig (object, optional)" -ForegroundColor Gray
    Write-Host "   - listConfig (object, optional)" -ForegroundColor Gray
    Write-Host "   - formConfig (object, optional)" -ForegroundColor Gray
    Write-Host "   - isActive (bool, mandatory, default: true)" -ForegroundColor Gray
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
    Write-Host "🔍 Troubleshooting:" -ForegroundColor Yellow
    Write-Host "   1. Token'ın geçerli olduğundan emin olun" -ForegroundColor Gray
    Write-Host "   2. System Datasets kategorisinin oluşturulduğundan emin olun" -ForegroundColor Gray
    Write-Host "   3. API'nin çalıştığından emin olun: $baseUrl" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
