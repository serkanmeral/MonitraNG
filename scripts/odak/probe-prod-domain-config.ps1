param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$cmd = @'
echo "=== mng_apps .env ==="
grep -E 'CORS_|GATEWAY_|HUB_|MNG_KEEPER|OPENAPI|DOMAIN|KEEPER_|DATAGATEWAY' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env 2>/dev/null || true
echo "=== mng_common Keycloak ==="
grep ODAK_KEYCLOAK /home/odak/mng_common/.env 2>/dev/null || true
echo "=== gateway CORS env in container ==="
docker exec mnggateway printenv 2>/dev/null | grep -i Cors || true
echo "=== domain probe ==="
curl -sI -m 5 https://mng.odaksavunma.com 2>&1 | head -8
curl -sI -m 5 http://mng.odaksavunma.com 2>&1 | head -5
'@
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 90
$r.Output | ForEach-Object { Write-Host $_ }
Write-Host "`n=== mngui bundle + nginx hub + CORS ===" -ForegroundColor Cyan
$verify = @'
docker exec mngui sh -c "grep -ro 'odaksavunma.com' /usr/share/nginx/html/_nuxt 2>/dev/null | head -1 || echo NO_DOMAIN_IN_BUNDLE"
docker exec mngui sh -c "grep -ro '192.168.20.8:5040' /usr/share/nginx/html/_nuxt 2>/dev/null | head -1 || echo NO_OLD_GATEWAY_IN_BUNDLE"
docker exec mngui grep -n "location /hub/" /etc/nginx/conf.d/default.conf || true
curl -sI -m 5 -H "Origin: https://mng.odaksavunma.com" http://127.0.0.1:5040/health | grep -iE 'HTTP|access-control' || true
'@
$r2 = Invoke-SSHCommand -SessionId $s.SessionId -Command $verify -TimeOut 60
$r2.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId | Out-Null
