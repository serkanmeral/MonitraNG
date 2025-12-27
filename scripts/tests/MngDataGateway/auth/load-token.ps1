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

# Try to load existing token
$token = $null
if (Test-Path $TokenFile) {
    try {
        $token = Get-Content $TokenFile -Raw -ErrorAction Stop
        $token = $token.Trim()
        
        if (-not [string]::IsNullOrEmpty($token)) {
            Write-Host "✅ Token dosyadan yüklendi" -ForegroundColor Green
            return $token
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
        return $null
    }
}

return $token

