param(
    [string]$Server = "192.168.20.8",
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
KC_PASS=`$(grep '^KEYCLOAK_ADMIN_PASSWORD=' "`$ENV_FILE" | cut -d= -f2- | tr -d "\r")
TOK=`$(curl -sf -X POST "`$KC_BASE/realms/master/protocol/openid-connect/token" -d 'grant_type=password' -d 'client_id=admin-cli' -d 'username=admin' -d "password=`$KC_PASS" | python3 -c 'import sys,json; print(json.load(sys.stdin)["access_token"])')

PID=`$(curl -sf -H "Authorization: Bearer `$TOK" "`$KC_BASE/admin/realms/$Realm/components?type=org.keycloak.storage.UserStorageProvider" | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d[0]["id"] if d else "")')
MID=`$(curl -sf -H "Authorization: Bearer `$TOK" "`$KC_BASE/admin/realms/$Realm/components?parent=`$PID&type=org.keycloak.storage.ldap.mappers.LDAPStorageMapper" | python3 -c 'import sys,json
for m in json.load(sys.stdin):
  if m.get("providerId")=="group-ldap-mapper": print(m["id"]); break')

echo "provider=$PID group_mapper=$MID"
curl -sf -X POST -H "Authorization: Bearer `$TOK" "`$KC_BASE/admin/realms/$Realm/user-storage/`$PID/mappers/`$MID/sync?direction=fedToKeycloak"
echo
"@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 300
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
