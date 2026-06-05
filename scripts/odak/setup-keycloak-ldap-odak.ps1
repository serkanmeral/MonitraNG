<#
.SYNOPSIS
  odak realm icin Keycloak LDAP (Active Directory) kurulumu veya username duzeltmesi.

.DESCRIPTION
  - Mevcut LDAP varsa: usernameLDAPAttribute + mapper'lari sAMAccountName yapar (-FixOnly veya otomatik).
  - LDAP yoksa: test ortamindaki yapiyi baz alarak dogru ayarlarla olusturur (-LdapBindPassword gerekir).

.PARAMETER LdapBindPassword
  AD bind parolasi (monitra@odak.local). Alternatif: .env.odak.prod.local icinde ODAK_LDAP_BIND_PASSWORD

.EXAMPLE
  .\setup-keycloak-ldap-odak.ps1 -Server 192.168.20.8 -LdapBindPassword '...' -SyncUsers -SyncGroups

.EXAMPLE
  .\setup-keycloak-ldap-odak.ps1 -Server 192.168.20.20 -FixOnly -SyncUsers
#>
param(
    [string]$Server = "192.168.20.8",
    [string]$Realm = "odak",
    [string]$LdapBindPassword = "",
    [switch]$FixOnly,
    [switch]$SyncUsers,
    [switch]$SyncGroups,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$isProd = ($Server -eq $script:OdakProdServer)
$envFile = if ($isProd) { Join-Path $repoRoot ".env.odak.prod.local" } else { Join-Path $repoRoot ".env.odak.local" }
if (Test-Path $envFile) { Import-OdakEnvFile $envFile }
if ([string]::IsNullOrWhiteSpace($LdapBindPassword) -and $env:ODAK_LDAP_BIND_PASSWORD) {
    $LdapBindPassword = $env:ODAK_LDAP_BIND_PASSWORD
}

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$bindPassB64 = ""
if ($LdapBindPassword) {
    $bindPassB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($LdapBindPassword))
}

$remote = ConvertTo-UnixShell @"
set -e
KC_BASE=http://127.0.0.1:8080/keycloak
ENV_FILE=/home/odak/mng_common/.env
[ -f "`$ENV_FILE" ] || ENV_FILE=/home/odak/MonitraNG/ApplicationResources/mng_apps/.env
KC_ADMIN=`$(grep '^KEYCLOAK_ADMIN_USERNAME=' "`$ENV_FILE" | cut -d= -f2- | tr -d "\r")
KC_PASS=`$(grep '^KEYCLOAK_ADMIN_PASSWORD=' "`$ENV_FILE" | cut -d= -f2- | tr -d "\r")
[ -n "`$KC_ADMIN" ] || KC_ADMIN=admin

export BIND_PASS_B64='$bindPassB64'
export WHATIF='$($WhatIf.IsPresent)'
export FIXONLY='$($FixOnly.IsPresent)'
export SYNC_USERS='$($SyncUsers.IsPresent)'
export SYNC_GROUPS='$($SyncGroups.IsPresent)'

python3 << 'PYEOF'
import json, os, subprocess, sys, base64, urllib.request, urllib.error

KC_BASE = "http://127.0.0.1:8080/keycloak"
REALM = "$Realm"
ENV_FILE = "/home/odak/mng_common/.env"
if not os.path.isfile(ENV_FILE):
    ENV_FILE = "/home/odak/MonitraNG/ApplicationResources/mng_apps/.env"

def read_env(key, default=""):
    try:
        with open(ENV_FILE) as f:
            for line in f:
                if line.startswith(key + "="):
                    return line.split("=", 1)[1].strip().strip('"').strip("'")
    except FileNotFoundError:
        pass
    return default

kc_admin = read_env("KEYCLOAK_ADMIN_USERNAME", "admin")
kc_pass = read_env("KEYCLOAK_ADMIN_PASSWORD", "")
bind_pass_b64 = os.environ.get("BIND_PASS_B64", "")
bind_pass = base64.b64decode(bind_pass_b64).decode("utf-8") if bind_pass_b64 else ""
whatif = os.environ.get("WHATIF", "") == "True"
fixonly = os.environ.get("FIXONLY", "") == "True"
sync_users = os.environ.get("SYNC_USERS", "") == "True"
sync_groups = os.environ.get("SYNC_GROUPS", "") == "True"

def kc_token():
    data = f"grant_type=password&client_id=admin-cli&username={kc_admin}&password={kc_pass}".encode()
    req = urllib.request.Request(f"{KC_BASE}/realms/master/protocol/openid-connect/token", data=data, method="POST")
    with urllib.request.urlopen(req) as r:
        return json.load(r)["access_token"]

def kc(method, path, body=None):
    headers = {"Authorization": f"Bearer {token}"}
    data = None
    if body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(body).encode()
    req = urllib.request.Request(f"{KC_BASE}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            payload = json.loads(raw) if raw else None
        except json.JSONDecodeError:
            payload = raw
        return e.code, payload

token = kc_token()
print("Admin token OK")

status, realm = kc("GET", f"/admin/realms/{REALM}")
if status != 200:
    print("HATA: realm bulunamadi:", REALM); sys.exit(1)
realm_id = realm["id"]

status, providers = kc("GET", f"/admin/realms/{REALM}/components?type=org.keycloak.storage.UserStorageProvider")
provider = providers[0] if providers else None

def cfg_val(c, k, default=None):
    v = c.get(k)
    if isinstance(v, list) and v:
        return v[0]
    return default

def fix_provider_and_mappers(pid):
    status, comp = kc("GET", f"/admin/realms/{REALM}/components/{pid}")
    config = comp.get("config", {})
    old = cfg_val(config, "usernameLDAPAttribute")
    config["usernameLDAPAttribute"] = ["sAMAccountName"]
    print(f"Provider usernameLDAPAttribute: {old} -> sAMAccountName")
    if not whatif:
        kc("PUT", f"/admin/realms/{REALM}/components/{pid}", {"id": pid, "name": comp["name"], "providerId": comp["providerId"], "providerType": comp["providerType"], "parentId": comp["parentId"], "config": config})

    status, mappers = kc("GET", f"/admin/realms/{REALM}/components?parent={pid}&type=org.keycloak.storage.ldap.mappers.LDAPStorageMapper")
    for m in mappers:
        mc = m.get("config", {})
        name = m.get("name", "")
        ptype = m.get("providerId", "")
        changed = False
        if ptype == "user-attribute-ldap-mapper" and cfg_val(mc, "user.model.attribute") == "username":
            old_attr = cfg_val(mc, "ldap.attribute")
            mc["ldap.attribute"] = ["sAMAccountName"]
            print(f"Mapper username ldap.attribute: {old_attr} -> sAMAccountName")
            changed = True
        if ptype == "group-ldap-mapper":
            old_mu = cfg_val(mc, "membership.user.ldap.attribute")
            mc["membership.user.ldap.attribute"] = ["sAMAccountName"]
            print(f"Mapper ldap-groups membership.user.ldap.attribute: {old_mu} -> sAMAccountName")
            changed = True
        if changed and not whatif:
            kc("PUT", f"/admin/realms/{REALM}/components/{m['id']}", {"id": m["id"], "name": m["name"], "providerId": m["providerId"], "providerType": m["providerType"], "parentId": m["parentId"], "config": mc})

def create_ldap_provider():
    if not bind_pass:
        print("HATA: Yeni LDAP icin bind parolasi gerekli (-LdapBindPassword veya ODAK_LDAP_BIND_PASSWORD)")
        sys.exit(2)
    body = {
        "name": "ldap",
        "providerId": "ldap",
        "providerType": "org.keycloak.storage.UserStorageProvider",
        "parentId": realm_id,
        "config": {
            "enabled": ["true"],
            "priority": ["0"],
            "fullSyncPeriod": ["-1"],
            "changedSyncPeriod": ["-1"],
            "cachePolicy": ["DEFAULT"],
            "evictionDay": [],
            "evictionHour": [],
            "evictionMinute": [],
            "maxLifespan": [],
            "batchSizeForSync": ["1000"],
            "editMode": ["READ_ONLY"],
            "importEnabled": ["true"],
            "syncRegistrations": ["false"],
            "pagination": ["true"],
            "allowKerberos": ["false"],
            "connectionUrl": ["LDAP://192.168.20.3:389"],
            "usersDn": ["DC=odak,DC=local"],
            "bindDn": ["monitra@odak.local"],
            "bindCredential": [bind_pass],
            "bindType": ["simple"],
            "useTruststoreSpi": ["ldapsOnly"],
            "connectionPooling": ["true"],
            "connectionPoolingAuthentication": ["simple"],
            "connectionPoolingDebug": ["false"],
            "connectionPoolingInitSize": ["0"],
            "connectionPoolingMaxSize": ["20"],
            "connectionPoolingPrefSize": ["10"],
            "connectionPoolingProtocol": ["plain"],
            "connectionPoolingTimeout": ["300000"],
            "connectionTimeout": [],
            "readTimeout": [],
            "vendor": ["ad"],
            "usernameLDAPAttribute": ["sAMAccountName"],
            "rdnLDAPAttribute": ["cn"],
            "uuidLDAPAttribute": ["objectGUID"],
            "userObjectClasses": ["person, organizationalPerson, user"],
            "searchScope": ["2"],
            "validatePasswordPolicy": ["false"],
            "trustEmail": ["false"],
            "usePasswordModifyExtendedOp": ["false"],
        },
    }
    if whatif:
        print("WhatIf: LDAP provider olusturulacak (sAMAccountName)")
        return None
    status, _ = kc("POST", f"/admin/realms/{REALM}/components", body)
    if status not in (200, 201):
        print("HATA: LDAP provider create", status); sys.exit(1)
    status, providers = kc("GET", f"/admin/realms/{REALM}/components?type=org.keycloak.storage.UserStorageProvider")
    return providers[0]["id"]

def create_mapper(pid, name, provider_id, config):
    body = {
        "name": name,
        "providerId": provider_id,
        "providerType": "org.keycloak.storage.ldap.mappers.LDAPStorageMapper",
        "parentId": pid,
        "config": config,
    }
    if whatif:
        print(f"WhatIf: mapper {name}")
        return
    status, _ = kc("POST", f"/admin/realms/{REALM}/components", body)
    if status not in (200, 201):
        print(f"HATA: mapper {name} create", status); sys.exit(1)

def create_default_mappers(pid):
    mappers = [
        ("username", "user-attribute-ldap-mapper", {
            "ldap.attribute": ["sAMAccountName"],
            "user.model.attribute": ["username"],
            "read.only": ["true"],
            "always.read.value.from.ldap": ["false"],
            "is.mandatory.in.ldap": ["true"],
        }),
        ("email", "user-attribute-ldap-mapper", {
            "ldap.attribute": ["mail"],
            "user.model.attribute": ["email"],
            "read.only": ["true"],
            "always.read.value.from.ldap": ["false"],
            "is.mandatory.in.ldap": ["false"],
        }),
        ("first name", "user-attribute-ldap-mapper", {
            "ldap.attribute": ["givenName"],
            "user.model.attribute": ["firstName"],
            "read.only": ["true"],
            "always.read.value.from.ldap": ["true"],
            "is.mandatory.in.ldap": ["true"],
        }),
        ("last name", "user-attribute-ldap-mapper", {
            "ldap.attribute": ["sn"],
            "user.model.attribute": ["lastName"],
            "read.only": ["true"],
            "always.read.value.from.ldap": ["true"],
            "is.mandatory.in.ldap": ["true"],
        }),
        ("creation date", "user-attribute-ldap-mapper", {
            "ldap.attribute": ["whenCreated"],
            "user.model.attribute": ["createTimestamp"],
            "read.only": ["true"],
            "always.read.value.from.ldap": ["true"],
            "is.mandatory.in.ldap": ["false"],
        }),
        ("modify date", "user-attribute-ldap-mapper", {
            "ldap.attribute": ["whenChanged"],
            "user.model.attribute": ["modifyTimestamp"],
            "read.only": ["true"],
            "always.read.value.from.ldap": ["true"],
            "is.mandatory.in.ldap": ["false"],
        }),
        ("MSAD account controls", "msad-user-account-control-mapper", {}),
        ("ldap-groups", "group-ldap-mapper", {
            "groups.dn": ["DC=odak,DC=local"],
            "group.name.ldap.attribute": ["cn"],
            "group.object.classes": ["group"],
            "preserve.group.inheritance": ["false"],
            "ignore.missing.groups": ["false"],
            "membership.ldap.attribute": ["member"],
            "membership.attribute.type": ["DN"],
            "membership.user.ldap.attribute": ["sAMAccountName"],
            "groups.path": ["/"],
            "mode": ["READ_ONLY"],
            "user.roles.retrieve.strategy": ["LOAD_GROUPS_BY_MEMBER_ATTRIBUTE"],
            "memberof.ldap.attribute": ["memberOf"],
            "drop.non.existing.groups.during.sync": ["false"],
        }),
    ]
    for name, ptype, cfg in mappers:
        create_mapper(pid, name, ptype, cfg)

def trigger_user_sync(pid, action):
    if whatif:
        print(f"WhatIf: user sync {action}")
        return
    status, resp = kc("POST", f"/admin/realms/{REALM}/user-storage/{pid}/sync?action={action}")
    print(f"Sync {action}: HTTP {status}", resp if resp else "")

def trigger_group_sync(pid):
    status, mappers = kc("GET", f"/admin/realms/{REALM}/components?parent={pid}&type=org.keycloak.storage.ldap.mappers.LDAPStorageMapper")
    mapper_id = next((m["id"] for m in mappers if m.get("providerId") == "group-ldap-mapper"), None)
    if not mapper_id:
        print("HATA: group-ldap-mapper bulunamadi"); sys.exit(1)
    if whatif:
        print(f"WhatIf: group mapper sync {mapper_id}")
        return
    status, resp = kc("POST", f"/admin/realms/{REALM}/user-storage/{pid}/mappers/{mapper_id}/sync?direction=fedToKeycloak")
    print(f"Sync groups: HTTP {status}", resp if resp else "")

if provider:
    pid = provider["id"]
    print(f"Mevcut LDAP provider: {pid}")
    fix_provider_and_mappers(pid)
elif fixonly:
    print("HATA: FixOnly ama LDAP provider yok"); sys.exit(1)
else:
    print("LDAP provider yok — olusturuluyor...")
    pid = create_ldap_provider()
    if pid:
        print(f"Provider olusturuldu: {pid}")
        create_default_mappers(pid)

if pid and sync_users:
    trigger_user_sync(pid, "triggerFullSync")
if pid and sync_groups:
    trigger_group_sync(pid)

print("=== Tamamlandi ===")
PYEOF
"@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 600
    $r.Output | ForEach-Object { Write-Host $_ }
    if ($r.ExitStatus -ne 0) {
        if ($r.Error) { Write-Host $r.Error -ForegroundColor Red }
        exit $r.ExitStatus
    }
} finally {
    Remove-SSHSession $s.SessionId | Out-Null
}
