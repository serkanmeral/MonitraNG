# Common Token Loading Script
# This script loads a token from the common location or retrieves a new one if needed
# Usage: $token = .\load-token.ps1
# Or: . .\load-token.ps1 (to load $token variable into current scope)

param(
    [string]$TokenFile = "$env:TEMP\serkan_token.txt",
    [switch]$AutoRefresh = $false
)

# Function to get token
function Get-AuthToken {
    $scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
    if ([string]::IsNullOrEmpty($scriptPath)) {
        $scriptPath = Get-Location
    }
    $getTokenScript = Join-Path $scriptPath "get-token.ps1"
    
    if (Test-Path $getTokenScript) {
        return & $getTokenScript
    } else {
        Write-Host "❌ get-token.ps1 bulunamadı! Path: $getTokenScript" -ForegroundColor Red
        return $null
    }
}

# Function to check if token is expired
function Test-TokenExpired {
    param([string]$token)
    
    if ([string]::IsNullOrEmpty($token)) {
        return $true
    }
    
    try {
        # JWT token'ı decode et (base64)
        $parts = $token.Split('.')
        if ($parts.Length -lt 2) {
            return $true
        }
        
        # Payload'ı decode et
        $payload = $parts[1]
        # Base64 padding ekle
        while ($payload.Length % 4 -ne 0) {
            $payload += "="
        }
        
        $payloadBytes = [System.Convert]::FromBase64String($payload)
        $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
        $payloadObj = $payloadJson | ConvertFrom-Json
        
        # Expiration time kontrolü
        if ($payloadObj.exp) {
            $expirationTime = [DateTimeOffset]::FromUnixTimeSeconds($payloadObj.exp).DateTime
            $now = [DateTime]::UtcNow
            
            # 5 dakika önceden yenile
            if ($expirationTime -lt $now.AddMinutes(5)) {
                return $true
            }
        }
        
        return $false
    } catch {
        # Decode hatası varsa token geçersiz sayılır
        return $true
    }
}

# Try to load existing token
$token = $null
if (Test-Path $TokenFile) {
    try {
        $token = Get-Content $TokenFile -Raw -ErrorAction Stop
        $token = $token.Trim()
        
        if (-not [string]::IsNullOrEmpty($token)) {
            # Token'ın süresi dolmuş mu kontrol et
            if (Test-TokenExpired $token) {
                Write-Host "🔄 Token süresi dolmuş, yeni token alınıyor..." -ForegroundColor Yellow
                $token = $null
            } else {
                Write-Host "✅ Token dosyadan yüklendi" -ForegroundColor Green
                $global:Token = $token
                return $token
            }
        }
    } catch {
        Write-Host "⚠️  Token dosyası okunamadı: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Token yoksa veya boşsa yeni token al
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "🔄 Token bulunamadı, yeni token alınıyor..." -ForegroundColor Yellow
    $token = Get-AuthToken
    
    if ([string]::IsNullOrEmpty($token)) {
        Write-Host "❌ Token alınamadı!" -ForegroundColor Red
        $global:Token = $null
        return $null
    }
}

$global:Token = $token
return $token

