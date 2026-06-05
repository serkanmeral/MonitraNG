param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$remote = ConvertTo-UnixShell @'
PROBE=mngkeeper
docker ps --format '{{.Names}}' | grep -qx mngkeeper || PROBE=mnggateway
echo "Probe: $PROBE"
docker exec $PROBE bash -c '
  test_tcp() { timeout 2 bash -c "echo >/dev/tcp/$1/$2" 2>/dev/null && echo "OK  $1:$2" || echo "FAIL $1:$2"; }
  test_tcp mongo 27017
  test_tcp keycloak 8080
  test_tcp redis 6379
  test_tcp rabbitmq 5672
  test_tcp minio 9000
  code=$(wget -q -O- --timeout=3 http://keycloak:8080/keycloak/ 2>/dev/null | head -c 1; echo)
  echo "Keycloak HTTP body: $([ -n "$code" ] && echo reachable || echo empty/fail)"
'
echo "--- mng_apps .env ic servisler ---"
grep -E '^(MONGO_CONNECTION|KEYCLOAK_BASE|REDIS_CONNECTION|RABBITMQ_HOST|MINIO_ENDPOINT|GATEWAY_URL)=' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env
'@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 60
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
