# Simple HTTP Server for test-signalr.html
# This serves the HTML file over HTTP so CORS works properly

$port = 8080
$htmlFile = Join-Path $PSScriptRoot "test-signalr.html"

Write-Host "=== Test SignalR HTML Server ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "HTML dosyası sunuluyor:" -ForegroundColor Yellow
Write-Host "  http://localhost:$port/test-signalr.html" -ForegroundColor Green
Write-Host ""
Write-Host "Tarayıcıda şu adresi açın:" -ForegroundColor Yellow
Write-Host "  http://localhost:$port/test-signalr.html" -ForegroundColor Green
Write-Host ""
Write-Host "Durdurmak için Ctrl+C tuşlarına basın" -ForegroundColor Gray
Write-Host ""

# Check if Python is available
$pythonAvailable = $false
try {
    $pythonVersion = python --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        $pythonAvailable = $true
        Write-Host "Python bulundu, Python HTTP server kullanılıyor..." -ForegroundColor Green
        Write-Host ""
        Set-Location $PSScriptRoot
        python -m http.server $port
    }
} catch {
    # Python not available, try Node.js
    try {
        $nodeVersion = node --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Node.js bulundu, Node.js HTTP server kullanılıyor..." -ForegroundColor Green
            Write-Host ""
            Set-Location $PSScriptRoot
            npx --yes http-server -p $port -c-1
        }
    } catch {
        Write-Host "Python veya Node.js bulunamadı!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Alternatif çözümler:" -ForegroundColor Yellow
        Write-Host "1. Python kurun: https://www.python.org/downloads/" -ForegroundColor White
        Write-Host "2. Node.js kurun: https://nodejs.org/" -ForegroundColor White
        Write-Host "3. VS Code Live Server extension kullanın" -ForegroundColor White
        Write-Host "4. HTML dosyasını doğrudan MngHub API'sine ekleyin" -ForegroundColor White
        Write-Host ""
        exit 1
    }
}

