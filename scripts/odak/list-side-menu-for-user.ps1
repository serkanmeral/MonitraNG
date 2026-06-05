param(
    [string]$Server = "192.168.20.8",
    [string]$Username = "serkan.meral",
    [switch]$IsManager,
    [switch]$IsAdmin
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$remote = ConvertTo-UnixShell @"
python3 << 'PYEOF'
import json, subprocess, urllib.request

USERNAME = "$Username"
FORCE_MANAGER = $($IsManager.IsPresent)
FORCE_ADMIN = $($IsAdmin.IsPresent)

def kc_token():
    import os
    env = open("/home/odak/mng_common/.env").read()
    kc_pass = next(l.split("=",1)[1].strip().strip('"').strip("'") for l in env.splitlines() if l.startswith("KEYCLOAK_ADMIN_PASSWORD="))
    data = f"grant_type=password&client_id=admin-cli&username=admin&password={kc_pass}".encode()
    req = urllib.request.Request("http://127.0.0.1:8080/keycloak/realms/master/protocol/openid-connect/token", data=data, method="POST")
    with urllib.request.urlopen(req) as r:
        return json.load(r)["access_token"]

def kc_get(path, tok):
    req = urllib.request.Request(f"http://127.0.0.1:8080/keycloak{path}", headers={"Authorization": f"Bearer {tok}"})
    with urllib.request.urlopen(req) as r:
        return json.load(r)

tok = kc_token()
users = kc_get(f"/admin/realms/odak/users?username={USERNAME}&exact=true", tok)
if not users:
    print(f"HATA: Keycloak kullanici yok: {USERNAME}"); raise SystemExit(1)
uid = users[0]["id"]
kc_groups = kc_get(f"/admin/realms/odak/users/{uid}/groups", tok)
kc_group_names = [g["name"] for g in kc_groups]
print("=== Keycloak gruplari ===")
for g in sorted(kc_group_names):
    print(f"  - {g}")

mongo_user_js = f'const u=db.getSiblingDB("mng_odak").getCollection("@users").findOne({{username:"{USERNAME}"}}); print(JSON.stringify(u && u.groups || []));'
mongo_groups_raw = subprocess.check_output([
    "docker", "exec", "mongo", "mongosh", "-u", "admin", "-p", "admin123",
    "--authenticationDatabase", "admin", "--quiet", "--eval", mongo_user_js
], text=True).strip()
mongo_groups = json.loads(mongo_groups_raw) if mongo_groups_raw else []
print("=== Mongo @users gruplari (JWT / menu filtre) ===")
for g in mongo_groups:
    print(f"  - {g}")

group_names = mongo_groups if mongo_groups else kc_group_names
is_admin = FORCE_ADMIN or any(str(g).lower() == "admins" for g in group_names)
is_manager = FORCE_MANAGER or is_admin or any(str(g).lower() == "managers" for g in group_names)
print(f"is_admin={is_admin} is_manager={is_manager}")

mongo_js = '''
const items = db.getSiblingDB("mng_odak").getCollection("@side_menu").find({}).sort({order:1, level:1}).toArray();
print(JSON.stringify(items));
'''
raw = subprocess.check_output([
    "docker", "exec", "mongo", "mongosh", "-u", "admin", "-p", "admin123",
    "--authenticationDatabase", "admin", "--quiet", "--eval", mongo_js
], text=True)
items = json.loads(raw.strip())

def build_tree(flat):
    by_id = {i.get("__dataId") or i.get("_id", {}).get("$oid", str(idx)): i for idx, i in enumerate(flat)}
    for i in flat:
        i["children"] = []
    roots = []
    for i in flat:
        pid = i.get("parentId")
        if pid and pid in by_id:
            by_id[pid].setdefault("children", []).append(i)
        elif not pid:
            roots.append(i)
    def sort_nodes(nodes):
        nodes.sort(key=lambda x: (x.get("order") or 0, x.get("level") or 0))
        for n in nodes:
            if n.get("children"):
                sort_nodes(n["children"])
    sort_nodes(roots)
    return roots

def visible(item, user_groups, is_admin, is_manager):
    page_type = item.get("pageType") or "user"
    if page_type == "admin" and not is_admin:
        return False
    if page_type == "manager" and not is_manager and not is_admin:
        return False
    if page_type == "manager" and is_manager:
        return True
    perms = item.get("permissions") or {}
    groups_perm = perms.get("groups") or {}
    if groups_perm:
        perm_lower = {k.lower(): v for k, v in groups_perm.items()}
        for gn in user_groups:
            gp = groups_perm.get(gn) or perm_lower.get(str(gn).lower())
            if gp and gp.get("view"):
                return True
        return False
    if page_type == "admin":
        return False
    return True

def filter_tree(nodes, user_groups, is_admin, is_manager):
    out = []
    for item in nodes:
        if not visible(item, user_groups, is_admin, is_manager):
            continue
        copy = dict(item)
        ch = item.get("children") or []
        if ch:
            copy["children"] = filter_tree(ch, user_groups, is_admin, is_manager)
        out.append(copy)
    out.sort(key=lambda x: (x.get("order") or 0))
    return out

tree = build_tree(items)
visible_tree = filter_tree(tree, group_names, is_admin, is_manager)

def print_tree(nodes, indent=0):
    for n in nodes:
        prefix = "  " * indent
        if n.get("itemType") == "header":
            label = n.get("header") or n.get("title") or "(header)"
            print(f"{prefix}[HEADER] {label}  (order={n.get('order')}, pageType={n.get('pageType') or '-'})")
        else:
            title = n.get("title") or n.get("pageCode") or "?"
            route = n.get("to") or "-"
            pt = n.get("pageType") or "user"
            print(f"{prefix}- {title}  -> {route}  [{pt}]")
        if n.get("children"):
            print_tree(n["children"], indent + 1)

def walk(nodes):
    for n in nodes:
        yield n
        yield from walk(n.get("children") or [])

print("")
print("=== Gorunur side menu ===")
print_tree(visible_tree)
flat_count = sum(1 for _ in walk(visible_tree))
print("")
print(f"Toplam gorunur oge (header+item): {flat_count}")

PYEOF
"@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) { exit 1 }
Remove-SSHSession $s.SessionId
