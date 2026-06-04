param([string]$Server = "192.168.20.8", [string]$Path = "/home/odak/MonitraNG/ApplicationResources/mng_apps")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command "ls -la '$Path' 2>&1; ls -la '$Path/.env' 2>&1; find /home/odak/MonitraNG -maxdepth 4 -type f -name 'docker-compose*.yml' 2>/dev/null"
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
