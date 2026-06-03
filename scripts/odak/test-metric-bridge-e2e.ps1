# Publish test metric to mng.topics on Odak and verify MngAlarm bridge
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$payload = '{"domainName":"odak","domainId":"odak","collectibleCode":"cpu_usage","value":97,"assetId":"bridge-e2e"}'

Write-Host "Publishing metric to mng.topics..."
$r = Invoke-OdakRabbitMqPublish -SshSession $session -Exchange "mng.topics" -RoutingKey "monitoring.metric.inserted.odak" -Payload $payload
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ } }

Start-Sleep -Seconds 4
Write-Host "`nAlarm worker logs:"
$r2 = Invoke-SSHCommand -SessionId $session.SessionId -Command "docker logs mngalarm-worker 2>&1 | tail -12" -TimeOut 30
$r2.Output | ForEach-Object { Write-Host $_ }

Remove-SSHSession -SessionId $session.SessionId | Out-Null

$token = (Get-Content "$env:TEMP\serkan_token.txt" -Raw).Trim()
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = "odak" }
$ing = Invoke-RestMethod -Uri "http://${Server}:5040/alarm/api/v1/dev/observations/ingest" -Method POST -Headers $hdr -ContentType "application/json" -Body '{"domainName":"odak","key":"cpu_usage","value":98}'
Write-Host "`nFollow-up dev ingest: raised=$($ing.alarmsRaised) updated=$($ing.alarmsUpdated)"
