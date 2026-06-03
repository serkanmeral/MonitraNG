# C6 gecis durumu — bridge + reactor health (Odak SSH)
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$Gateway = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$cmd = @'
echo "=== mngreactor image ==="
docker inspect mngreactor --format "{{.Config.Image}}" 2>/dev/null || echo missing
echo "=== mngalarm-worker bridge env ==="
docker exec mngalarm-worker printenv 2>/dev/null | grep -i ReactorBridge || echo no_worker
echo "=== reactor gateway probe ==="
curl -s -o /dev/null -w "reactor_live=%{http_code}\n" http://127.0.0.1:5040/reactor/api/v1/health/live || true
'@

$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd -TimeOut 30
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null

Write-Host "`nC6 tamamlanmak icin:" -ForegroundColor Cyan
Write-Host "  1) MngReactor repo: R1-R3 (REACTOR_NATIVE_PUBLISH_HANDOFF.md)" -ForegroundColor Gray
Write-Host "  2) Odak: gercek mngreactor image + ObservationPublish__Enabled=true" -ForegroundColor Gray
Write-Host "  3) mngalarm-worker: ReactorBridge__Enabled=false" -ForegroundColor Gray
Write-Host "  4) .\scripts\odak\test-reactor-observation-e2e.ps1 -FailIfSkipped" -ForegroundColor Gray
