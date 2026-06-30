param(
    [string]$Server = "192.168.20.20",
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$DomainId = "odak",
    [string]$Database = "mng_odak"
)
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$getTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/get-operationcore-token.ps1"
if (-not (Test-Path $getTokenScript)) {
    throw "Token script not found: $getTokenScript"
}

Write-Host "=== 1) Token ===" -ForegroundColor Cyan
& $getTokenScript
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Token alinamadi" }
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
$token = (Get-Content $tokenFile -Raw).Trim()
if (-not $token) { throw "Token dosyasi bos" }

function Get-ScopeSnapshot {
    param([string]$Label)
    Import-Module Posh-SSH -Force
    . (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
    Initialize-OdakSshEnvironment -Server $Server
    $cred = Get-OdakSshCredential -Server $Server
    $js = @'
const d=db.getSiblingDB('__DB__');
function snap(col) {
  const c=d.getCollection(col);
  return {
    total: c.countDocuments(),
    visible: c.countDocuments({includeInApplication:true}),
    hidden: c.countDocuments({includeInApplication:false}),
    missing: c.countDocuments({includeInApplication:{$exists:false}}),
  };
}
const users=snap('@users');
const groups=snap('@groups');
const sampleVisibleUsers=d.getCollection('@users').find({includeInApplication:true},{username:1}).limit(5).toArray().map(u=>u.username);
const sampleHiddenUsers=d.getCollection('@users').find({includeInApplication:false},{username:1}).limit(5).toArray().map(u=>u.username);
print(JSON.stringify({label:'__LABEL__',users,groups,sampleVisibleUsers,sampleHiddenUsers}));
'@ -replace '__DB__', $Database -replace '__LABEL__', $Label
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($js))
    $cmd = "echo $b64 | base64 -d | docker exec -i mongo mongosh -u admin -p admin123 --authenticationDatabase admin --quiet"
    $s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
    try {
        $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 120
        $line = ($r.Output | ForEach-Object { if ($_ -match '\{.*\}') { $Matches[0] } }) | Select-Object -First 1
        if (-not $line) { throw "Mongo snapshot bos ($Label)" }
        return $line | ConvertFrom-Json
    }
    finally {
        Remove-SSHSession -SessionId $s.SessionId | Out-Null
    }
}

Write-Host "`n=== 2) Sync ONCESI Mongo ===" -ForegroundColor Cyan
$before = Get-ScopeSnapshot -Label "before"

Write-Host "Users:  visible=$($before.users.visible) hidden=$($before.users.hidden) missing=$($before.users.missing) total=$($before.users.total)"
Write-Host "Groups: visible=$($before.groups.visible) hidden=$($before.groups.hidden) missing=$($before.groups.missing) total=$($before.groups.total)"

Write-Host "`n=== 3) POST /keeper/api/directory/sync ===" -ForegroundColor Cyan
$syncUrl = "$GatewayBaseUrl/keeper/api/directory/sync"
$body = @{ domainId = $DomainId; triggeredBy = 0 } | ConvertTo-Json
try {
    $syncResp = Invoke-RestMethod -Uri $syncUrl -Method POST -Body $body -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" } -TimeoutSec 600
}
catch {
    if ($_.ErrorDetails.Message) {
        throw "Sync failed: $($_.ErrorDetails.Message)"
    }
    throw $_
}

Write-Host "Sync sonuc: code=$($syncResp.code) success=$($syncResp.isSuccess)"
Write-Host "  users +$($syncResp.usersCreated)/~$($syncResp.usersUpdated) skip=$($syncResp.usersSkipped) deactivated=$($syncResp.usersDeactivated)"
Write-Host "  groups +$($syncResp.groupsCreated)/~$($syncResp.groupsUpdated) ms=$($syncResp.durationMs)"

Write-Host "`n=== 4) Sync SONRASI Mongo ===" -ForegroundColor Cyan
$after = Get-ScopeSnapshot -Label "after"

Write-Host "Users:  visible=$($after.users.visible) hidden=$($after.users.hidden) missing=$($after.users.missing) total=$($after.users.total)"
Write-Host "Groups: visible=$($after.groups.visible) hidden=$($after.groups.hidden) missing=$($after.groups.missing) total=$($after.groups.total)"

$usersHiddenDelta = $after.users.hidden - $before.users.hidden
$usersCreated = [int]($syncResp.usersCreated ?? 0)
$usersScopeOk = ($before.users.visible -eq $after.users.visible) -and (
    ($before.users.hidden -eq $after.users.hidden) -or
    ($usersHiddenDelta -gt 0 -and $usersHiddenDelta -le $usersCreated)
)
$groupsScopeOk = ($before.groups.visible -eq $after.groups.visible) -and
                 ($before.groups.hidden -eq $after.groups.hidden)
$ok = $usersScopeOk -and $groupsScopeOk

Write-Host "`n=== 5) DEGERLENDIRME ===" -ForegroundColor $(if ($ok) { "Green" } else { "Red" })
if ($ok) {
    if ($usersHiddenDelta -gt 0 -and $usersCreated -gt 0) {
        Write-Host "Gorunur kullanici sayisi korundu; $usersCreated yeni directory kullanicisi varsayilan sakli eklendi." -ForegroundColor Green
    }
    else {
        Write-Host "includeInApplication dagilimi sync sonrasi AYNI — saklama mekanigi korunmus." -ForegroundColor Green
    }
}
else {
    Write-Host "UYARI: includeInApplication sayilari degisti!" -ForegroundColor Red
    Write-Host "Before users:  V=$($before.users.visible) H=$($before.users.hidden)"
    Write-Host "After users:   V=$($after.users.visible) H=$($after.users.hidden)"
    Write-Host "Before groups: V=$($before.groups.visible) H=$($before.groups.hidden)"
    Write-Host "After groups:  V=$($after.groups.visible) H=$($after.groups.hidden)"
}

Write-Host "`nOrnek gorunur kullanicilar (once/sonra):"
Write-Host "  once:  $($before.sampleVisibleUsers -join ', ')"
Write-Host "  sonra: $($after.sampleVisibleUsers -join ', ')"

if (-not $syncResp.isSuccess) {
    Write-Host "Sync basarisiz (code=$($syncResp.code)) — grup asamasi tamamlanmis olabilir; asagida Mongo karsilastirmasi yine yapilir." -ForegroundColor Yellow
}

if (-not $ok) { exit 2 }
if (-not $syncResp.isSuccess) { exit 1 }
exit 0
