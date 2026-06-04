# Odak workflow.execution kuyrugunu temizle (E2E oncesi backlog)
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
$c = Get-OdakSshCredential -User odak -Server 192.168.20.20
$s = New-SSHSession -ComputerName 192.168.20.20 -Credential $c -AcceptKey
try {
    $mq = Get-OdakRabbitMqCredentials -SshSession $s
    $pw = $mq.Password.Replace("'", "'\''")
    $cmd = "docker exec rabbitmq rabbitmqadmin -u $($mq.Username) -p '$pw' purge queue name=workflow.execution"
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 30
    $r.Output
    Invoke-SSHCommand -SessionId $s.SessionId -Command "docker restart mngworkflow-worker" -TimeOut 60 | Out-Null
    Start-Sleep -Seconds 8
    Write-Host "workflow.execution purged + worker restarted" -ForegroundColor Green
} finally {
    Remove-SSHSession -SessionId $s.SessionId | Out-Null
}
