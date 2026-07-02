# MariaDB kalite semasini salt okunur yapar (192.168.20.30 arsiv sunucusu).
#
# 1) kalite_ro kullanicisi (SELECT only)
# 2) CakePHP app_local.php -> kalite_ro
# 3) kalite kullanicisindan yazma yetkileri kaldirilir
#
# Kullanim:
#   .\enable-legacy-kalite-db-readonly.ps1
#   .\enable-legacy-kalite-db-readonly.ps1 -ReadOnlyPassword 'KaliteRo333221'

param(
    [string]$Server = "192.168.20.30",
    [string]$User = "odak",
    [string]$Password = "Odak333221",
    [string]$ReadOnlyUser = "kalite_ro",
    [string]$ReadOnlyPassword = "KaliteRo333221",
    [string]$LegacyUser = "kalite",
    [string]$AppRoot = "/home/odak/html/kalite",
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
SUDO_PW="__SUDO_PW__"
RO_USER="__RO_USER__"
RO_PW="__RO_PW__"
LEGACY_USER="__LEGACY_USER__"
APP_ROOT="__APP_ROOT__"

sudo_cmd() {
  echo "$SUDO_PW" | sudo -S "$@"
}

mysql_root() {
  sudo_cmd mysql -e "$1"
}

echo "=== 1) kalite_ro kullanicisi (SELECT only) ==="
mysql_root "CREATE USER IF NOT EXISTS '${RO_USER}'@'localhost' IDENTIFIED BY '${RO_PW}';"
mysql_root "GRANT SELECT ON kalite.* TO '${RO_USER}'@'localhost';"
mysql_root "FLUSH PRIVILEGES;"

echo "=== 2) kalite kullanicisindan yazma yetkileri ==="
for priv in INSERT UPDATE DELETE DROP ALTER CREATE INDEX TRIGGER REFERENCES CREATE VIEW SHOW VIEW CREATE ROUTINE ALTER ROUTINE EXECUTE EVENT LOCK TABLES; do
  mysql_root "REVOKE ${priv} ON kalite.* FROM '${LEGACY_USER}'@'localhost';" 2>/dev/null || true
done
mysql_root "FLUSH PRIVILEGES;"

echo "=== 3) app_local.php -> kalite_ro ==="
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
            'username' => '${RO_USER}',
            'password' => '${RO_PW}',
            'database' => 'kalite',
        ],
    ],
];
PHP

echo "=== 4) Queue/cron kontrolu ==="
if crontab -l 2>/dev/null | grep -qi queue; then
  echo "WARN: odak crontab queue isi bulundu — manuel kontrol edin"
else
  echo "OK: odak crontab queue yok"
fi
if pgrep -af "[c]ake queue run" 2>/dev/null; then
  echo "WARN: queue worker calisiyor"
else
  echo "OK: queue worker yok"
fi

echo "=== 5) Yetki dogrulama ==="
mysql -u "$RO_USER" -p"$RO_PW" kalite -e "SELECT COUNT(*) AS packages FROM packages;" 
mysql -u "$RO_USER" -p"$RO_PW" kalite -e "INSERT INTO packages (package_no) VALUES ('READONLY-TEST');" 2>&1 | grep -qi "denied\|readonly\|command denied" && echo RO_INSERT_BLOCKED || echo "FAIL: INSERT izinli!"
mysql -u "$LEGACY_USER" -p333221 kalite -e "UPDATE packages SET name=name WHERE id=1;" 2>&1 | grep -qi "denied\|readonly\|command denied" && echo LEGACY_UPDATE_BLOCKED || echo "WARN: kalite hala yazabiliyor"

echo "=== 6) HTTP smoke ==="
BODY=$(curl -s http://127.0.0.1/kalite/users/login)
echo "$BODY" | grep -qi '<form' && echo LOGIN_FORM_OK || echo LOGIN_FORM_FAIL
echo "$BODY" | grep -qi 'Deprecated' && echo DEPRECATED_VISIBLE || echo DEPRECATED_HIDDEN

echo "=== 7) Aktif DB kullanicisi ==="
grep -E "username|password" "$APP_ROOT/config/app_local.php" | head -4

echo "DONE_READONLY"
'@

$remoteScript = $remoteScript.Replace("__SUDO_PW__", $Password)
$remoteScript = $remoteScript.Replace("__RO_USER__", $ReadOnlyUser)
$remoteScript = $remoteScript.Replace("__RO_PW__", $ReadOnlyPassword)
$remoteScript = $remoteScript.Replace("__LEGACY_USER__", $LegacyUser)
$remoteScript = $remoteScript.Replace("__APP_ROOT__", $AppRoot)

if ($WhatIf) {
    Write-Host $remoteScript
    return
}

Write-Host "Read-only DB: $ReadOnlyUser@${Server}" -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $result = Invoke-SSHCommand -SessionId $session.SessionId -Command $remoteScript -TimeOut 120
    if ($result.Output) { $result.Output | ForEach-Object { Write-Host $_ } }
    if ($result.Error) { $result.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
    if ($result.ExitStatus -ne 0) { throw "Uzak script exit $($result.ExitStatus)" }
    if (($result.Output -join "`n") -notmatch "DONE_READONLY") { throw "Script tamamlanmadi" }
}
finally {
    Remove-SSHSession -SessionId $session.SessionId -ErrorAction SilentlyContinue | Out-Null
}

Write-Host "`nUygulama salt okunur DB ile bagli: http://${Server}/kalite/" -ForegroundColor Green
