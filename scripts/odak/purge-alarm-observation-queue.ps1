# Odak alarm.observation.inbound kuyrugunu temizle (benchmark/E2E oncesi backlog)
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
$c = Get-OdakSshCredential -User odak -Server 192.168.20.20
$s = New-SSHSession -ComputerName 192.168.20.20 -Credential $c -AcceptKey
try {
    $mq = Get-OdakRabbitMqCredentials -SshSession $s
    $pw = $mq.Password.Replace("'", "'\''")
    $cmd = "docker exec rabbitmq rabbitmqadmin -u $($mq.Username) -p '$pw' purge queue name=alarm.observation.inbound"
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 30
    $r.Output
    Invoke-SSHCommand -SessionId $s.SessionId -Command "docker restart mngalarm-worker" -TimeOut 60 | Out-Null
    Start-Sleep -Seconds 8
    Write-Host "alarm.observation.inbound purged + mngalarm-worker restarted" -ForegroundColor Green
    exit 0
} finally {
    Remove-SSHSession -SessionId $s.SessionId | Out-Null
}
