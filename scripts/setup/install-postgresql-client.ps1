# PostgreSQL Client Tools Installation Script
# Installs pg_dump and other PostgreSQL client utilities using Chocolatey

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PostgreSQL Client Tools Kurulumu" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "⚠ UYARI: Bu script yönetici yetkisi gerektirir!" -ForegroundColor Yellow
    Write-Host "PowerShell'i 'Yönetici olarak çalıştır' seçeneğiyle açın." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternatif: Manuel kurulum için aşağıdaki komutu çalıştırın:" -ForegroundColor Cyan
    Write-Host "  choco install postgresql --params '/Password:yourpassword'" -ForegroundColor White
    Write-Host ""
    Write-Host "Veya sadece client tools için:" -ForegroundColor Cyan
    Write-Host "  choco install postgresql15 --params '/NoPassword'" -ForegroundColor White
    exit 1
}

# Check if Chocolatey is installed
if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Write-Host "✗ Chocolatey kurulu değil!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Chocolatey'yi kurmak için:" -ForegroundColor Yellow
    Write-Host "  Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))" -ForegroundColor White
    exit 1
}

Write-Host "✓ Chocolatey kurulu" -ForegroundColor Green
Write-Host ""

# Install PostgreSQL client tools (without full server)
Write-Host "PostgreSQL client tools kuruluyor..." -ForegroundColor Cyan
Write-Host "Bu işlem birkaç dakika sürebilir..." -ForegroundColor Yellow
Write-Host ""

try {
    # Install PostgreSQL 15 client tools (lightweight, no server)
    choco install postgresql15 --params '/NoPassword' -y
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✓ PostgreSQL client tools başarıyla kuruldu!" -ForegroundColor Green
        Write-Host ""
        
        # Refresh PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
        
        # Verify installation
        Write-Host "Kurulum doğrulanıyor..." -ForegroundColor Cyan
        Start-Sleep -Seconds 2
        
        $pgDumpPath = Get-Command pg_dump -ErrorAction SilentlyContinue
        if ($pgDumpPath) {
            Write-Host "✓ pg_dump bulundu: $($pgDumpPath.Source)" -ForegroundColor Green
            Write-Host ""
            Write-Host "Versiyon kontrolü:" -ForegroundColor Cyan
            & pg_dump --version
        } else {
            Write-Host "⚠ pg_dump bulunamadı. PATH'i yenilemek için PowerShell'i yeniden başlatın." -ForegroundColor Yellow
        }
    } else {
        Write-Host "✗ Kurulum başarısız oldu!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Hata: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Kurulum Tamamlandı!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Not: Eğer pg_dump komutu çalışmazsa, PowerShell'i yeniden başlatın." -ForegroundColor Yellow
