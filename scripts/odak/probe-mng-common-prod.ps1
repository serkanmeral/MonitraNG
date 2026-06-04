param([string]$Server = "192.168.20.8")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$cmd = ConvertTo-UnixShell @"
echo '=== docker ==='
docker --version 2>&1 || echo NO_DOCKER
groups
echo '=== mng_common files ==='
ls -la /home/odak/mng_common/ | head -20
test -f /home/odak/mng_common/docker-compose.odak.prod.yml && echo PROD_COMPOSE=ok
test -f /home/odak/mng_common/.env && echo ENV=ok
echo '=== containers ==='
docker ps -a 2>&1 | head -15 || true
docker network ls 2>&1 | grep mng || true
"@
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 30
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
