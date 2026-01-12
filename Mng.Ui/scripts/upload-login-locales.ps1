# PowerShell script to upload login namespace to MinIO via Locale Editor API
# This script reads the login namespace from local locale files and uploads them to MinIO
#
# Usage:
#   1. Make sure you're logged in to the application (token will be in cookie/localStorage)
#   2. Run this script from Mng.Ui directory: .\scripts\upload-login-locales.ps1
#
# Alternative: Use Locale Editor page in the UI (/apps/locale-editor)

param(
    [string]$KeeperUrl = "https://localhost:5001",
    [string]$GatewayUrl = "https://localhost:5040"
)

# Colors for output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-ColorOutput Green "=========================================="
Write-ColorOutput Green "Login Locale Upload Script"
Write-ColorOutput Green "=========================================="
Write-Host ""
Write-Host "This script will:"
Write-Host "  1. Read login namespace from local locale files (utils/locales/*.json)"
Write-Host "  2. Load existing locale files from MinIO"
Write-Host "  3. Merge login namespace into MinIO locale files"
Write-Host "  4. Upload updated locale files to MinIO"
Write-Host ""
Write-Host "Note: You need to be logged in to the application first."
Write-Host "      Token will be read from browser's localStorage or cookies."
Write-Host ""
Write-Host "Alternative: Use Locale Editor page at /apps/locale-editor"
Write-Host ""

# Prompt for token (user needs to copy from browser)
Write-ColorOutput Yellow "Please provide your authentication token:"
Write-Host "You can get it from browser DevTools > Application > Local Storage > access_token"
Write-Host "Or from browser DevTools > Application > Cookies > access_token"
Write-Host ""
$token = Read-Host "Enter token (or press Enter to skip and use Locale Editor manually)"

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-ColorOutput Yellow ""
    Write-ColorOutput Yellow "Token not provided. Please use Locale Editor page instead:"
    Write-ColorOutput Cyan "  1. Go to /apps/locale-editor in your browser"
    Write-ColorOutput Cyan "  2. Select each locale (tr, en, fr, ar, zh)"
    Write-ColorOutput Cyan "  3. Click 'Reload' to load from MinIO"
    Write-ColorOutput Cyan "  4. Add the 'login' namespace from local files"
    Write-ColorOutput Cyan "  5. Click 'Save' to upload to MinIO"
    Write-Host ""
    exit 0
}

Write-ColorOutput Green "Token provided, proceeding..."
Write-Host ""

# Locale files directory (relative to script location)
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$localesDir = Join-Path $scriptPath "..\utils\locales"
$locales = @("tr", "en", "fr", "ar", "zh")

# Function to load locale file from MinIO
function Get-LocaleFromMinIO {
    param(
        [string]$Locale,
        [string]$KeeperUrl,
        [string]$Token
    )
    
    # Try Gateway first, fallback to direct Keeper
    $url = "$GatewayUrl/keeper/api/system/locales/$Locale"
    
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -Headers @{
            "Authorization" = "Bearer $Token"
        } -SkipCertificateCheck
        return $response
    } catch {
        # Try direct Keeper if Gateway fails
        try {
            $url = "$KeeperUrl/api/system/locales/$Locale"
            $response = Invoke-RestMethod -Uri $url -Method Get -Headers @{
                "Authorization" = "Bearer $Token"
            } -SkipCertificateCheck
            return $response
        } catch {
            if ($_.Exception.Response.StatusCode -eq 404) {
                return @{}
            }
            throw
        }
    }
}

# Function to save locale file to MinIO
function Save-LocaleToMinIO {
    param(
        [string]$Locale,
        [object]$LocaleData,
        [string]$KeeperUrl,
        [string]$Token
    )
    
    # Try Gateway first, fallback to direct Keeper
    $url = "$GatewayUrl/keeper/api/system/locales/$Locale"
    $body = $LocaleData | ConvertTo-Json -Depth 100 -Compress
    
    try {
        Invoke-RestMethod -Uri $url -Method Put -Headers @{
            "Authorization" = "Bearer $Token"
            "Content-Type" = "application/json"
        } -Body $body -SkipCertificateCheck
        return $true
    } catch {
        # Try direct Keeper if Gateway fails
        try {
            $url = "$KeeperUrl/api/system/locales/$Locale"
            Invoke-RestMethod -Uri $url -Method Put -Headers @{
                "Authorization" = "Bearer $Token"
                "Content-Type" = "application/json"
            } -Body $body -SkipCertificateCheck
            return $true
        } catch {
            Write-ColorOutput Red "Error saving $Locale`: $($_.Exception.Message)"
            return $false
        }
    }
}

# Process each locale
foreach ($locale in $locales) {
    Write-ColorOutput Cyan "Processing locale: $locale"
    
    # Read local locale file
    $localFile = Join-Path $localesDir "$locale.json"
    if (-not (Test-Path $localFile)) {
        Write-ColorOutput Yellow "Warning: Local file not found: $localFile"
        continue
    }
    
    $localData = Get-Content $localFile -Raw | ConvertFrom-Json
    
    # Extract login namespace
    if (-not $localData.login) {
        Write-ColorOutput Yellow "Warning: 'login' namespace not found in $locale.json"
        continue
    }
    
    $loginNamespace = $localData.login
    
    # Load existing locale from MinIO
    Write-Host "  Loading existing locale from MinIO..."
    $minioData = Get-LocaleFromMinIO -Locale $locale -KeeperUrl $KeeperUrl -Token $token
    
    if ($null -eq $minioData) {
        $minioData = @{}
    }
    
    # Merge login namespace into MinIO data
    $minioData | Add-Member -MemberType NoteProperty -Name "login" -Value $loginNamespace -Force
    
    # Save to MinIO
    Write-Host "  Uploading to MinIO..."
    $success = Save-LocaleToMinIO -Locale $locale -LocaleData $minioData -KeeperUrl $KeeperUrl -Token $token
    
    if ($success) {
        Write-ColorOutput Green "  ✓ Successfully uploaded $locale locale"
    } else {
        Write-ColorOutput Red "  ✗ Failed to upload $locale locale"
    }
    
    Write-Host ""
}

Write-ColorOutput Green "=========================================="
Write-ColorOutput Green "Upload completed!"
Write-ColorOutput Green "=========================================="
Write-Host ""
Write-Host "Note: You may need to invalidate the locale cache in the application"
Write-Host "      or wait for the cache TTL (1 hour) to expire."
