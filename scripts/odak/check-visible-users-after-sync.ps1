param([string]$Server = "192.168.20.20")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$remote = ConvertTo-UnixShell @'
docker exec -i mongodb mongosh --quiet mng_odak --eval '
const visible = db.getCollection("@users").find({includeInApplication:true},{username:1,firstName:1,lastName:1,provisioningSource:1}).toArray();
const demoted = db.getCollection("@users").find({includeInApplication:false, provisioningSource:"Directory", firstName:{$exists:true,$ne:""}}).limit(5).toArray();
print("visible_count=" + visible.length);
print("visible_sample=" + JSON.stringify(visible.slice(0,5).map(u=>u.username)));
print("demoted_check_count=" + demoted.length);
'
'@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try { Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 60 | Select-Object -ExpandProperty Output | ForEach-Object { Write-Host $_ } }
finally { Remove-SSHSession -SessionId $s.SessionId | Out-Null }
