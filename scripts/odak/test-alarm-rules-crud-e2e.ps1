# Alarm rule CRUD API smoke (GET/POST/PUT/DELETE)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$base = "$Gateway/alarm/api/v1/rules"

Write-Host "1) POST rule..." -ForegroundColor Cyan
$created = Invoke-RestMethod -Uri $base -Method POST -Headers $hdr -Body (@{
    name = "crud-e2e-$(Get-Random)"
    type = "threshold"
    severity = 5
    matchKey = "test.crud.e2e"
    operator = "gt"
    threshold = 10
    cooldownMinutes = 5
    windowMinutes = 5
    stalenessMinutes = 30
} | ConvertTo-Json)
if (-not $created.id) { throw "Create did not return id" }

Write-Host "2) GET rule $($created.id)..." -ForegroundColor Yellow
$got = Invoke-RestMethod -Uri "$base/$($created.id)" -Headers $hdr
if ($got.name -ne $created.name) { throw "GET name mismatch" }

Write-Host "3) PUT rule..." -ForegroundColor Yellow
$updated = Invoke-RestMethod -Uri "$base/$($created.id)" -Method PUT -Headers $hdr -Body (@{
    name = "$($created.name)-updated"
    threshold = 20
    enabled = $false
} | ConvertTo-Json)
if ($updated.threshold -ne 20 -or $updated.enabled -ne $false) { throw "PUT did not apply" }

Write-Host "4) DELETE rule..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "$base/$($created.id)" -Method DELETE -Headers $hdr | Out-Null
try {
    Invoke-RestMethod -Uri "$base/$($created.id)" -Headers $hdr
    throw "Rule still exists after delete"
} catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
}

Write-Host "OK alarm rules CRUD E2E" -ForegroundColor Green
exit 0
