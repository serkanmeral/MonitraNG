# GitHub Actions Self-Hosted Runner Kurulum Script
# Windows için

param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory=$false)]
    [string]$RunnerName = "local-runner",
    [Parameter(Mandatory=$false)]
    [string]$RunnerWorkFolder = "C:\actions-runner"
)

Write-Host "`n=== GitHub Actions Runner Kurulumu ===" -ForegroundColor Cyan

# 1. Runner klasörü oluştur
Write-Host "`n1. Runner klasörü oluşturuluyor..." -ForegroundColor Yellow
if (-not (Test-Path $RunnerWorkFolder)) {
    New-Item -ItemType Directory -Path $RunnerWorkFolder -Force | Out-Null
    Write-Host "  ✅ Klasör oluşturuldu: $RunnerWorkFolder" -ForegroundColor Green
} else {
    Write-Host "  ℹ️  Klasör zaten mevcut: $RunnerWorkFolder" -ForegroundColor Gray
}

# 2. Runner indir
Write-Host "`n2. Runner indiriliyor..." -ForegroundColor Yellow
$runnerUrl = "https://github.com/actions/runner/releases/latest/download/actions-runner-win-x64-2.311.0.zip"
$zipPath = Join-Path $RunnerWorkFolder "runner.zip"

try {
    Invoke-WebRequest -Uri $runnerUrl -OutFile $zipPath
    Write-Host "  ✅ Runner indirildi" -ForegroundColor Green
} catch {
    Write-Host "  ❌ İndirme hatası: $_" -ForegroundColor Red
    exit 1
}

# 3. Zip'i çıkar
Write-Host "`n3. Zip dosyası çıkarılıyor..." -ForegroundColor Yellow
try {
    Expand-Archive -Path $zipPath -DestinationPath $RunnerWorkFolder -Force
    Remove-Item $zipPath -Force
    Write-Host "  ✅ Zip çıkarıldı" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Çıkarma hatası: $_" -ForegroundColor Red
    exit 1
}

# 4. Runner yapılandır
Write-Host "`n4. Runner yapılandırılıyor..." -ForegroundColor Yellow
Push-Location $RunnerWorkFolder

try {
    .\config.cmd --url https://github.com/serkanmeral/MonitraNG --token $GitHubToken --name $RunnerName --work "_work" --unattended
    Write-Host "  ✅ Runner yapılandırıldı" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Yapılandırma hatası: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# 5. Servis olarak kur (opsiyonel)
Write-Host "`n5. Servis olarak kuruluyor mu? (Y/N)" -ForegroundColor Yellow
$installService = Read-Host

if ($installService -eq "Y" -or $installService -eq "y") {
    Push-Location $RunnerWorkFolder
    
    try {
        # Yönetici yetkisi kontrolü
        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        
        if (-not $isAdmin) {
            Write-Host "  ⚠️  Servis kurulumu için yönetici yetkisi gereklidir!" -ForegroundColor Yellow
            Write-Host "  ℹ️  Manuel olarak çalıştırmak için: .\run.cmd" -ForegroundColor Gray
        } else {
            .\svc.cmd install
            .\svc.cmd start
            Write-Host "  ✅ Runner servis olarak kuruldu ve başlatıldı" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ❌ Servis kurulum hatası: $_" -ForegroundColor Red
    }
    
    Pop-Location
} else {
    Write-Host "  ℹ️  Runner'ı manuel çalıştırmak için:" -ForegroundColor Gray
    Write-Host "     cd $RunnerWorkFolder" -ForegroundColor Gray
    Write-Host "     .\run.cmd" -ForegroundColor Gray
}

Write-Host "`n=== Kurulum Tamamlandı ===" -ForegroundColor Green
Write-Host "`nRunner bilgileri:" -ForegroundColor Cyan
Write-Host "  - Klasör: $RunnerWorkFolder" -ForegroundColor Gray
Write-Host "  - İsim: $RunnerName" -ForegroundColor Gray
Write-Host "  - Repository: serkanmeral/MonitraNG" -ForegroundColor Gray

Write-Host "`nSonraki adımlar:" -ForegroundColor Cyan
Write-Host "  1. GitHub repository'de Settings > Actions > Runners bölümünden runner'ı kontrol et" -ForegroundColor Gray
Write-Host "  2. Workflow'ları test et: git push origin main" -ForegroundColor Gray

