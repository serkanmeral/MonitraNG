param(
    [string]$Server = "192.168.20.20"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$remote = @'
echo === host ===
hostname
cat /etc/os-release 2>/dev/null | head -5
id
groups
docker --version 2>/dev/null || echo NO_DOCKER
echo === dirs ===
ls -la /home/odak/mng_common 2>/dev/null | head -3 || echo NO_MNG_COMMON
ls -la /home/odak/MonitraNG 2>/dev/null | head -5 || echo NO_MONITRANG_ROOT
ls -la /home/odak/MonitraNG/ApplicationResources/mng_apps/docker-compose.production.yml 2>/dev/null || echo NO_MNG_APPS_COMPOSE
ls -la /home/odak/MonitraNG/ApplicationResources/mng_apps/.env.odak.prod.example 2>/dev/null || echo NO_PROD_ENV_EXAMPLE
test -f /home/odak/mng_common/.env && echo MNG_COMMON_ENV=yes || echo MNG_COMMON_ENV=no
test -f /home/odak/MonitraNG/ApplicationResources/mng_apps/.env && echo MNG_APPS_ENV=yes || echo MNG_APPS_ENV=no
docker network inspect mng_common_mng_network >/dev/null 2>&1 && echo NETWORK=ok || echo NETWORK=missing
curl -s -o /dev/null -w "gateway=%{http_code}\n" http://127.0.0.1:5040/health 2>/dev/null || echo gateway=down
'@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 30
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
if ($r.ExitStatus -ne 0) { exit $r.ExitStatus }
