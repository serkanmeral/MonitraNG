# Odak mngnotifier — kurumsal SMTP (.env patch + container recreate)
#
# Sifre repoya yazilmaz. Ortam degiskeni veya parametre:
#   $env:ODAK_SMTP_PASSWORD = '...'
#   .\configure-odak-notifier-smtp.ps1
#
param(
    [string]$Server = "192.168.20.20",
    [string]$SmtpPassword = "",
    [string]$EnvFile = "/home/odak/MonitraNG/ApplicationResources/mng_apps/.env"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($SmtpPassword)) {
    $SmtpPassword = $env:ODAK_SMTP_PASSWORD
}
if ([string]::IsNullOrEmpty($SmtpPassword)) {
    throw "ODAK_SMTP_PASSWORD ortam degiskeni veya -SmtpPassword gerekli."
}

Import-Module Posh-SSH -Force
$repoRoot = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
. (Join-Path $repoRoot "scripts/odak/OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$escapedPass = $SmtpPassword -replace "'", "'\''"

$remote = @"
set -e
ENV='$EnvFile'
touch "`$ENV"
upsert() {
  key="`$1"; val="`$2"
  if grep -q "^`${key}=" "`$ENV" 2>/dev/null; then
    sed -i "s|^`${key}=.*|`${key}=`${val}|" "`$ENV"
  else
    echo "`${key}=`${val}" >> "`$ENV"
  fi
}
upsert SMTP_HOST mail.kurumsaleposta.com
upsert SMTP_PORT 465
upsert SMTP_ENABLE_SSL true
upsert SMTP_SECURE_SOCKET_MODE SslOnConnect
upsert SMTP_USERNAME noreply@odakkompozit.com.tr
upsert SMTP_PASSWORD '$escapedPass'
upsert SMTP_FROM_EMAIL noreply@odakkompozit.com.tr
upsert SMTP_FROM_NAME "Odak Kompozit"
echo "=== SMTP .env (maskeli) ==="
grep '^SMTP_' "`$ENV" | sed 's/PASSWORD=.*/PASSWORD=***/'
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml up -d --force-recreate mngnotifier
echo "mngnotifier recreated"
"@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) {
    if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Red } }
    throw "Odak SMTP yapilandirmasi basarisiz (exit $($r.ExitStatus))"
}
Remove-SSHSession -SessionId $session.SessionId | Out-Null
Write-Host "Tamam. send-template SMTP testi icin smoke veya preview-template calistirin." -ForegroundColor Green
