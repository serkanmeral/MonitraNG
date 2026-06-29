Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "../../../scripts/odak/OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server 192.168.20.8
$cred = Get-OdakSshCredential -User odak -Server 192.168.20.8
$session = New-SSHSession -ComputerName 192.168.20.8 -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command "docker logs mngdocument --tail 60 2>&1"
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
