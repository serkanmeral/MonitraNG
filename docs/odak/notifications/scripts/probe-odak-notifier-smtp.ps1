# Odak mngnotifier SMTP ve log kontrolu (sifre maskelenir)
param([string]$Server = "192.168.20.20")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
$repoRoot = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
$odakScripts = Join-Path $repoRoot "scripts/odak"
. (Join-Path $odakScripts "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$remote = @'
echo "=== .env SMTP ==="
grep '^SMTP_' /home/odak/MonitraNG/ApplicationResources/mng_apps/.env | sed 's/PASSWORD=.*/PASSWORD=***/'
echo "=== mngnotifier runtime SMTP ==="
docker exec mngnotifier printenv | grep 'MngNotifierSettings__Mail' | sed 's/Password=.*/Password=***/'
echo "=== mngnotifier logs (smtp/template) ==="
docker logs mngnotifier --tail 30 2>&1 | grep -iE 'smtp|template|mail|Connection|error|sent' || echo "(no matching lines)"
echo "=== mngoperations mail logs ==="
docker logs mngoperations --tail 50 2>&1 | grep -iE 'notif|mail|template|send-template' || echo "(no matching lines)"
'@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 60
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
