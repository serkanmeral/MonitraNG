param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$cmd = ConvertTo-UnixShell @"
sleep 15
cd /home/odak/mng_common
docker compose -f docker-compose.yml -f docker-compose.odak.prod.yml --env-file .env ps
echo '--- health ---'
curl -s -o /dev/null -w 'keycloak=%{http_code}\n' http://127.0.0.1:8080/keycloak/ || true
curl -s -o /dev/null -w 'mongo_express=%{http_code}\n' http://127.0.0.1:8081/ || true
docker logs keycloak --tail 5 2>&1 || true
docker logs nodered --tail 3 2>&1 || true
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 90
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
