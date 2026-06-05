param([string]$Server = "192.168.20.8")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$sudoPass = $env:ODAK_SSH_PASSWORD
if ([string]::IsNullOrWhiteSpace($sudoPass)) { $sudoPass = $env:ODAK_PROD_SSH_PASSWORD }
$escaped = $sudoPass.Replace("'", "'\''")
$cmd = ConvertTo-UnixShell @"
echo '=== groups ==='
groups
echo '=== docker paths ==='
command -v docker 2>&1 || true
ls -la /usr/bin/docker 2>&1 || true
echo '=== sudo docker ==='
echo '$escaped' | sudo -S docker --version 2>&1 || true
echo '$escaped' | sudo -S systemctl is-active docker 2>&1 || true
echo '=== network curl docker.com ==='
curl -fsSL -o /dev/null -w 'docker_gpg=%{http_code}\n' https://download.docker.com/linux/debian/gpg 2>&1 || echo curl_fail
echo '=== dns ==='
getent hosts download.docker.com 2>&1 || true
"@
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 45
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
