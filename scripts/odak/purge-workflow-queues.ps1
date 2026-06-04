# Odak lab — workflow/alarm RabbitMQ kuyruklarini bosalt (E2E / birikim temizligi)
# Varsayilan dry-run; -Apply ile purge eder. workflow.deadletter korunur (inceleme icin).
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string[]]$Queues = @(
        "workflow.execution",
        "workflow.event.inbound",
        "alarm.observation.inbound"
    ),
    [switch]$Apply
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

    Write-Host "=== Workflow/alarm queue purge ===" -ForegroundColor Cyan
    if (-not $Apply) { Write-Host "   Dry-run (-Apply ile purge)" -ForegroundColor Yellow }

    foreach ($q in $Queues) {
        $enc = [uri]::EscapeDataString($q)
        $info = Invoke-SSHCommand -SessionId $session.SessionId -Command "curl -s -u '$u':'$p' 'http://127.0.0.1:15672/api/queues/%2F/$enc'" -TimeOut 20
        $json = ($info.Output -join "") | ConvertFrom-Json
        $count = [int]$json.messages
        Write-Host "   $q messages=$count consumers=$($json.consumers)" -ForegroundColor DarkGray
        if ($Apply -and $count -gt 0) {
            Invoke-SSHCommand -SessionId $session.SessionId -Command "curl -s -u '$u':'$p' -X DELETE 'http://127.0.0.1:15672/api/queues/%2F/$enc/contents'" -TimeOut 60 | Out-Null
            Write-Host "   PURGED $q ($count)" -ForegroundColor Green
        }
    }

    if ($Apply) { Write-Host "`nOK purge tamamlandi" -ForegroundColor Green }
    else { Write-Host "`nOK dry-run (-Apply ile purge)" -ForegroundColor Green }
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
