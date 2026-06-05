# Production: host (192.168.20.8) ve mng_apps konteynerlerinden altyapi erisimi
param([string]$Server = "192.168.20.8")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$remote = ConvertTo-UnixShell @'
set -e
HOST_IP=192.168.20.8
APPS=/home/odak/MonitraNG/ApplicationResources/mng_apps
COMMON=/home/odak/mng_common

echo "========== 1) Host uzerinden dis erisim ($HOST_IP / 127.0.0.1) =========="
check_url() {
  label="$1"; url="$2"
  code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "$url" 2>/dev/null || echo "ERR")
  echo "  $label -> $url => $code"
}
check_url "Keycloak"     "http://$HOST_IP:8080/keycloak/"
check_url "Keycloak loc" "http://127.0.0.1:8080/keycloak/"
check_url "Mongo Express" "http://$HOST_IP:8081/"
check_url "MinIO Console" "http://$HOST_IP:9091/"
check_url "RabbitMQ Mgmt" "http://$HOST_IP:15672/"
check_url "Gateway health" "http://$HOST_IP:5040/health"
check_url "Keeper health"  "http://$HOST_IP:5001/health"

echo ""
echo "========== 2) TCP portlari (host) =========="
for spec in "27017:MongoDB" "6379:Redis" "5672:RabbitMQ" "9090:MinIO API" "1883:MQTT"; do
  port="${spec%%:*}"; name="${spec#*:}"
  if timeout 2 bash -c "echo >/dev/tcp/$HOST_IP/$port" 2>/dev/null; then
    echo "  $name ($port) -> OPEN on $HOST_IP"
  else
    echo "  $name ($port) -> CLOSED/timeout on $HOST_IP"
  fi
done

echo ""
echo "========== 3) Docker agi (mng_common_mng_network) =========="
docker network inspect mng_common_mng_network --format '{{len .Containers}} containers' 2>/dev/null || echo "  NETWORK MISSING"
echo "  mng_common:"
docker ps --filter name=^(mongo|keycloak|redis|rabbitmq|minio|postgres|mosquitto)$ --format '  {{.Names}} {{.Status}}' 2>/dev/null || true
echo "  mng_apps (ornek):"
docker ps --filter name=^(mngkeeper|mnggateway|mngdatagateway)$ --format '  {{.Names}} {{.Status}}' 2>/dev/null || true

echo ""
echo "========== 4) mng_apps .env ic baglanti (host adlari) =========="
grep -E '^(MONGO_|KEYCLOAK_|REDIS_|RABBITMQ_|MINIO_|GATEWAY_URL|KEEPER_URL)=' "$APPS/.env" 2>/dev/null | head -20 || echo "  .env yok"

echo ""
echo "========== 5) Konteyner icinden altyapi (docker DNS) =========="
# mngkeeper ayakta degilse mnggateway dene
PROBE=mngkeeper
docker ps --format '{{.Names}}' | grep -q "^${PROBE}$" || PROBE=mnggateway
if ! docker ps --format '{{.Names}}' | grep -q "^${PROBE}$"; then
  echo "  UYARI: mngkeeper/mnggateway yok, ic test atlandi"
else
  echo "  Probe konteyner: $PROBE"
  docker exec "$PROBE" sh -c '
    for t in "mongo:27017" "keycloak:8080" "redis:6379" "rabbitmq:5672" "minio:9000"; do
      host="${t%%:*}"; port="${t#*:}"
      if wget -q -O /dev/null --timeout=3 "http://${host}:${port}/" 2>/dev/null || \
         wget -q -O /dev/null --timeout=3 "http://${host}:${port}/keycloak/" 2>/dev/null || \
         nc -z -w2 "$host" "$port" 2>/dev/null || \
         timeout 2 bash -c "echo >/dev/tcp/$host/$port" 2>/dev/null; then
        echo "    OK  $host:$port"
      else
        echo "    FAIL $host:$port"
      fi
    done
  ' 2>/dev/null || docker exec "$PROBE" bash -c '
    for t in mongo:27017 keycloak:8080 redis:6379 rabbitmq:5672 minio:9000; do
      host="${t%%:*}"; port="${t#*:}"
      if timeout 2 bash -c "echo >/dev/tcp/$host/$port" 2>/dev/null; then
        echo "    OK  $host:$port (tcp)"
      else
        echo "    FAIL $host:$port"
      fi
    done
  ' 2>/dev/null || echo "  exec test basarisiz (image shell araci yok)"
fi

echo ""
echo "========== 6) 192.168.20.8 ile ic servis (YANLIS pattern kontrolu) =========="
if grep -q '192.168.20.20' "$APPS/.env" 2>/dev/null; then
  echo "  UYARI: mng_apps .env icinde test IP 192.168.20.20 var!"
else
  echo "  mng_apps .env: test IP (20.20) yok"
fi
if grep -q '192.168.20.20' "$COMMON/.env" 2>/dev/null; then
  echo "  UYARI: mng_common .env icinde test IP 192.168.20.20 var!"
else
  echo "  mng_common .env: test IP (20.20) yok; hostname=$(grep ODAK_KEYCLOAK_HOSTNAME "$COMMON/.env" | cut -d= -f2)"
fi
'@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
Write-Host "Production altyapi baglanti kontrolu ($Server)..." -ForegroundColor Cyan
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
Remove-SSHSession -SessionId $session.SessionId | Out-Null

# PC'den dis erisim (gelistirme agi)
Write-Host ""
Write-Host "========== 7) Gelistirme PC -> $Server ==========" -ForegroundColor Cyan
@(
    "http://${Server}:8080/keycloak/",
    "http://${Server}:5040/health",
    "http://${Server}:27017"
) | ForEach-Object {
    if ($_ -match ':27017') {
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $iar = $tcp.BeginConnect($Server, 27017, $null, $null)
            $ok = $iar.AsyncWaitHandle.WaitOne(3000, $false)
            if ($ok -and $tcp.Connected) { Write-Host "  Mongo TCP ${Server}:27017 -> OPEN" } else { Write-Host "  Mongo TCP ${Server}:27017 -> timeout" }
            $tcp.Close()
        } catch { Write-Host "  Mongo TCP ${Server}:27017 -> FAIL" }
    } else {
        try {
            $code = (Invoke-WebRequest -Uri $_ -UseBasicParsing -TimeoutSec 8).StatusCode
            Write-Host "  $_ -> $code"
        } catch {
            Write-Host "  $_ -> FAIL ($($_.Exception.Message))"
        }
    }
}
