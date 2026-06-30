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
python3 << 'PYEOF'
import json, urllib.request, urllib.parse, urllib.error, os, sys

env_file = "/home/odak/mng_common/.env"
if not os.path.isfile(env_file):
    env_file = "/home/odak/MonitraNG/ApplicationResources/mng_apps/.env"
admin = passw = None
with open(env_file) as f:
    for line in f:
        if line.startswith("KEYCLOAK_ADMIN_USERNAME="):
            admin = line.split("=", 1)[1].strip()
        if line.startswith("KEYCLOAK_ADMIN_PASSWORD="):
            passw = line.split("=", 1)[1].strip()

body = urllib.parse.urlencode({
    "grant_type": "password",
    "client_id": "admin-cli",
    "username": admin,
    "password": passw,
}).encode()
req = urllib.request.Request(
    "http://127.0.0.1:8080/keycloak/realms/master/protocol/openid-connect/token",
    data=body,
    method="POST",
)
with urllib.request.urlopen(req, timeout=60) as r:
    token = json.load(r)["access_token"]

base = "http://127.0.0.1:8080/keycloak/admin/realms/$Realm/users"

def test_page(first, page_size, brief=None):
    qs = f"first={first}&max={page_size}"
    if brief is not None:
        qs += f"&briefRepresentation={str(brief).lower()}"
    url = f"{base}?{qs}"
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            raw = r.read()
    except urllib.error.HTTPError as he:
        body = he.read().decode("utf-8", errors="replace") if he.fp else ""
        print(f"HTTP {he.code} first={first} max={page_size} brief={brief} body={body[:200]}")
        return None, he, None
    label = f"first={first} max={page_size} brief={brief}"
    try:
        users = json.loads(raw.decode("utf-8"))
        print(f"OK {label} bytes={len(raw)} count={len(users)}")
        return users, None, raw
    except json.JSONDecodeError as e:
        s = raw.decode("utf-8", errors="replace")
        pos = e.pos
        start = max(0, pos - 150)
        end = min(len(s), pos + 150)
        print(f"FAIL {label} bytes={len(raw)} error={e}")
        print("CONTEXT:", repr(s[start:end]))
        return None, e, raw

print("=== Paginated user list (max=100) ===")
first = 0
page_size = 100
total = 0
list100_ok = True
while True:
    users, err, raw = test_page(first, page_size)
    if err:
        list100_ok = False
        break
    total += len(users)
    if len(users) == 0 or len(users) < page_size:
        break
    first += page_size
if list100_ok:
    print(f"TOTAL users listed: {total}")

print("=== briefRepresentation comparison (first page) ===")
for br in [True, False]:
    users, err, raw = test_page(0, 100, br)
    if err:
        print(f"briefRepresentation={br} FAILED")
    elif users:
        print(f"briefRepresentation={br} sample usernames: {[u.get('username') for u in users[:3]]}")

print("=== Smaller page sizes ===")
for ps in [10, 25, 50]:
    users, err, raw = test_page(0, ps)
    if err:
        print(f"max={ps} FAILED")
        break

print("=== Full sync simulation with max=25 ===")
first = 0
page_size = 25
total = 0
while True:
    users, err, raw = test_page(first, page_size)
    if err:
        print(f"FAILED at first={first}")
        sys.exit(1)
    total += len(users)
    if len(users) == 0 or len(users) < page_size:
        break
    first += page_size
print(f"TOTAL with max=25: {total}")

print("=== Full sync simulation with max=50 ===")
first = 0
page_size = 50
total = 0
while True:
    users, err, raw = test_page(first, page_size)
    if err:
        print(f"FAILED at first={first}")
        sys.exit(1)
    total += len(users)
    if len(users) == 0 or len(users) < page_size:
        break
    first += page_size
print(f"TOTAL with max=50: {total}")

print("=== Edge: first=50 max=50 (second page) ===")
test_page(50, 50)
test_page(50, 100)

print("=== Pinpoint bad offset (max=1 from 70) ===")
for i in range(70, 85):
    users, err, raw = test_page(i, 1)
    if err:
        print(f"  offset {i}: FAIL")
    elif users:
        print(f"  offset {i}: OK username={users[0].get('username')}")
    else:
        print(f"  offset {i}: empty")

print("=== Inspect user talasli ===")
# find talasli via search
url = f"{base}?username=talasli&exact=true"
req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
with urllib.request.urlopen(req, timeout=30) as r:
    raw = r.read()
print("search talasli bytes=", len(raw))
try:
    arr = json.loads(raw.decode("utf-8"))
    print("search result count=", len(arr))
    if arr:
        uid = arr[0].get("id")
        print("id=", uid, "keys=", sorted(arr[0].keys()))
        url2 = f"{base}/{uid}"
        req2 = urllib.request.Request(url2, headers={"Authorization": f"Bearer {token}"})
        with urllib.request.urlopen(req2, timeout=30) as r2:
            raw2 = r2.read()
        print("single GET bytes=", len(raw2))
        one = json.loads(raw2.decode("utf-8"))
        print("single GET OK username=", one.get("username"))
        attrs = one.get("attributes") or {}
        print("attribute keys:", list(attrs.keys())[:20])
except Exception as ex:
    print("talasli inspect error:", ex)
    print("raw snippet:", repr(raw.decode("utf-8", errors="replace")[:500]))

PYEOF
"@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 180
    $r.Output | ForEach-Object { Write-Host $_ }
    if ($r.Error) { Write-Host "STDERR: $($r.Error)" -ForegroundColor Yellow }
    if ($r.ExitStatus -ne 0) { exit $r.ExitStatus }
}
finally {
    Remove-SSHSession -SessionId $s.SessionId | Out-Null
}
