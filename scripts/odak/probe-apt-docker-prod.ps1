param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$sp = $env:ODAK_SSH_PASSWORD.Replace("'", "'\''")
$cmd = ConvertTo-UnixShell @"
SP='$sp'
run_sudo() { echo "`$SP" | sudo -S "`$@"; }
run_sudo apt-get update -qq
apt-cache search '^docker' 2>/dev/null | head -15
echo '---'
apt-cache policy docker.io docker-ce docker-compose-plugin 2>/dev/null | head -25
echo '--- docker.list ---'
cat /etc/apt/sources.list.d/docker.list 2>/dev/null || echo none
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
