param(
    [string]$Server = "192.168.20.20",
    [string]$Realm = "odak"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$remote = ConvertTo-UnixShell @"
set -e
KC_BASE=http://127.0.0.1:8080/keycloak
ENV_FILE=/home/odak/mng_common/.env
[ -f "$ENV_FILE" ] || ENV_FILE=/home/odak/MonitraNG/ApplicationResources/mng_apps/.env
KC_ADMIN=`$(grep '^KEYCLOAK_ADMIN_USERNAME=' "`$ENV_FILE" | cut -d= -f2- | tr -d "\r")
KC_PASS=`$(grep '^KEYCLOAK_ADMIN_PASSWORD=' "`$ENV_FILE" | cut -d= -f2- | tr -d "\r")
[ -n "`$KC_ADMIN" ] || KC_ADMIN=admin

TOKEN_JSON=`$(curl -sf -X POST "`$KC_BASE/realms/master/protocol/openid-connect/token" \
  -d "grant_type=password" -d "client_id=admin-cli" -d "username=`$KC_ADMIN" -d "password=`$KC_PASS")
ADMIN_TOKEN=`$(echo "`$TOKEN_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

echo "=== REALM: $Realm ==="
curl -sf -H "Authorization: Bearer `$ADMIN_TOKEN" "`$KC_BASE/admin/realms/$Realm" | python3 -c "import sys,json; r=json.load(sys.stdin); print('realm:', r.get('realm'), 'enabled:', r.get('enabled'))" 2>/dev/null || echo "Realm bulunamadi veya hata"

echo "=== LDAP COMPONENTS ($Realm) ==="
COMP=`$(curl -sf -H "Authorization: Bearer `$ADMIN_TOKEN" "`$KC_BASE/admin/realms/$Realm/components?type=org.keycloak.storage.UserStorageProvider")
echo "`$COMP" | python3 -c "
import sys,json
items=json.load(sys.stdin)
print('provider_count:', len(items))
for p in items:
    c=p.get('config',{})
    def g(k):
        v=c.get(k)
        return v[0] if isinstance(v,list) and v else v
    print('---')
    print('id:', p.get('id'))
    print('name:', p.get('name'))
    print('providerId:', p.get('providerId'))
    print('connectionUrl:', g('connectionUrl'))
    print('usersDn:', g('usersDn'))
    print('bindDn:', g('bindDn'))
    print('usernameLDAPAttribute:', g('usernameLDAPAttribute'))
    print('rdnLDAPAttribute:', g('rdnLDAPAttribute'))
    print('uuidLDAPAttribute:', g('uuidLDAPAttribute'))
    print('vendor:', g('vendor'))
    print('editMode:', g('editMode'))
    print('importEnabled:', g('importEnabled'))
    print('syncRegistrations:', g('syncRegistrations'))
    print('pagination:', g('pagination'))
    print('fullSyncPeriod:', g('fullSyncPeriod'))
    print('changedSyncPeriod:', g('changedSyncPeriod'))
"

echo "=== LDAP MAPPERS ==="
COMP_IDS=`$(echo "`$COMP" | python3 -c "import sys,json; [print(p['id']) for p in json.load(sys.stdin)]" 2>/dev/null || true)
for PID in `$COMP_IDS; do
  echo "--- mappers for provider `$PID ---"
  curl -sf -H "Authorization: Bearer `$ADMIN_TOKEN" "`$KC_BASE/admin/realms/$Realm/components?parent=`$PID&type=org.keycloak.storage.ldap.mappers.LDAPStorageMapper" | python3 -c "
import sys,json
for m in json.load(sys.stdin):
    c=m.get('config',{})
    def g(k):
        v=c.get(k)
        return v[0] if isinstance(v,list) and v else v
    print('mapper:', m.get('name'), '| type:', m.get('providerId'))
    for k in sorted(c.keys()):
        v=g(k)
        if v: print(' ', k, '=', v)
" 2>/dev/null || true
done

echo "=== SAMPLE USERS (federated) ==="
curl -sf -H "Authorization: Bearer `$ADMIN_TOKEN" "`$KC_BASE/admin/realms/$Realm/users?max=5" | python3 -c "
import sys,json
for u in json.load(sys.stdin):
    fed=u.get('federationLink','')
    print(u.get('username'), '| fed:', fed, '| fn:', u.get('firstName'), '| ln:', u.get('lastName'))
" 2>/dev/null || true

echo "=== smeral user lookup ==="
curl -sf -H "Authorization: Bearer `$ADMIN_TOKEN" "`$KC_BASE/admin/realms/$Realm/users?username=smeral&exact=true" | python3 -c "
import sys,json
d=json.load(sys.stdin)
print('count:', len(d))
for u in d: print('username:', u.get('username'), 'id:', u.get('id'))
" 2>/dev/null || echo "smeral bulunamadi"
"@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) { Write-Host "STDERR: $($r.Error)" -ForegroundColor Red; exit 1 }
Remove-SSHSession $s.SessionId
