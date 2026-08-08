# Stop ALL alarm production for a domain: disable every alarm rule and published scenario.
# Flows stay published but stopped; operators start them one-by-one after approval.
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$null = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$tokenFile = Join-Path $env:TEMP "serkan_token.txt"
if (-not (Test-Path $tokenFile)) { throw "Token dosyasi yok: $tokenFile" }
$token = (Get-Content -Path $tokenFile -Raw).Trim()
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }

$rulesApi = "$Gateway/alarm/api/v1/rules"
$scenariosApi = "$Gateway/alarm/api/v1/scenarios"

function Get-Array {
    param($Raw)
    if ($null -eq $Raw) { return @() }
    if ($Raw -is [System.Array]) { return @($Raw) }
    return @($Raw)
}

Write-Host "=== Stop all alarm production ($Domain) ===" -ForegroundColor Cyan
if (-not $Apply) { Write-Host "   Dry-run (-Apply ile uygula)" -ForegroundColor Yellow }

$rules = Get-Array (Invoke-RestMethod -Uri $rulesApi -Headers $hdr)
$enabledRules = @($rules | Where-Object { $_.enabled -eq $true })
Write-Host "   Rules total=$($rules.Count) enabled=$($enabledRules.Count)" -ForegroundColor DarkGray

$disabledRules = 0
foreach ($rule in $enabledRules) {
    if ($Apply) {
        Invoke-RestMethod -Uri "$rulesApi/$($rule.id)" -Method PUT -Headers $hdr -Body (@{
            enabled = $false
        } | ConvertTo-Json) | Out-Null
        Write-Host "   RULE OFF $($rule.name) ($($rule.id))" -ForegroundColor Green
    } else {
        Write-Host "   WOULD RULE OFF $($rule.name) ($($rule.id))" -ForegroundColor DarkGray
    }
    $disabledRules++
}

$disabledFlows = 0
$flowApiMissing = $false
try {
    $scenarios = Get-Array (Invoke-RestMethod -Uri "$scenariosApi`?includeDrafts=true" -Headers $hdr)
    $published = @($scenarios | Where-Object { $null -ne $_.publishedVersion })
    Write-Host "   Scenarios total=$($scenarios.Count) withPublished=$($published.Count)" -ForegroundColor DarkGray

    foreach ($item in $published) {
        $ver = [int]$item.publishedVersion
        $alreadyOff = ($item.enabled -eq $false) -or ($item.operationalStatus -eq "stopped")
        if ($alreadyOff) {
            Write-Host "   FLOW already stopped $($item.name) v$ver" -ForegroundColor DarkGray
            continue
        }
        if ($Apply) {
            try {
                Invoke-RestMethod -Uri "$scenariosApi/$($item.scenarioId)/versions/$ver/enabled" `
                    -Method POST -Headers $hdr -Body (@{ enabled = $false } | ConvertTo-Json) | Out-Null
                Write-Host "   FLOW OFF $($item.name) v$ver" -ForegroundColor Green
                $disabledFlows++
            } catch {
                $flowApiMissing = $true
                Write-Host "   FLOW API skip $($item.name): $($_.Exception.Message)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   WOULD FLOW OFF $($item.name) v$ver" -ForegroundColor DarkGray
            $disabledFlows++
        }
    }
} catch {
    $flowApiMissing = $true
    Write-Host "   Scenarios API unavailable: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "   Rule disable still stops production (legacy + projected rules)." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Summary: rulesDisabled=$disabledRules flowsStopped=$disabledFlows" -ForegroundColor Cyan
if ($flowApiMissing) {
    Write-Host "Note: scenario APIs may not be deployed yet; rule disable still stops production." -ForegroundColor Yellow
}
if ($Apply) {
    Write-Host "OK alarm production stopped. Start flows one-by-one after approval." -ForegroundColor Green
} else {
    Write-Host "OK dry-run (-Apply ile uygula)" -ForegroundColor Green
}
exit 0
