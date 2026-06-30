param([string]$Server = "192.168.20.20", [string]$Realm = "odak")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$remote = ConvertTo-UnixShell @"
python3 << 'PYEOF'
import json, urllib.request, urllib.parse, os
env_file = "/home/odak/mng_common/.env"
admin = passw = None
with open(env_file) as f:
    for line in f:
        if line.startswith("KEYCLOAK_ADMIN_USERNAME="):
            admin = line.split("=", 1)[1].strip()
        if line.startswith("KEYCLOAK_ADMIN_PASSWORD="):
            passw = line.split("=", 1)[1].strip()
body = urllib.parse.urlencode({"grant_type": "password", "client_id": "admin-cli", "username": admin, "password": passw}).encode()
req = urllib.request.Request("http://127.0.0.1:8080/keycloak/realms/master/protocol/openid-connect/token", data=body, method="POST")
token = json.load(urllib.request.urlopen(req))["access_token"]
base = "http://127.0.0.1:8080/keycloak/admin/realms/$Realm/users"

def page(first, mx):
    url = f"{base}?first={first}&max={mx}"
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
    raw = urllib.request.urlopen(req, timeout=60).read()
    return json.loads(raw.decode())

for i in range(70, 85):
    try:
        u = page(i, 1)
        if not u:
            print(f"offset {i}: empty")
        else:
            print(f"offset {i}: OK username={u[0].get('username')} id={u[0].get('id')}")
    except Exception as e:
        print(f"offset {i}: FAIL {e}")

# user count
req = urllib.request.Request(f"{base}/count", headers={"Authorization": f"Bearer {token}"})
count = json.load(urllib.request.urlopen(req))
print("user count:", count)

for mx in [1, 2, 3, 5, 10, 25]:
    try:
        u = page(75, mx)
        print(f"first=75 max={mx}: OK count={len(u)} users={[x.get('username') for x in u]}")
    except Exception as e:
        print(f"first=75 max={mx}: FAIL {e}")

for mx in [77, 78, 100]:
    try:
        u = page(0, mx)
        print(f"first=0 max={mx}: OK count={len(u)}")
    except Exception as e:
        print(f"first=0 max={mx}: FAIL {e}")

for mx in [27, 28]:
    try:
        u = page(50, mx)
        print(f"first=50 max={mx}: OK count={len(u)}")
    except Exception as e:
        print(f"first=50 max={mx}: FAIL {e}")
PYEOF
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 120
    $r.Output | ForEach-Object { Write-Host $_ }
} finally { Remove-SSHSession -SessionId $s.SessionId | Out-Null }
