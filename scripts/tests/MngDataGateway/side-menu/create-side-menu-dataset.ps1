# Create @side_menu Dataset
# Faz 1.2: @side_menu dataset'ini oluştur

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "@side_menu Dataset Oluşturma" -ForegroundColor Cyan
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

# System Datasets category ID'yi yükle
$categoryIdFile = Join-Path $scriptPath "system-datasets-category-id.txt"
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
$datasetName = "@side_menu"
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
    Name = "@side_menu"
    Description = "Side menu items dataset - stores all menu items and their hierarchy"
    Category = $categoryId
    ForceSchema = $true
    Logging = "none"
    PublishMode = "none"
    fields = @(
        @{
            fieldType = "number"
            name = "order"
            title = "Sıralama"
            mandatory = $true
            validation = @{
                min = 0
            }
        },
        @{
            fieldType = "text"
            name = "itemType"
            title = "Item Tipi"
            mandatory = $true
            validation = @{
                pattern = "^(header|item)$"
            }
        },
        @{
            fieldType = "text"
            name = "header"
            title = "Header Metni"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "title"
            title = "Menü Başlığı"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "icon"
            title = "Icon Adı"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "iconType"
            title = "Icon Tipi"
            mandatory = $false
            validation = @{
                pattern = "^(mdi|tabler)$"
            }
        },
        @{
            fieldType = "text"
            name = "to"
            title = "Route Path"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "type"
            title = "Link Tipi"
            mandatory = $false
            validation = @{
                pattern = "^(internal|external)$"
            }
        },
        @{
            fieldType = "text"
            name = "pageType"
            title = "Sayfa Tipi"
            mandatory = $false
            validation = @{
                pattern = "^(user|manager|admin)$"
            }
        },
        @{
            fieldType = "relation"
            name = "parentId"
            title = "Parent Item"
            mandatory = $false
            relationDataset = "@side_menu"
            isArray = $false
        },
        @{
            fieldType = "number"
            name = "level"
            title = "Seviye"
            mandatory = $true
            validation = @{
                min = 0
                max = 10
            }
        },
        @{
            fieldType = "text"
            name = "chip"
            title = "Chip Metni"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "chipBgColor"
            title = "Chip Arka Plan Rengi"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "chipColor"
            title = "Chip Metin Rengi"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "chipVariant"
            title = "Chip Variant"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "chipIcon"
            title = "Chip Icon"
            mandatory = $false
        },
        @{
            fieldType = "bool"
            name = "disabled"
            title = "Devre Dışı"
            mandatory = $false
        },
        @{
            fieldType = "text"
            name = "subCaption"
            title = "Alt Başlık"
            mandatory = $false
        },
        @{
            fieldType = "object"
            name = "permissions"
            title = "Yetkilendirme"
            mandatory = $false
            isArray = $false
        }
    )
    indexList = @(
        @{
            name = "idx_order"
            fields = @{
                order = 1
            }
            unique = $false
        },
        @{
            name = "idx_parentId"
            fields = @{
                parentId = 1
                order = 1
            }
            unique = $false
        },
        @{
            name = "idx_level"
            fields = @{
                level = 1
                order = 1
            }
            unique = $false
        },
        @{
            name = "idx_itemType_level"
            fields = @{
                itemType = 1
                level = 1
                order = 1
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
    
    Write-Host "🎉 @side_menu dataset hazır!" -ForegroundColor Green
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
    if ($_.Exception.Response) {
        Write-Host "📦 Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "💡 Request Body (ilk 500 karakter):" -ForegroundColor Yellow
    $jsonBody.Substring(0, [Math]::Min(500, $jsonBody.Length)) | Write-Host
    Write-Host ""
    exit 1
}
