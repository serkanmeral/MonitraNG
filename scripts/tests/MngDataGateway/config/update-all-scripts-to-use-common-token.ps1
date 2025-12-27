# Script to update all test scripts to use common token scripts
# This script finds all test scripts and updates them to use load-token.ps1

$testScripts = Get-ChildItem -Path $PSScriptRoot -Filter "test-*.ps1" -File

$tokenLoadCode = @'
# Token'ı yükle (ortak script kullanarak)
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
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$tokenFile = "$env:TEMP\serkan_token.txt"
'@

$patternsToReplace = @(
    @{
        Pattern = '(?s)# Token dosyasının yolunu belirle.*?Token'ı oku.*?\$token = \$token\.Trim\(\)'
        Replacement = $tokenLoadCode
    }
)

Write-Host "📝 Test scriptleri güncelleniyor...`n" -ForegroundColor Cyan

foreach ($script in $testScripts) {
    $content = Get-Content $script.FullName -Raw
    $originalContent = $content
    
    # Skip if already using load-token.ps1
    if ($content -match "load-token\.ps1") {
        Write-Host "⏭️  $($script.Name) - Zaten güncellenmiş" -ForegroundColor Gray
        continue
    }
    
    # Skip setup scripts
    if ($script.Name -match "setup|insert|update-all") {
        Write-Host "⏭️  $($script.Name) - Setup script, atlanıyor" -ForegroundColor Gray
        continue
    }
    
    # Try to replace token loading code
    $updated = $false
    foreach ($pattern in $patternsToReplace) {
        if ($content -match $pattern.Pattern) {
            $content = $content -replace $pattern.Pattern, $pattern.Replacement
            $updated = $true
            break
        }
    }
    
    if ($updated) {
        Set-Content -Path $script.FullName -Value $content -NoNewline
        Write-Host "✅ $($script.Name) - Güncellendi" -ForegroundColor Green
    } else {
        Write-Host "⚠️  $($script.Name) - Token yükleme kodu bulunamadı (manuel güncelleme gerekebilir)" -ForegroundColor Yellow
    }
}

Write-Host "`n✅ Güncelleme tamamlandı!`n" -ForegroundColor Green

