# Odak workflow.deadletter kuyrugu — mesaj ozeti (root cause triage)
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [int]$SampleCount = 10
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

try {
    $mq = Get-OdakRabbitMqCredentials -SshSession $session
    $u = $mq.Username
    $p = $mq.Password.Replace("'", "'\''")

    Write-Host "=== workflow.deadletter triage ===" -ForegroundColor Cyan

    $info = Invoke-SSHCommand -SessionId $session.SessionId -Command "curl -s -u '$u':'$p' 'http://127.0.0.1:15672/api/queues/%2F/workflow.deadletter'" -TimeOut 20
    $q = ($info.Output -join "") | ConvertFrom-Json
    Write-Host "   messages=$($q.messages) consumers=$($q.consumers)" -ForegroundColor DarkGray

    if ([int]$q.messages -eq 0) {
        Write-Host "`nOK kuyruk bos" -ForegroundColor Green
        exit 0
    }

    $peekCmd = @"
curl -s -u '$u':'$p' 'http://127.0.0.1:15672/api/queues/%2F/workflow.deadletter/get' \
  -H 'content-type: application/json' \
  -d '{"count":$SampleCount,"ackmode":"ack_requeue_true","encoding":"auto"}' \
  | python3 -c "
import sys, json
msgs = json.load(sys.stdin)
print('samples', len(msgs))
for i, m in enumerate(msgs):
    props = m.get('properties') or {}
    headers = props.get('headers') or {}
    body = (m.get('payload') or '')[:400]
    print('---', i + 1, '---')
    print('headers', headers)
    print('body', body)
"
"@
    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $peekCmd -TimeOut 45
    $r.Output | ForEach-Object { Write-Host $_ }

    Write-Host "`nNot: DLQ mesajlari genelde workflow execution hatasi (node timeout, block.ip, vb.)" -ForegroundColor DarkGray
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
