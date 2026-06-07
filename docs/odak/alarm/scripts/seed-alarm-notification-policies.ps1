# Odak — alarm bildirim politikasi seed (idempotent)
# On kosul: mngalarm deploy, odak_admin token
# Mail sablonlari: setup-notifier-datasets.ps1 (alarm-raised / alarm-resolved)
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\notifications\scripts\setup-notifier-datasets.ps1
#   .\docs\odak\alarm\scripts\seed-alarm-notification-policies.ps1

param(
    [string]$GatewayUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$SeedFile = "",
    [switch]$SkipMailTemplates,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"
$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"

if ([string]::IsNullOrWhiteSpace($SeedFile)) {
    $SeedFile = Join-Path (Split-Path $scriptDir -Parent) "datasets/alarm_notification_policies_seed.json"
}
if (-not (Test-Path $SeedFile)) {
    throw "Seed dosyasi bulunamadi: $SeedFile"
}

$seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$alarmBase = "$($GatewayUrl.TrimEnd('/'))/alarm/api/v1"

if (-not $SkipMailTemplates) {
    $notifierScript = Join-Path $repoRoot "docs/odak/notifications/scripts/setup-notifier-datasets.ps1"
    if (Test-Path $notifierScript) {
        Write-Host "=== Mail sablonlari (alarm-raised / alarm-resolved) ===" -ForegroundColor Cyan
        & $notifierScript -BaseUrl $GatewayUrl
    }
}

$token = & $loadTokenScript
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Token alinamadi."
}

$headers = @{
    Authorization   = "Bearer $token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}
$alarmParams = @{ Headers = $headers; ErrorAction = "Stop" }

function Get-PolicySeedMarker([string]$SeedKey) {
    return "[seed:$SeedKey]"
}

function Get-Prop($obj, [string[]]$Names, [string]$Default = "") {
    foreach ($n in $Names) {
        if ($null -ne $obj.$n -and -not [string]::IsNullOrWhiteSpace([string]$obj.$n)) {
            return [string]$obj.$n
        }
    }
    return $Default
}

function Test-PolicySeeded($existingPolicies, [string]$SeedKey, [string]$Name) {
    $marker = Get-PolicySeedMarker $SeedKey
    foreach ($p in $existingPolicies) {
        $desc = Get-Prop $p @("description", "Description")
        $pname = Get-Prop $p @("name", "Name")
        if ($desc.Contains($marker) -or $pname -eq $Name) { return $true }
    }
    return $false
}

Write-Host "=== Alarm notification policy seed ===" -ForegroundColor Cyan

function Normalize-AlarmApiList($raw) {
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return ,@($raw) }
    if ($null -ne $raw.items) { return ,@($raw.items) }
    if ($null -ne $raw.Items) { return ,@($raw.Items) }
    return ,@($raw)
}

$rules = Normalize-AlarmApiList (Invoke-RestMethod -Uri "$alarmBase/rules" -Method GET @alarmParams)
$existing = Normalize-AlarmApiList (Invoke-RestMethod -Uri "$alarmBase/notification-policies" -Method GET @alarmParams)
Write-Host "Kurallar: $($rules.Count) | Mevcut politikalar: $($existing.Count)" -ForegroundColor Gray

$recipientId = [string]$seed.recipientPersonId
if ([string]::IsNullOrWhiteSpace($recipientId)) {
    throw "seed.recipientPersonId zorunlu"
}

$ruleByMatchKey = @{}
foreach ($r in $rules) {
    $mk = (Get-Prop $r @("matchKey", "MatchKey")).Trim()
    $rid = Get-Prop $r @("id", "Id")
    if ($mk -and $rid) { $ruleByMatchKey[$mk] = $rid }
}

$result = @{
    domain             = $Domain
    gatewayUrl         = $GatewayUrl
    recipientPersonId  = $recipientId
    recipientUsername  = [string]$seed.recipientUsername
    seededAt           = (Get-Date).ToUniversalTime().ToString("o")
    policies           = @{}
}

foreach ($row in $seed.policies) {
    $seedKey = [string]$row.seedKey
    $name = [string]$row.name
    if ([string]::IsNullOrWhiteSpace($seedKey) -or [string]::IsNullOrWhiteSpace($name)) {
        Write-Host "SKIP: seedKey veya name eksik" -ForegroundColor Yellow
        continue
    }

    if (Test-PolicySeeded $existing $seedKey $name) {
        Write-Host "SKIP: $name (zaten seed edilmis)" -ForegroundColor Green
        $hit = $existing | Where-Object {
            $n = Get-Prop $_ @("name", "Name")
            $d = Get-Prop $_ @("description", "Description")
            $n -eq $name -or $d.Contains((Get-PolicySeedMarker $seedKey))
        } | Select-Object -First 1
        if ($hit) {
            $result.policies[$seedKey] = Get-Prop $hit @("id", "Id")
        }
        continue
    }

    $ruleId = $null
    $ruleMatchKey = $row.ruleMatchKey
    if ($null -ne $ruleMatchKey -and -not [string]::IsNullOrWhiteSpace([string]$ruleMatchKey)) {
        $mk = [string]$ruleMatchKey
        if ($ruleByMatchKey.ContainsKey($mk)) {
            $ruleId = $ruleByMatchKey[$mk]
        }
        else {
            Write-Host "UYARI: ruleMatchKey '$mk' bulunamadi ($name) — ruleId bos kalacak" -ForegroundColor Yellow
        }
    }

    $desc = [string]$row.description
    $marker = Get-PolicySeedMarker $seedKey
    if (-not $desc.Contains($marker)) {
        $desc = if ($desc) { "$desc $marker" } else { $marker }
    }

    $body = @{
        name               = $name
        description        = $desc
        eventType          = [string]$row.eventType
        ruleId             = $ruleId
        minSeverity        = $row.minSeverity
        maxSeverity        = $row.maxSeverity
        channels           = @($row.channels)
        recipientPersonIds = @($recipientId)
        emailTemplateKey   = $row.emailTemplateKey
        emailSubject       = $row.emailSubject
        settings           = $row.settings
        cooldownMinutes    = $row.cooldownMinutes
        priority           = $row.priority
        isActive           = if ($null -eq $row.isActive) { $true } else { [bool]$row.isActive }
    }

    if ($WhatIf) {
        Write-Host "WhatIf POST $name -> $($body | ConvertTo-Json -Compress)" -ForegroundColor Yellow
        continue
    }

    $json = $body | ConvertTo-Json -Compress -Depth 6
    $created = Invoke-RestMethod -Uri "$alarmBase/notification-policies" -Method POST -Body $json @alarmParams
    $policyId = Get-Prop $created @("id", "Id")
    $result.policies[$seedKey] = $policyId
    $ruleInfo = if ($ruleId) { "ruleId=$ruleId" } else { "tum kurallar" }
    Write-Host "OK $name -> id=$policyId ($ruleInfo)" -ForegroundColor Green
}

$resultFile = Join-Path $scriptDir "alarm_notification_policies_seed_result.json"
if (-not $WhatIf) {
    $result | ConvertTo-Json -Depth 4 | Set-Content -Path $resultFile -Encoding UTF8
    Write-Host "`nSonuc: $resultFile" -ForegroundColor Cyan
}

Write-Host "UI: $GatewayUrl -> /apps/alarm-center/notification-policies" -ForegroundColor Cyan
