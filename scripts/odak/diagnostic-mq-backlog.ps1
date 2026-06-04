# RabbitMQ backlog triage — Odak workflow/alarm kuyrukları
# Kullanım: pwsh scripts/odak/diagnostic-mq-backlog.ps1 [-SampleSec 30]

param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [int]$SampleSec = 30,
    [string[]]$WatchQueues = @(
        "workflow.execution",
        "workflow.event.inbound",
        "alarm.observation.inbound",
        "workflow.deadletter"
    )
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

    $py = @'
import json, sys, urllib.request, base64
user, pwd, queues = sys.argv[1], sys.argv[2], sys.argv[3].split(",")
auth = base64.b64encode(f"{user}:{pwd}".encode()).decode()
req = urllib.request.Request("http://127.0.0.1:15672/api/queues/%2F")
req.add_header("Authorization", f"Basic {auth}")
with urllib.request.urlopen(req, timeout=20) as resp:
    data = json.load(resp)
watch = set(queues)
rows = []
for q in data:
    name = q.get("name", "")
    if name not in watch and (q.get("messages", 0) > 0 or q.get("consumers", 0) > 0):
        pass
    if name in watch or q.get("messages", 0) > 0:
        stats = q.get("message_stats") or {}
        pub = (stats.get("publish_details") or {}).get("rate") or 0
        deliv = (stats.get("deliver_get_details") or {}).get("rate") or 0
        rows.append((name, q.get("messages", 0), q.get("messages_ready", 0),
                     q.get("messages_unacknowledged", 0), q.get("consumers", 0), pub, deliv))
rows.sort(key=lambda r: -r[1])
print("QUEUE\tMSGS\tREADY\tUNACK\tCONSUMERS\tPUB/s\tDEL/s")
for r in rows[:30]:
    print("%s\t%d\t%d\t%d\t%d\t%.2f\t%.2f" % r)
'@

    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
    $queueList = ($WatchQueues -join ",")
    $snap1Cmd = "echo '$b64' | base64 -d > /tmp/mq_triage.py && python3 /tmp/mq_triage.py '$u' '$p' '$queueList'"
    Write-Host "=== RabbitMQ snapshot (t=0) ===" -ForegroundColor Cyan
    $s1 = Invoke-SSHCommand -SessionId $session.SessionId -Command $snap1Cmd -TimeOut 45
    $s1.Output | ForEach-Object { Write-Host $_ }

    Write-Host "`n=== Worker CPU (docker stats) ===" -ForegroundColor Cyan
    $stats = Invoke-SSHCommand -SessionId $session.SessionId -Command "docker stats --no-stream --format '{{.Name}} {{.CPUPerc}} {{.MemUsage}}' mngworkflow-worker mngalarm-worker 2>/dev/null" -TimeOut 30
    $stats.Output | ForEach-Object { Write-Host $_ }

    if ($SampleSec -gt 0) {
        Write-Host "`nSampling ${SampleSec}s for drain rate..." -ForegroundColor DarkGray
        Start-Sleep -Seconds $SampleSec
        Write-Host "=== RabbitMQ snapshot (t=${SampleSec}s) ===" -ForegroundColor Cyan
        $s2 = Invoke-SSHCommand -SessionId $session.SessionId -Command $snap1Cmd -TimeOut 45
        $s2.Output | ForEach-Object { Write-Host $_ }
    }

    Write-Host "`n=== workflow.deadletter (son 3 mesaj özeti) ===" -ForegroundColor Cyan
    $dlqCmd = @"
curl -s -u '$u':'$p' 'http://127.0.0.1:15672/api/queues/%2F/workflow.deadletter/get' \
  -H 'content-type: application/json' \
  -d '{"count":3,"ackmode":"ack_requeue_true","encoding":"auto"}' \
  | python3 -c "import sys,json; ms=json.load(sys.stdin); print('count',len(ms));
[print((m.get('properties',{}).get('headers') or {}), (m.get('payload','')[:120])) for m in ms]"
"@
    $dlq = Invoke-SSHCommand -SessionId $session.SessionId -Command $dlqCmd -TimeOut 30
    $dlq.Output | ForEach-Object { Write-Host $_ }

    Write-Host "`n=== Son workflow-worker log (hata?) ===" -ForegroundColor Cyan
    $wl = Invoke-SSHCommand -SessionId $session.SessionId -Command "docker logs mngworkflow-worker --tail 8 2>&1" -TimeOut 20
    $wl.Output | ForEach-Object { Write-Host $_ }
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
