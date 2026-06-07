param([string]$Server = "192.168.20.20")
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
$repoRoot = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
. (Join-Path $repoRoot "scripts/odak/OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command "docker restart mngoperations" -TimeOut 60
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
Start-Sleep -Seconds 15
$h = Invoke-RestMethod -Uri "http://${Server}:5040/operations/api/v1/health/live" -Method GET
Write-Host "MO health: $($h.status)"
