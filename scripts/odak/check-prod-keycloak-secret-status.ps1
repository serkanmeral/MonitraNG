param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$cmd = ConvertTo-UnixShell @'
sec=$(grep '^KEYCLOAK_CLIENT_SECRET=' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env | cut -d= -f2- | tr -d "\r")
case "$sec" in CHANGE_ME*|*CHANGE_ME*) echo "SECRET_STATUS=PLACEHOLDER" ;; "" ) echo "SECRET_STATUS=EMPTY" ;; *) echo "SECRET_STATUS=SET (len ${#sec})" ;; esac
echo "CLIENT_ID=$(grep '^KEYCLOAK_CLIENT_ID=' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env | cut -d= -f2-)"
'@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 20
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
