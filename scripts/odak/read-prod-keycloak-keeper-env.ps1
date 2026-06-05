param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$cmd = ConvertTo-UnixShell @"
echo '=== mng_apps .env ==='
grep -E '^KEYCLOAK_' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env 2>/dev/null | sed 's/SECRET=.*/SECRET=***REDACTED***/'
echo '=== mngkeeper container env ==='
docker inspect mngkeeper --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null | grep -i keycloak | sed 's/ClientSecret=.*/ClientSecret=***REDACTED***/'
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 30
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
