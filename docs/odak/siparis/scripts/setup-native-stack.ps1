# Native local stack for legacy Kalite (no Docker / no WSL)
# Installs PHP 7.4 + MySQL 8 zip under %USERPROFILE%\kalite-legacy-local

param(
    [string]$BaseDir = "",
    [string]$SourceDir = "",
    [int]$WebPort = 8080,
    [int]$MySqlPort = 3307
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BaseDir)) {
    $BaseDir = Join-Path $env:USERPROFILE "kalite-legacy-local"
}
if ([string]::IsNullOrWhiteSpace($SourceDir)) {
    $SourceDir = Join-Path $env:USERPROFILE "kalite-legacy-docker"
}

$phpZipUrl = "https://windows.php.net/downloads/releases/archives/php-7.4.33-Win32-vc15-x64.zip"
$mysqlZipUrl = "https://dev.mysql.com/get/Downloads/MySQL-8.0/mysql-8.0.39-winx64.zip"

$phpDir = Join-Path $BaseDir "php"
$mysqlDir = Join-Path $BaseDir "mysql"
$dataDir = Join-Path $BaseDir "mysql-data"
$dlDir = Join-Path $BaseDir "downloads"
$appDir = Join-Path $BaseDir "app"
$uploadDir = Join-Path $BaseDir "uploads"

@($dlDir, $dataDir, $appDir, $uploadDir) | ForEach-Object {
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
}

function Ensure-Download($Url, $OutFile) {
    if ((Test-Path $OutFile) -and (Get-Item $OutFile).Length -gt 1MB) {
        Write-Host "Already downloaded: $OutFile"
        return
    }
    Write-Host "Downloading $Url ..."
    curl.exe -L -o $OutFile $Url
    if (-not (Test-Path $OutFile)) { throw "Download failed: $Url" }
}

# --- PHP ---
$phpZip = Join-Path $dlDir "php-7.4.33.zip"
Ensure-Download $phpZipUrl $phpZip
if (-not (Test-Path (Join-Path $phpDir "php.exe"))) {
    Write-Host "Extracting PHP..."
    if (Test-Path $phpDir) { Remove-Item $phpDir -Recurse -Force }
    Expand-Archive $phpZip -DestinationPath $phpDir -Force
}

$phpIni = Join-Path $phpDir "php.ini"
if (-not (Test-Path $phpIni)) {
    Copy-Item (Join-Path $phpDir "php.ini-development") $phpIni
    Add-Content $phpIni "`nextension_dir = `"ext`""
    Add-Content $phpIni "`nextension=mysqli`nextension=pdo_mysql`nextension=mbstring`nextension=openssl`nextension=intl`nextension=fileinfo`nextension=curl`nextension=gd"
}

# --- MySQL ---
$mysqlZip = Join-Path $dlDir "mysql-8.0.39.zip"
Ensure-Download $mysqlZipUrl $mysqlZip
$mysqlRoot = Join-Path $mysqlDir "mysql-8.0.39-winx64"
if (-not (Test-Path (Join-Path $mysqlRoot "bin\mysqld.exe"))) {
    Write-Host "Extracting MySQL (may take a minute)..."
    if (Test-Path $mysqlDir) { Remove-Item $mysqlDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $mysqlDir | Out-Null
    Expand-Archive $mysqlZip -DestinationPath $mysqlDir -Force
}

$mysqld = Join-Path $mysqlRoot "bin\mysqld.exe"
$mysql = Join-Path $mysqlRoot "bin\mysql.exe"

if (-not (Test-Path (Join-Path $dataDir "mysql"))) {
    Write-Host "Initializing MySQL data directory..."
    $initProc = Start-Process -FilePath $mysqld -ArgumentList @("--initialize-insecure", "--datadir=$dataDir") -Wait -PassThru -NoNewWindow -RedirectStandardError (Join-Path $BaseDir "mysql-init.log")
    if ($initProc.ExitCode -ne 0 -and -not (Test-Path (Join-Path $dataDir "mysql"))) {
        throw "MySQL initialize failed. See mysql-init.log"
    }
}

# --- App + uploads from docker sync dir ---
if (Test-Path (Join-Path $SourceDir "app")) {
    if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }
    Copy-Item (Join-Path $SourceDir "app") $appDir -Recurse -Force
}
if (Test-Path (Join-Path $SourceDir "uploads")) {
    if (Test-Path $uploadDir) { Remove-Item $uploadDir -Recurse -Force }
    Copy-Item (Join-Path $SourceDir "uploads") $uploadDir -Recurse -Force
}

$appLocal = @'
<?php
return [
    'debug' => true,
    'Datasources' => [
        'default' => [
            'host' => '127.0.0.1',
            'port' => '3307',
            'username' => 'root',
            'password' => '',
            'database' => 'kalite',
        ],
    ],
];
'@
Set-Content (Join-Path $appDir "config\app_local.php") -Value $appLocal -Encoding UTF8
$bootstrapPath = Join-Path $appDir "config\bootstrap.php"
$bootstrapText = Get-Content $bootstrapPath -Raw
if ($bootstrapText -match "//Configure::load\('app_local'") {
    $bootstrapText = $bootstrapText -replace "//Configure::load\('app_local', 'default'\);", "Configure::load('app_local', 'default');"
    Set-Content -Path $bootstrapPath -Value $bootstrapText -Encoding UTF8
}
@("tmp", "tmp\cache", "tmp\sessions", "logs") | ForEach-Object {
    New-Item -ItemType Directory -Force -Path (Join-Path $appDir $_) | Out-Null
}

# --- my.ini ---
$myIni = Join-Path $BaseDir "my.ini"
@"
[mysqld]
basedir=$($mysqlRoot -replace '\\','/')
datadir=$($dataDir -replace '\\','/')
port=$MySqlPort
bind-address=127.0.0.1
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
max_allowed_packet=256M
"@ | Set-Content $myIni -Encoding ASCII

# --- start scripts ---
$startMysql = @"
`$ErrorActionPreference = 'Stop'
`$base = '$BaseDir'
`$mysqlRoot = '$mysqlRoot'
Start-Process -FilePath (Join-Path `$mysqlRoot 'bin\mysqld.exe') `
  -ArgumentList @('--defaults-file=`"$base\my.ini`"", '--console') `
  -WorkingDirectory (Join-Path `$mysqlRoot 'bin') `
  -WindowStyle Minimized
Write-Host "MySQL starting on port $MySqlPort ..."
Start-Sleep -Seconds 8
"@

$importSql = @"
`$mysql = Join-Path '$mysqlRoot' 'bin\mysql.exe'
`$sql = Join-Path '$SourceDir' 'db\init\01-kalite.sql'
if (-not (Test-Path `$sql)) { `$sql = Join-Path '$SourceDir' 'db\init\kalite_yedek.sql' }
Write-Host 'Creating database and importing (5-15 min)...'
& `$mysql -h 127.0.0.1 -P $MySqlPort -u root -e 'CREATE DATABASE IF NOT EXISTS kalite CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;'
cmd /c "`"`$mysql`" -h 127.0.0.1 -P $MySqlPort -u root kalite < `"`$sql`""
Write-Host 'Import done.'
& `$mysql -h 127.0.0.1 -P $MySqlPort -u root -e 'SELECT COUNT(*) AS packages FROM kalite.packages;'
"@

$startWeb = @"
`$ErrorActionPreference = 'Stop'
`$env:Path = '$phpDir;' + `$env:Path
`$env:CAKEPHP_UPLOAD_ROOT = '$uploadDir\'
Set-Location '$appDir'
Write-Host 'Kalite web: http://localhost:$WebPort'
Write-Host 'Press Ctrl+C to stop.'
& '$phpDir\php.exe' bin\cake.php server -H 127.0.0.1 -p $WebPort
"@

Set-Content (Join-Path $BaseDir "start-mysql.ps1") $startMysql -Encoding UTF8
Set-Content (Join-Path $BaseDir "import-db.ps1") $importSql -Encoding UTF8
Set-Content (Join-Path $BaseDir "start-web.ps1") $startWeb -Encoding UTF8

Write-Host ""
Write-Host "Setup complete: $BaseDir"
Write-Host "Next:"
Write-Host "  1. .\start-mysql.ps1"
Write-Host "  2. .\import-db.ps1   (once)"
Write-Host "  3. .\start-web.ps1"
Write-Host "  -> http://localhost:$WebPort"
