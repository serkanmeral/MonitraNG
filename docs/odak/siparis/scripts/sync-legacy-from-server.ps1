# Sync legacy Kalite app + DB dump from 192.168.20.30 for local Docker
# Usage: .\sync-legacy-from-server.ps1 [-DockerDir "C:\Users\...\kalite-legacy-docker"]

param(
    [string]$Server = "192.168.20.30",
    [string]$User = "odak",
    [string]$Password = "Odak333221",
    [string]$DockerDir = ""
)

$ErrorActionPreference = "Stop"

if (-not (Get-Module -ListAvailable Posh-SSH)) {
    throw "Posh-SSH module required. Install: Install-Module Posh-SSH -Scope CurrentUser"
}

$templateDir = Join-Path $PSScriptRoot "..\docker"
$templateDir = (Resolve-Path $templateDir).Path

if ([string]::IsNullOrWhiteSpace($DockerDir)) {
    $DockerDir = Join-Path $env:USERPROFILE "kalite-legacy-docker"
}

Write-Host "Docker runtime dir: $DockerDir"

@("app", "uploads", "db\init", "tmp") | ForEach-Object {
    New-Item -ItemType Directory -Force -Path (Join-Path $DockerDir $_) | Out-Null
}

# Copy compose templates
Copy-Item (Join-Path $templateDir "docker-compose.yml") $DockerDir -Force
Copy-Item (Join-Path $templateDir "Dockerfile") $DockerDir -Force
Copy-Item (Join-Path $templateDir ".gitignore") $DockerDir -Force
if (-not (Test-Path (Join-Path $DockerDir ".env"))) {
    Copy-Item (Join-Path $templateDir ".env.example") (Join-Path $DockerDir ".env") -Force
}

$pass = ConvertTo-SecureString $Password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($User, $pass)

Write-Host "Downloading html.zip (~65 MB)..."
$sftp = New-SFTPSession -ComputerName $Server -Credential $cred -AcceptKey -Force
try {
    Get-SFTPItem -SessionId $sftp.SessionId -Path "/home/odak/html.zip" -Destination $DockerDir -Force
    Write-Host "Downloading kalite_yedek.sql (~36 MB)..."
    Get-SFTPItem -SessionId $sftp.SessionId -Path "/home/odak/html/kalite_yedek.sql" -Destination (Join-Path $DockerDir "db\init") -Force
}
finally {
    Remove-SFTPSession -SessionId $sftp.SessionId -ErrorAction SilentlyContinue
}

$sqlDownloaded = Join-Path $DockerDir "db\init\kalite_yedek.sql"
$sqlTarget = Join-Path $DockerDir "db\init\01-kalite.sql"
if (Test-Path $sqlDownloaded) {
    if (Test-Path $sqlTarget) { Remove-Item $sqlTarget -Force }
    Rename-Item $sqlDownloaded $sqlTarget
}

Write-Host "Extracting html.zip..."
$zipLocal = Join-Path $DockerDir "html.zip"
$extractDir = Join-Path $DockerDir "_extract"
if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
Expand-Archive -Path $zipLocal -DestinationPath $extractDir -Force
$htmlRoot = Join-Path $extractDir "html"
$kaliteSrc = Join-Path $htmlRoot "kalite"
if (-not (Test-Path $kaliteSrc)) {
    throw "Expected html/kalite in archive"
}

$appDest = Join-Path $DockerDir "app"
if (Test-Path $appDest) { Remove-Item $appDest -Recurse -Force }
Copy-Item $kaliteSrc $appDest -Recurse -Force

# Upload root mirrors server DocumentRoot (/home/odak/html) minus kalite app
$uploadDest = Join-Path $DockerDir "uploads"
if (Test-Path $uploadDest) { Get-ChildItem $uploadDest | Remove-Item -Recurse -Force }
@("Yonetim", "Urunler", "file_storage", "Satin_Alma", "Kalite_Arsiv", "Idari_Kayitlar", "KYS_Uygulama") | ForEach-Object {
    $src = Join-Path $htmlRoot $_
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $uploadDest $_) -Recurse -Force
    }
}
$emptyPdf = Join-Path $htmlRoot "empty.pdf"
if (Test-Path $emptyPdf) {
    Copy-Item $emptyPdf (Join-Path $uploadDest "empty.pdf") -Force
}

# app_local.php for Docker DB
$appLocal = @'
<?php
return [
    'debug' => filter_var(env('CAKEPHP_DEBUG', true), FILTER_VALIDATE_BOOLEAN),
    'Datasources' => [
        'default' => [
            'host' => env('DB_HOST', 'mysql'),
            'username' => env('DB_USER', 'kalite'),
            'password' => env('DB_PASSWORD', '333221'),
            'database' => env('DB_NAME', 'kalite'),
        ],
    ],
];
'@
Set-Content -Path (Join-Path $appDest "config\app_local.php") -Value $appLocal -Encoding UTF8

$bootstrap = Join-Path $appDest "config\bootstrap.php"
$bootstrapText = Get-Content $bootstrap -Raw
if ($bootstrapText -match "//Configure::load\('app_local'") {
    $bootstrapText = $bootstrapText -replace "//Configure::load\('app_local', 'default'\);", "Configure::load('app_local', 'default');"
    Set-Content -Path $bootstrap -Value $bootstrapText -Encoding UTF8
}

# Writable tmp for CakePHP
$tmpDirs = @("tmp", "tmp\cache", "tmp\sessions", "tmp\tests", "logs")
foreach ($d in $tmpDirs) {
    New-Item -ItemType Directory -Force -Path (Join-Path $appDest $d) | Out-Null
}

Remove-Item $extractDir -Recurse -Force

Write-Host "Done. Next:"
Write-Host "  cd `"$DockerDir`""
Write-Host "  docker compose build"
Write-Host "  docker compose up -d"
