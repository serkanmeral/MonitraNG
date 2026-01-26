<#
.SYNOPSIS
  Proje kökünde çalıştırıldığında verilen dosya/klasörleri stage eder, commit ve push yapar.
  Cursor IDE içinden git komutları index.lock / .git yazma hataları verdiğinde bu script
  harici bir PowerShell penceresinde çalıştırılarak aynı işlem yapılır.

.DESCRIPTION
  - .git/index.lock varsa siler, sonra git add / commit / push çalıştırır.
  - Paths göreli veya mutlak olabilir; repo köküne göre çalışır.

.PARAMETER Paths
  Commit edilecek dosya/klasör yolları (repo köküne göre). Örn: "scripts/README_VERSION_BUMP.md","scripts/bump-versions.ps1","scripts/hooks/"

.PARAMETER Message
  Commit mesajı (Conventional Commits önerilir).

.PARAMETER Branch
  Push edilecek branch (varsayılan: main).

.PARAMETER Remote
  Remote adı (varsayılan: origin).

.EXAMPLE
  .\scripts\git-commit-push.ps1 -Paths "scripts/README_VERSION_BUMP.md","scripts/bump-versions.ps1","scripts/hooks/" -Message "chore: version bump otomasyonu - AutoCommit ve hook sablonlari"
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]] $Paths,
    [Parameter(Mandatory = $true)]
    [string]  $Message,
    [string]  $Branch = "main",
    [string]  $Remote = "origin"
)
# Tek string gelirse virgüle göre böl (örn. "a,b,c")
if ($Paths.Count -eq 1 -and $Paths[0] -match ',') {
    $Paths = $Paths[0] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
}

$ErrorActionPreference = "Stop"
$gitRoot = git rev-parse --show-toplevel 2>$null
if (-not $gitRoot) {
    Write-Host "Hata: Git repo kökü bulunamadı. Bu script proje kökünde veya altında çalıştırılmalı." -ForegroundColor Red
    exit 1
}
Set-Location $gitRoot

$lockPath = Join-Path $gitRoot ".git\index.lock"
if (Test-Path $lockPath) {
    try {
        Remove-Item -Force $lockPath -ErrorAction Stop
        Write-Host "index.lock kaldırıldı." -ForegroundColor Yellow
    } catch {
        Write-Host "Uyarı: index.lock silinemedi ($_). Devam ediliyor..." -ForegroundColor Yellow
    }
}

foreach ($p in $Paths) {
    $p = $p.Trim().Trim('"')
    if (-not $p) { continue }
    try {
        $full = if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $gitRoot ($p -replace '/', [IO.Path]::DirectorySeparatorChar) }
    } catch {
        $full = Join-Path $gitRoot ($p -replace '/', [IO.Path]::DirectorySeparatorChar)
    }
    if (-not (Test-Path $full)) {
        Write-Host "Uyarı: Yok sayılıyor: $p" -ForegroundColor Yellow
        continue
    }
    git add $p
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Hata: git add $p başarısız." -ForegroundColor Red
        exit 1
    }
}
Write-Host "Staged:" -ForegroundColor Cyan
git status --short

git commit -m "$Message"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Hata: git commit başarısız (staging boş veya hata)." -ForegroundColor Red
    exit 1
}

git push $Remote $Branch
if ($LASTEXITCODE -ne 0) {
    Write-Host "Hata: git push başarısız." -ForegroundColor Red
    exit 1
}
Write-Host "Commit ve push tamamlandı." -ForegroundColor Green
