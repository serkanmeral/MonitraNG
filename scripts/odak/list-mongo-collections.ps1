param(
    [string]$Server = "192.168.20.20",
    [string]$Db = "mng_odak"
)
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$cmd = ConvertTo-UnixShell @"
docker exec mongo mongosh -u admin -p admin123 --authenticationDatabase admin --quiet --eval "
  const d=db.getSiblingDB('$Db');
  const cols=d.getCollectionNames().sort();
  print('DB=$Db count=' + cols.length);
  cols.forEach(c => print('  ' + c + ' docs=' + d.getCollection(c).countDocuments()));
"
"@
$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession $s.SessionId
