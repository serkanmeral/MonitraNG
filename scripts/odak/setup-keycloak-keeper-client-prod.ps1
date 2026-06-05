# Production Keycloak: master realm mng-keeper-admin client + .env + mngkeeper recreate
param(
    [string]$Server = "192.168.20.8",
    [string]$User = "odak"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server

$remote = ConvertTo-UnixShell @'
set -e
KC_BASE=http://127.0.0.1:8080/keycloak
ENV_FILE=/home/odak/MonitraNG/ApplicationResources/mng_apps/.env
APPS_DIR=/home/odak/MonitraNG/ApplicationResources/mng_apps
CLIENT_ID=mng-keeper-admin

KC_ADMIN=$(grep '^KEYCLOAK_ADMIN_USERNAME=' "$ENV_FILE" | cut -d= -f2- | tr -d "\r")
KC_PASS=$(grep '^KEYCLOAK_ADMIN_PASSWORD=' "$ENV_FILE" | cut -d= -f2- | tr -d "\r")
[ -n "$KC_ADMIN" ] || KC_ADMIN=admin

echo "=== 1) Keycloak admin token (admin-cli) ==="
TOKEN_JSON=$(curl -sf -X POST "$KC_BASE/realms/master/protocol/openid-connect/token" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" \
  -d "username=$KC_ADMIN" \
  -d "password=$KC_PASS") || { echo "HATA: admin token alinamadi"; exit 1; }

ADMIN_TOKEN=$(echo "$TOKEN_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])" 2>/dev/null \
  || echo "$TOKEN_JSON" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
[ -n "$ADMIN_TOKEN" ] || { echo "HATA: access_token parse"; exit 1; }
echo "Admin token OK"

echo "=== 2) Client $CLIENT_ID kontrol ==="
EXISTING=$(curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$KC_BASE/admin/realms/master/clients?clientId=$CLIENT_ID" || echo "[]")
INTERNAL_ID=$(echo "$EXISTING" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d[0]['id'] if d else '')" 2>/dev/null || true)

if [ -z "$INTERNAL_ID" ]; then
  echo "Client yok — olusturuluyor..."
  HTTP_CODE=$(curl -s -o /tmp/kc-create-client.json -w "%{http_code}" -X POST \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    "$KC_BASE/admin/realms/master/clients" \
    -d "{\"clientId\":\"$CLIENT_ID\",\"name\":\"MngKeeper Admin\",\"enabled\":true,\"protocol\":\"openid-connect\",\"publicClient\":false,\"directAccessGrantsEnabled\":true,\"standardFlowEnabled\":true,\"serviceAccountsEnabled\":false}")
  if [ "$HTTP_CODE" != "201" ] && [ "$HTTP_CODE" != "409" ]; then
    echo "HATA: client create HTTP $HTTP_CODE"; cat /tmp/kc-create-client.json; exit 1
  fi
  EXISTING=$(curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$KC_BASE/admin/realms/master/clients?clientId=$CLIENT_ID")
  INTERNAL_ID=$(echo "$EXISTING" | python3 -c "import sys,json; print(json.load(sys.stdin)[0]['id'])")
  echo "Client olusturuldu: $INTERNAL_ID"
else
  echo "Client mevcut: $INTERNAL_ID"
  curl -sf -X PUT -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
    "$KC_BASE/admin/realms/master/clients/$INTERNAL_ID" \
    -d "{\"clientId\":\"$CLIENT_ID\",\"enabled\":true,\"protocol\":\"openid-connect\",\"publicClient\":false,\"directAccessGrantsEnabled\":true,\"standardFlowEnabled\":true}" >/dev/null || true
  echo "Direct access grants guncellendi"
fi

echo "=== 3) Client secret ==="
SECRET_JSON=$(curl -sf -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$KC_BASE/admin/realms/master/clients/$INTERNAL_ID/client-secret")
CLIENT_SECRET=$(echo "$SECRET_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['value'])" 2>/dev/null \
  || echo "$SECRET_JSON" | sed -n 's/.*"value":"\([^"]*\)".*/\1/p')
[ -n "$CLIENT_SECRET" ] || { echo "HATA: secret alinamadi"; exit 1; }
echo "Secret alindi (len ${#CLIENT_SECRET})"

echo "=== 4) .env guncelle ==="
if grep -q '^KEYCLOAK_CLIENT_SECRET=' "$ENV_FILE"; then
  sed -i "s|^KEYCLOAK_CLIENT_SECRET=.*|KEYCLOAK_CLIENT_SECRET=$CLIENT_SECRET|" "$ENV_FILE"
else
  echo "KEYCLOAK_CLIENT_SECRET=$CLIENT_SECRET" >> "$ENV_FILE"
fi
grep '^KEYCLOAK_CLIENT_ID=' "$ENV_FILE" >/dev/null || echo "KEYCLOAK_CLIENT_ID=$CLIENT_ID" >> "$ENV_FILE"
case "$(grep '^KEYCLOAK_CLIENT_SECRET=' "$ENV_FILE" | cut -d= -f2-)" in CHANGE_ME*) echo "HATA: .env hala placeholder"; exit 1 ;; esac
echo ".env OK"

echo "=== 5) Keeper token testi ==="
KEEPER_USER=$(grep '^KEYCLOAK_ADMIN_USERNAME=' "$ENV_FILE" | cut -d= -f2- | tr -d "\r")
KEEPER_PASS=$(grep '^KEYCLOAK_ADMIN_PASSWORD=' "$ENV_FILE" | cut -d= -f2- | tr -d "\r")
TEST=$(curl -sf -X POST "$KC_BASE/realms/master/protocol/openid-connect/token" \
  -d "grant_type=password" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "username=$KEEPER_USER" \
  -d "password=$KEEPER_PASS") || { echo "HATA: keeper client token test failed"; exit 1; }
echo "$TEST" | grep -q access_token && echo "Keeper client token OK"

echo "=== 6) mngkeeper recreate ==="
cd "$APPS_DIR"
docker compose -f docker-compose.production.yml -f docker-compose.odak.prod.yml --env-file .env up -d --no-deps mngkeeper
sleep 3
docker inspect mngkeeper --format '{{range .Config.Env}}{{println .}}{{end}}' | grep 'MngKeeperSettings__Keycloak__ClientSecret=' | sed 's/ClientSecret=.*/ClientSecret=***OK***/'
echo "Tamamlandi."
'@

Write-Host "Production Keycloak mng-keeper-admin kurulumu ($Server)..." -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 180
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
if ($r.ExitStatus -ne 0) { throw "Keycloak keeper client setup failed (exit $($r.ExitStatus))" }
Write-Host "Bitti. Domain olusturmayi tekrar deneyebilirsiniz." -ForegroundColor Green
