# Alarm lifecycle → workflow E2E (raised / updated / resolved)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$wf = "$Gateway/workflow/api/v1"
$alarm = "$Gateway/alarm/api/v1"

function New-AlarmWorkflow([string]$suffix, [string]$eventType, [string]$logMsg) {
    $def = Invoke-RestMethod -Uri "$wf/definitions" -Method POST -Headers $hdr -Body (@{ key = "alarm-$suffix-$(Get-Random)"; name = "Alarm $suffix E2E" } | ConvertTo-Json)
    $body = @{
        entryNodeId = "manual_1"
        nodes = @(
            @{ id = "manual_1"; type = "manual.trigger"; config = @{} },
            @{ id = "log_1"; type = "write.log"; config = @{ message = $logMsg } }
        )
        edges = @(@{ fromNodeId = "manual_1"; toNodeId = "log_1"; edgeKey = "default" })
        triggers = @(@{ type = "event"; enabled = $true; config = @{ eventType = $eventType } })
    } | ConvertTo-Json -Depth 8
    $ver = Invoke-RestMethod -Uri "$wf/definitions/$($def.id)/versions" -Method POST -Headers $hdr -Body $body
    Invoke-RestMethod -Uri "$wf/versions/$($ver.id)/publish" -Method POST -Headers $hdr | Out-Null
    return @{ WorkflowId = $def.id; VersionId = $ver.id }
}

Write-Host "Creating alarm rule (cooldown=0)..." -ForegroundColor Cyan
$rules = Invoke-RestMethod -Uri "$alarm/rules" -Headers $hdr
$rule = $rules | Where-Object { $_.matchKey -eq "cpu_usage" -and $_.operator -eq "gt" } | Select-Object -First 1
if (-not $rule) {
    $rule = Invoke-RestMethod -Uri "$alarm/rules?domainName=$Domain" -Method POST -Headers $hdr -Body (@{
        name = "CPU lifecycle E2E"; matchKey = "cpu_usage"; operator = "gt"; threshold = 90; severity = 5; cooldownMinutes = 0
    } | ConvertTo-Json)
} else {
    Write-Host "Using existing rule $($rule.id)"
}

Write-Host "Creating workflows..." -ForegroundColor Cyan
$wfRaised = New-AlarmWorkflow "raised" "alarm.raised" "alarm raised e2e"
$wfUpdated = New-AlarmWorkflow "updated" "alarm.updated" "alarm updated e2e"
$wfResolved = New-AlarmWorkflow "resolved" "alarm.resolved" "alarm resolved e2e"

function Send-Obs([double]$value) {
    Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
        domainName = $Domain; key = "cpu_usage"; value = $value; kind = "metric"
    } | ConvertTo-Json)
}

Write-Host "`n0) Pre-clear active alarm (value=50)..." -ForegroundColor DarkGray
$pre = Send-Obs 50
Write-Host "   resolved=$($pre.alarmsResolved)"
Start-Sleep -Seconds 8

Write-Host "`n1) Raise (value=95)..." -ForegroundColor Yellow
$r1 = Send-Obs 95
Write-Host "   raised=$($r1.alarmsRaised) updated=$($r1.alarmsUpdated) resolved=$($r1.alarmsResolved)"
Start-Sleep -Seconds 12

Write-Host "2) Update (value=96)..." -ForegroundColor Yellow
$r2 = Send-Obs 96
Write-Host "   raised=$($r2.alarmsRaised) updated=$($r2.alarmsUpdated) resolved=$($r2.alarmsResolved)"
Start-Sleep -Seconds 12

Write-Host "3) Resolve (value=50)..." -ForegroundColor Yellow
$r3 = Send-Obs 50
Write-Host "   raised=$($r3.alarmsRaised) updated=$($r3.alarmsUpdated) resolved=$($r3.alarmsResolved)"
Start-Sleep -Seconds 12

function Check-Runs($wfId, $label) {
    $runs = Invoke-RestMethod -Uri "$wf/runs?workflowId=$wfId&limit=3" -Headers $hdr
    $count = @($runs).Count
    if ($count -eq 0) {
        Write-Host "  FAIL $label : no runs" -ForegroundColor Red
        return $false
    }
    $detail = Invoke-RestMethod -Uri "$wf/runs/$($runs[0].id)" -Headers $hdr
    $ok = $detail.instance.status -eq 2 -and ($detail.executions | Where-Object { $_.nodeId -eq "log_1" -and $_.status -eq 1 })
    if ($ok) {
        Write-Host "  OK $label : run $($runs[0].id) Completed" -ForegroundColor Green
    } else {
        Write-Host "  FAIL $label : status=$($detail.instance.status)" -ForegroundColor Red
    }
    return $ok
}

Write-Host "`nResults:" -ForegroundColor Cyan
$ok1 = Check-Runs $wfRaised.WorkflowId "alarm.raised"
$ok2 = Check-Runs $wfUpdated.WorkflowId "alarm.updated"
$ok3 = Check-Runs $wfResolved.WorkflowId "alarm.resolved"

if ($ok1 -and $ok2 -and $ok3) {
    Write-Host "`nAll alarm lifecycle E2E passed." -ForegroundColor Green
    exit 0
}
Write-Host "`nSome checks failed." -ForegroundColor Red
exit 1
