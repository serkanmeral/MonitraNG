# Get App Header and Child Items from @side_menu Dataset
# "App" header'ı ve altındaki tüm item'ları getirir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "App Menu Items Query" -ForegroundColor Cyan
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

# Comprehensive SSL/TLS fixes
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls
} catch {
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    } catch {
        # Use default if TLS12 not available
    }
}

# 1. Önce "App" header'ını bul
Write-Host "🔍 'App' header'ı aranıyor..." -ForegroundColor Yellow
Write-Host ""

$queryUrl = "$baseUrl/api/v1/data/@side_menu/query"

# Query body: header = "App" ve itemType = "header" olan kayıtları bul
$queryBody = @{
    filter = @{
        header = "App"
        itemType = "header"
    }
    page = 1
    pageSize = 10
} | ConvertTo-Json -Depth 10

Write-Host "📤 Query Request:" -ForegroundColor Cyan
Write-Host ($queryBody | ConvertFrom-Json | ConvertTo-Json -Depth 10) -ForegroundColor Gray
Write-Host ""

try {
    $params = @{
        Uri = $queryUrl
        Method = "POST"
        Headers = $headers
        Body = $queryBody
        ErrorAction = "Stop"
    }
    
    if (Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
        $params.SkipCertificateCheck = $true
    }
    
    $response = Invoke-RestMethod @params
    
    if ($response.items -and $response.items.Count -gt 0) {
        $appHeader = $response.items[0]
        $appHeaderId = $appHeader.__dataId
        
        Write-Host "✅ 'App' header bulundu!" -ForegroundColor Green
        Write-Host "   __dataId: $appHeaderId" -ForegroundColor Gray
        Write-Host "   Header: $($appHeader.header)" -ForegroundColor Gray
        Write-Host "   Order: $($appHeader.order)" -ForegroundColor Gray
        Write-Host "   Level: $($appHeader.level)" -ForegroundColor Gray
        Write-Host ""
        
        # 2. App header'ının altındaki tüm item'ları bul (parentId = appHeaderId)
        Write-Host "🔍 'App' header'ının altındaki item'lar aranıyor..." -ForegroundColor Yellow
        Write-Host ""
        
        $childrenQueryBody = @{
            filter = @{
                parentId = $appHeaderId
            }
            page = 1
            pageSize = 1000
            sort = @(
                @{
                    field = "order"
                    direction = "asc"
                }
            )
        } | ConvertTo-Json -Depth 10
        
        Write-Host "📤 Children Query Request:" -ForegroundColor Cyan
        Write-Host ($childrenQueryBody | ConvertFrom-Json | ConvertTo-Json -Depth 10) -ForegroundColor Gray
        Write-Host ""
        
        $childrenResponse = Invoke-RestMethod @params -Body $childrenQueryBody
        
        $childrenItems = @()
        if ($childrenResponse.items) {
            $childrenItems = $childrenResponse.items
        }
        
        Write-Host "✅ Toplam $($childrenItems.Count) child item bulundu" -ForegroundColor Green
        Write-Host ""
        
        # 3. Sonuçları göster
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "APP HEADER DETAYLARI" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host ($appHeader | ConvertTo-Json -Depth 10) -ForegroundColor White
        Write-Host ""
        
        if ($childrenItems.Count -gt 0) {
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host "APP HEADER ALTINDAKI ITEM'LAR ($($childrenItems.Count) adet)" -ForegroundColor Cyan
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host ""
            
            foreach ($item in $childrenItems) {
                Write-Host "📄 Item: $($item.title)" -ForegroundColor Yellow
                Write-Host "   __dataId: $($item.__dataId)" -ForegroundColor Gray
                Write-Host "   Order: $($item.order)" -ForegroundColor Gray
                Write-Host "   Level: $($item.level)" -ForegroundColor Gray
                Write-Host "   ParentId: $($item.parentId)" -ForegroundColor Gray
                Write-Host "   ItemType: $($item.itemType)" -ForegroundColor Gray
                Write-Host "   PageType: $($item.pageType)" -ForegroundColor Gray
                if ($item.to) {
                    Write-Host "   Route: $($item.to)" -ForegroundColor Gray
                }
                Write-Host ""
            }
            
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host "TÜM CHILD ITEM'LAR (JSON)" -ForegroundColor Cyan
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host ""
            Write-Host ($childrenItems | ConvertTo-Json -Depth 10) -ForegroundColor White
            Write-Host ""
        } else {
            Write-Host "⚠️  'App' header'ının altında item bulunamadı" -ForegroundColor Yellow
            Write-Host ""
        }
        
        # 4. Tüm veriyi birleştirilmiş olarak göster
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "BİRLEŞTİRİLMİŞ VERİ (Header + Children)" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        
        $combinedData = @{
            header = $appHeader
            children = $childrenItems
            totalChildren = $childrenItems.Count
        }
        
        Write-Host ($combinedData | ConvertTo-Json -Depth 10) -ForegroundColor White
        Write-Host ""
        
    } else {
        Write-Host "❌ 'App' header bulunamadı!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Response:" -ForegroundColor Yellow
        Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
        Write-Host ""
    }
    
} catch {
    Write-Host "❌ Hata oluştu!" -ForegroundColor Red
    Write-Host "   Hata: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   Status Code: $statusCode" -ForegroundColor Red
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Response Body: $responseBody" -ForegroundColor Red
        } catch {
            # Ignore stream read errors
        }
    }
    
    Write-Host ""
    exit 1
}

Write-Host "✅ İşlem tamamlandı" -ForegroundColor Green
Write-Host ""
