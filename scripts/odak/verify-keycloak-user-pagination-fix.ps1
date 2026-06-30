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
headers = {"Authorization": f"Bearer {token}"}

count = int(urllib.request.urlopen(urllib.request.Request(f"{base}/count", headers=headers)).read())
print("count:", count)

page_size = 100
first = 0
all_users = []
while first < count:
    mx = min(page_size, count - first)
    url = f"{base}?first={first}&max={mx}"
    raw = urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=120).read()
    page = json.loads(raw.decode())
    all_users.extend(page)
    first += len(page)
    if len(page) < mx:
        break

print("listed:", len(all_users), "expected:", count)
print("OK" if len(all_users) == count else "MISMATCH")
PYEOF
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 120
    $r.Output | ForEach-Object { Write-Host $_ }
} finally { Remove-SSHSession -SessionId $s.SessionId | Out-Null }
