param([string]$Server = "192.168.20.8")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$cmd = ConvertTo-UnixShell @"
echo '=== mng_common ==='
grep -E '^MINIO_' /home/odak/mng_common/.env 2>/dev/null || true
echo '=== mng_apps ==='
grep -E '^MINIO_' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env 2>/dev/null || true
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 20
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
