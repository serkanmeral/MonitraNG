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
headers = {"Authorization": f"Bearer {token}"}
for resource in ["users", "groups"]:
    url = f"http://127.0.0.1:8080/keycloak/admin/realms/$Realm/{resource}/count"
    raw = urllib.request.urlopen(urllib.request.Request(url, headers=headers)).read()
    print(f"{resource}/count raw={raw!r} decoded={raw.decode()!r}")
PYEOF
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try { Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 60 | Select-Object -ExpandProperty Output | ForEach-Object { Write-Host $_ } }
finally { Remove-SSHSession -SessionId $s.SessionId | Out-Null }
