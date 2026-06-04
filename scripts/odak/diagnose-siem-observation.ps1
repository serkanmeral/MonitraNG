# SIEM observation pipeline diagnostic (Odak SSH)
param([string]$Server = "192.168.20.20", [string]$User = "odak")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$cmd = @'
echo "=== mngreactor ObservationPublish env ==="
docker exec mngreactor printenv 2>/dev/null | grep -i Observation || echo none
echo "=== mngalarm-worker observation env ==="
docker exec mngalarm-worker printenv 2>/dev/null | grep -iE "ConsumeObservations|Observation" || echo none
echo "=== rabbitmq queues ==="
docker exec rabbitmq rabbitmqctl list_queues name messages consumers 2>/dev/null | head -40
echo "=== mngreactor recent logs ==="
docker logs mngreactor 2>&1 | tail -25
echo "=== mngalarm-worker recent logs ==="
docker logs mngalarm-worker 2>&1 | tail -25
'@

$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd -TimeOut 60
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
