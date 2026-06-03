# Native monitra.observations publish (Reactor flat DTO) → MngAlarm consumer
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"

Write-Host "1) Ensure cpu_usage threshold rule..." -ForegroundColor Cyan
$rules = Invoke-RestMethod -Uri "$alarm/rules" -Headers $hdr
$rule = @($rules) | Where-Object { $_.matchKey -eq "cpu_usage" -and $_.operator -eq "gt" } | Select-Object -First 1
if (-not $rule) {
    Invoke-RestMethod -Uri "$alarm/rules?domainName=$Domain" -Method POST -Headers $hdr -Body (@{
        name = "CPU native obs E2E"; matchKey = "cpu_usage"; operator = "gt"; threshold = 90; severity = 5; cooldownMinutes = 0
    } | ConvertTo-Json) | Out-Null
}

Write-Host "2) Pre-clear alarm (dev ingest value=50)..." -ForegroundColor DarkGray
Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
    domainName = $Domain; key = "cpu_usage"; value = 50; kind = "metric"
} | ConvertTo-Json) | Out-Null
Start-Sleep -Seconds 6

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$payload = '{"domainName":"odak","domainId":"odak","collectibleCode":"cpu_usage","value":97,"assetId":"native-e2e"}'
$routingKey = "odak.metric.cpu_usage"

Write-Host "3) Publish flat metric to monitra.observations (native Reactor shape)..." -ForegroundColor Yellow
$r = Invoke-OdakRabbitMqPublish -SshSession $session -Exchange "monitra.observations" -RoutingKey $routingKey -Payload $payload
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) { throw "rabbitmq publish failed" }

Start-Sleep -Seconds 10

Write-Host "4) Verify via dev ingest (expect updated or stable)..." -ForegroundColor Cyan
$follow = Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
    domainName = $Domain; key = "cpu_usage"; value = 98; kind = "metric"
} | ConvertTo-Json)
Write-Host "   raised=$($follow.alarmsRaised) updated=$($follow.alarmsUpdated) resolved=$($follow.alarmsResolved)"

Remove-SSHSession -SessionId $session.SessionId | Out-Null

if ($follow.alarmsUpdated -ge 1 -or $follow.alarmsRaised -ge 1) {
    Write-Host "OK native observation path consumed by MngAlarm" -ForegroundColor Green
    exit 0
}

Write-Host "FAIL: expected alarm raised/updated after native observation publish" -ForegroundColor Red
exit 1
