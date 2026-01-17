# MkDocs Çalıştırma Script'i
# Microsoft Store Python'unu kullanır

$pythonPath = "$env:LOCALAPPDATA\Microsoft\WindowsApps\python.exe"

if (Test-Path $pythonPath) {
    Write-Host "MkDocs başlatılıyor..." -ForegroundColor Green
    & $pythonPath -m mkdocs serve --dev-addr=127.0.0.1:6010
} else {
    Write-Host "Python bulunamadı!" -ForegroundColor Red
    Write-Host "Lütfen Microsoft Store'dan Python'u yükleyin." -ForegroundColor Yellow
}
