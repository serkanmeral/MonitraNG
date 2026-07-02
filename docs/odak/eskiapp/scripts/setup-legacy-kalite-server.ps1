# Eski Kalite uygulamasini 192.168.20.30 uzerinde calisir hale getirir.
# Apache + MariaDB zaten kurulu; bu script config ve ortam degiskenlerini tamamlar.
#
# Kullanim:
#   .\docs\odak\eskiapp\scripts\setup-legacy-kalite-server.ps1
#   .\docs\odak\eskiapp\scripts\setup-legacy-kalite-server.ps1 -WhatIf

param(
    [string]$Server = "192.168.20.30",
    [string]$User = "odak",
    [string]$Password = "Odak333221",
    [string]$DbUser = "kalite_ro",
    [string]$DbPassword = "KaliteRo333221",
    [string]$AppRoot = "/home/odak/html/kalite",
    [string]$UploadRoot = "/home/odak/html/",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

if (-not (Get-Module -ListAvailable Posh-SSH)) {
    throw "Posh-SSH gerekli: Install-Module Posh-SSH -Scope CurrentUser"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
. (Join-Path $repoRoot "scripts\odak\OdakSshCommon.ps1")

$sec = ConvertTo-SecureString $Password -AsPlainText -Force
$cred = Get-OdakSshCredential -User $User -Server $Server -Password $sec

$remoteScript = @'
set -e
APP_ROOT="__APP_ROOT__"
UPLOAD_ROOT="__UPLOAD_ROOT__"
SUDO_PW="__SUDO_PW__"
DB_USER="__DB_USER__"
DB_PW="__DB_PW__"

sudo_cmd() {
  echo "$SUDO_PW" | sudo -S "$@"
}

echo "=== 1) app_local.php ==="
cat > "$APP_ROOT/config/app_local.php" <<PHP
<?php
return [
    'debug' => false,
    'Error' => [
        'errorLevel' => E_ALL & ~E_DEPRECATED & ~E_USER_DEPRECATED & ~E_STRICT,
    ],
    'Datasources' => [
        'default' => [
            'host' => 'localhost',
            'username' => '${DB_USER}',
            'password' => '${DB_PW}',
            'database' => 'kalite',
        ],
    ],
];
PHP

echo "=== 2) bootstrap.php — app_local aktif ==="
BOOT="$APP_ROOT/config/bootstrap.php"
if grep -q "//Configure::load('app_local'" "$BOOT"; then
  sed -i "s|//Configure::load('app_local', 'default');|Configure::load('app_local', 'default');|" "$BOOT"
fi

echo "=== 3) app.php — debug kapat ==="
APP="$APP_ROOT/config/app.php"
sed -i "s/'debug' => true,/'debug' => false,/" "$APP" || true

echo "=== 4) tmp/logs izinleri ==="
chmod -R 775 "$APP_ROOT/tmp" "$APP_ROOT/logs" 2>/dev/null || true

echo "=== 5) Apache SetEnv CAKEPHP_UPLOAD_ROOT ==="
VHOST="/etc/apache2/sites-available/kalite.conf"
if ! grep -q "CAKEPHP_UPLOAD_ROOT" "$VHOST"; then
  sudo_cmd sed -i "/ServerAlias kalite/a\\    SetEnv CAKEPHP_UPLOAD_ROOT $UPLOAD_ROOT" "$VHOST"
fi
sudo_cmd apache2ctl configtest
sudo_cmd systemctl reload apache2

echo "=== 6) Servis durumu ==="
systemctl is-active apache2
systemctl is-active mariadb || systemctl is-active mysql

echo "=== 7) Veri ozeti ==="
mysql -u "$DB_USER" -p"$DB_PW" kalite -e "SELECT 'packages' t, COUNT(*) c FROM packages UNION SELECT 'packageitems', COUNT(*) FROM packageitems UNION SELECT 'users', COUNT(*) FROM users;"

echo "=== 8) HTTP smoke ==="
BODY=$(curl -s http://127.0.0.1/kalite/users/login)
echo "$BODY" | grep -q 'Giris Yap' && echo LOGIN_OK || echo LOGIN_FAIL
echo "$BODY" | grep -qi 'Deprecated' && echo DEPRECATED_VISIBLE || echo DEPRECATED_HIDDEN

echo "DONE"
'@

$remoteScript = $remoteScript.Replace("__APP_ROOT__", $AppRoot)
$remoteScript = $remoteScript.Replace("__UPLOAD_ROOT__", $UploadRoot)
$remoteScript = $remoteScript.Replace("__SUDO_PW__", $Password)
$remoteScript = $remoteScript.Replace("__DB_USER__", $DbUser)
$remoteScript = $remoteScript.Replace("__DB_PW__", $DbPassword)

if ($WhatIf) {
    Write-Host $remoteScript
    return
}

Write-Host "Sunucu: $User@${Server}" -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $result = Invoke-SSHCommand -SessionId $session.SessionId -Command $remoteScript -TimeOut 120
    if ($result.Output) { $result.Output | ForEach-Object { Write-Host $_ } }
    if ($result.Error) { $result.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
    if ($result.ExitStatus -ne 0) { throw "Uzak script exit $($result.ExitStatus)" }
}
finally {
    Remove-SSHSession -SessionId $session.SessionId -ErrorAction SilentlyContinue | Out-Null
}

Write-Host "`nUygulama: http://${Server}/kalite/" -ForegroundColor Green
Write-Host "Giris:    http://${Server}/kalite/users/login" -ForegroundColor Green
