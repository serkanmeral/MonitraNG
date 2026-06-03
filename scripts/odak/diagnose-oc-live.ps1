# Diagnose oc_live 502 via UI nginx -> mngoperations
param([string]$Server = "192.168.20.20", [string]$User = "odak")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$cred = Get-OdakSshCredential -User $User -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

$cmd = @'
echo "=== containers ==="
docker ps --format "table {{.Names}}\t{{.Status}}" | grep -E "mngui|mngoperations|NAMES" || true
echo "=== mo direct :5086 ==="
curl -s -o /dev/null -w "mo_direct=%{http_code}\n" http://127.0.0.1:5086/api/v1/health/live || echo mo_direct_fail
echo "=== oc_live via :3000 ==="
curl -s -w "\noc_live=%{http_code}\n" http://127.0.0.1:3000/api/operations/v1/health/live | head -c 300
echo ""
echo "=== mngui logs tail ==="
docker logs mngui 2>&1 | tail -8
echo "=== mngoperations logs tail ==="
docker logs mngoperations 2>&1 | tail -8
'@

$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd -TimeOut 45
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Red } }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
