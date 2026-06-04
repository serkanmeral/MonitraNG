# SIEM B3 — hazır alarm kural paketi (`siem-mvp-v1`) seed
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$PackageId = "siem-mvp-v1",
    [switch]$Replace,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$rulesApi = "$Gateway/alarm/api/v1/rules"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$packDir = Join-Path $repoRoot "tests/fixtures/siem/alarm_rules/packages/$PackageId"
$rulesDir = Join-Path $repoRoot "tests/fixtures/siem/alarm_rules"
$manifestPath = Join-Path $packDir "manifest.json"

if (-not (Test-Path $manifestPath)) { throw "Manifest bulunamadi: $manifestPath" }

Write-Host "=== SIEM B3 seed alarm rule pack: $PackageId ===" -ForegroundColor Cyan
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.packageId -ne $PackageId) {
    throw "Manifest packageId=$($manifest.packageId) beklenen=$PackageId"
}

$existing = @(Invoke-RestMethod -Uri $rulesApi -Headers $hdr)
$created = 0
$skipped = 0
$replaced = 0

foreach ($entry in @($manifest.rules)) {
    $scenarioId = $entry.scenarioId
    $sourcePath = Join-Path $rulesDir $entry.sourceFile
    if (-not (Test-Path $sourcePath)) { throw "Kural dosyasi eksik: $sourcePath" }

    $ruleBody = Get-Content $sourcePath -Raw | ConvertFrom-Json
    $meta = $entry.metadata
    $ruleBody | Add-Member -NotePropertyName metadata -NotePropertyValue ([ordered]@{
        packageId           = $manifest.packageId
        packageVersion      = $manifest.packageVersion
        scenarioId          = $scenarioId
        description         = $meta.description
        threatTacticId      = $meta.threatTacticId
        threatTacticName    = $meta.threatTacticName
        threatTechniqueId   = $meta.threatTechniqueId
        threatTechniqueName = $meta.threatTechniqueName
        complianceTags      = @($meta.complianceTags)
    }) -Force

    $match = @($existing) | Where-Object {
        $_.metadata -and
        $_.metadata.packageId -eq $manifest.packageId -and
        $_.metadata.scenarioId -eq $scenarioId
    } | Select-Object -First 1

    if ($match -and -not $Replace) {
        Write-Host "   SKIP $scenarioId (ruleId=$($match.id))" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    $payload = $ruleBody | ConvertTo-Json -Depth 10 -Compress:$false
    if ($DryRun) {
        Write-Host "   DRY-RUN $scenarioId -> $($ruleBody.name)" -ForegroundColor Yellow
        continue
    }

    if ($match -and $Replace) {
        Invoke-RestMethod -Uri "$rulesApi/$($match.id)" -Method DELETE -Headers $hdr | Out-Null
        $replaced++
    }

    $createdRule = Invoke-RestMethod -Uri $rulesApi -Method POST -Headers $hdr -Body $payload
    Write-Host "   OK $scenarioId ruleId=$($createdRule.id) technique=$($meta.threatTechniqueId)" -ForegroundColor Green
    $created++
    $existing = @(Invoke-RestMethod -Uri $rulesApi -Headers $hdr)
}

Write-Host "`nPaket: $($manifest.name) v$($manifest.packageVersion)" -ForegroundColor Cyan
Write-Host "   created=$created skipped=$skipped replaced=$replaced" -ForegroundColor Green
if ($DryRun) { Write-Host "   (dry-run — degisiklik yok)" -ForegroundColor Yellow }
Write-Host "`nOK SIEM B3 rule pack seed tamam" -ForegroundColor Green
exit 0
