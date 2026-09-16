# TEST-only: SEED-PMO obligation demo records. Do not run against prod.
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$ProjectId = "027bdf17-6741-4001-835f-9c1412c42f21"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = $PSScriptRoot
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"))) {
    $parent = Split-Path $repoRoot -Parent
    if (-not $parent -or $parent -eq $repoRoot) { break }
    $repoRoot = $parent
}
if (-not (Test-Path (Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"))) {
    $repoRoot = "C:\Users\monitra\Dev\MonitraNG\MonitraNG"
}
$load = Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"

$token = (& $load -AutoRefresh).ToString().Trim()
$headers = @{ Authorization = "Bearer $token"; Accept = "application/json" }
$ops = "$Gateway/operations/api/v1"
$doc = "$Gateway/documents/api/v1/resources"

function Invoke-Json {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        $Body = $null,
        [int[]]$Expect = @(200, 201, 204)
    )
    $status = 0
    $p = @{
        Uri                  = $Uri
        Method               = $Method
        Headers              = $headers
        TimeoutSec           = 90
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $p.ContentType = "application/json; charset=utf-8"
        $p.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
    }
    $result = Invoke-RestMethod @p
    if ($Expect -notcontains [int]$status) {
        $err = $null
        try { $err = $result | ConvertTo-Json -Compress -Depth 8 } catch { $err = [string]$result }
        throw "HTTP $status $Method $Uri : $err"
    }
    return $result
}

function Find-Wbs($items, [string]$code) {
    $hit = @($items) | Where-Object { $_.wbsCode -eq $code } | Select-Object -First 1
    if (-not $hit) { throw "WBS $code bulunamadi." }
    return $hit
}

function Ensure-Markdown([string]$parentId, [string]$title, [string]$content) {
    $kids = Invoke-Json "$doc/children?parentId=$([uri]::EscapeDataString($parentId))&limit=100"
    $found = @($kids.items) | Where-Object {
        $_.type -eq "markdown" -and (($_.title -eq $title) -or ($_.name -eq $title))
    } | Select-Object -First 1
    if ($found) { return $found.id }
    $created = Invoke-Json "$doc/markdown" "POST" @{
        parentId = $parentId
        title    = $title
        content  = $content
        isDraft  = $false
    }
    return $created.id
}

Write-Host "SEED-PMO obligation seed  project=$ProjectId" -ForegroundColor Cyan

$detail = Invoke-Json "$ops/projects/$ProjectId"
$wbs = @($detail.wbs)
if ($wbs.Count -eq 0) { throw "WBS bos." }
$project = $detail.project
if (-not $project) { $project = $detail }

$wbs11 = Find-Wbs $wbs "1.1"
$wbs21 = Find-Wbs $wbs "2.1"
$wbs32 = Find-Wbs $wbs "3.2"

Write-Host ("WBS 1.1={0}  2.1={1}  3.2={2}" -f $wbs11.id, $wbs21.id, $wbs32.id)

$workRaw = Invoke-Json "$ops/projects/$ProjectId/work-items"
$workItems = @($workRaw)
if ($workItems.Count -eq 1 -and $null -ne $workRaw.items) { $workItems = @($workRaw.items) }
Write-Host "Work items: $($workItems.Count)"

function Pick-WorkByTitle([string]$title) {
    $hit = @($workItems) | Where-Object { [string]$_.title -eq $title } | Select-Object -First 1
    if ($hit) { return [string]$hit.id }
    return $null
}

function Pick-WorkByKey([string]$key) {
    $hit = @($workItems) | Where-Object { [string]$_.key -eq $key } | Select-Object -First 1
    if ($hit) { return [string]$hit.id }
    return $null
}

$wi11 = Pick-WorkByKey "SEEDPMO-0002"
if (-not $wi11) { $wi11 = Pick-WorkByTitle "Kick-off" }
$wi21 = Pick-WorkByKey "SEEDPMO-0005"
if (-not $wi21) { $wi21 = Pick-WorkByTitle "Proje plani" }
$wi32 = Pick-WorkByKey "SEEDPMO-0009"
if (-not $wi32) { $wi32 = Pick-WorkByTitle "Teslimatlar" }
Write-Host "WI 1.1=$wi11  2.1=$wi21  3.2=$wi32"

$hubId = [string]$project.diFolderId
if (-not $hubId) { throw "Proje diFolderId yok." }
$hubKids = Invoke-Json "$doc/children?parentId=$([uri]::EscapeDataString($hubId))&limit=100"
$wiki = @($hubKids.items) | Where-Object { $_.type -eq "folder" -and $_.name -eq "Wiki" } | Select-Object -First 1
if (-not $wiki) { throw "Wiki klasoru yok (hub=$hubId)." }
$wikiId = [string]$wiki.id
Write-Host "Wiki=$wikiId"

$sourceId = Ensure-Markdown $wikiId "Sartname ozeti (DEMO)" @"
# Sartname ozeti (DEMO)

Teslimat omurgasi yukumluluk ornekleri icin kaynak belge.

- SRT-12.3 Yangin butonu 30 m'de bir
- SRT-4.2 Yedekleme proseduru
- SRT-8.1 Devreye alma egitimi
- SRT-3.4 Kablo etiketleme
"@

$evidenceId = Ensure-Markdown $wikiId "Yangin butonu tutanak (DEMO)" @"
# Yangin butonu tutanak (DEMO)

Saha kontrolu tamamlandi. Buton araligi 30 m kuralina uygun.
"@

$safetyId = Ensure-Markdown $wikiId "Guvenlik talimati (DEMO)" @"
# Guvenlik talimati (DEMO)

Okundu ve yukumluluk ornekleri icin ortak belge.
"@

Write-Host "Docs source=$sourceId evidence=$evidenceId safety=$safetyId"

$pack = Invoke-Json "$ops/projects/$ProjectId/obligations"
$existing = @($pack.items)
$existingKeys = @{}
foreach ($row in $existing) {
    $key = ("{0}|{1}" -f ([string]$row.clauseRef).Trim().ToLowerInvariant(), ([string]$row.title).Trim().ToLowerInvariant())
    $existingKeys[$key] = $true
}

$seeds = @(
    @{
        title              = "Yangin butonu 30 m'de bir (DEMO)"
        clauseRef          = "SRT-12.3"
        sourceResourceId   = $sourceId
        evidenceResourceId = $evidenceId
        wbsId              = $wbs32.id
        workItemId         = $wi32
        status             = "satisfied"
        dueDate            = "2026-08-15T00:00:00.000Z"
        note               = $null
    }
    @{
        title              = "Yedekleme proseduru yazilacak (DEMO)"
        clauseRef          = "SRT-4.2"
        sourceResourceId   = $sourceId
        evidenceResourceId = $null
        wbsId              = $wbs21.id
        workItemId         = $wi21
        status             = "open"
        dueDate            = "2020-01-15T00:00:00.000Z"
        note               = $null
    }
    @{
        title              = "Devreye alma egitimi verilecek (DEMO)"
        clauseRef          = "SRT-8.1"
        sourceResourceId   = $sourceId
        evidenceResourceId = $null
        wbsId              = $null
        workItemId         = $null
        status             = "open"
        dueDate            = "2026-12-31T00:00:00.000Z"
        note               = $null
    }
    @{
        title              = "Kablo etiketleme tamamlanacak (DEMO)"
        clauseRef          = "SRT-3.4"
        sourceResourceId   = $sourceId
        evidenceResourceId = $null
        wbsId              = $wbs32.id
        workItemId         = $wi32
        status             = "inProgress"
        dueDate            = "2026-10-15T00:00:00.000Z"
        note               = $null
    }
    @{
        title              = "ISO kalite el kitabi yururlukte (DEMO)"
        clauseRef          = "SRT-1.0"
        sourceResourceId   = $null
        evidenceResourceId = $null
        wbsId              = $null
        workItemId         = $null
        status             = "waived"
        dueDate            = $null
        note               = "Kapsam disi: musteri ISO belgesi kendi sisteminde."
    }
    @{
        title              = "Is guvenligi brifingi (DEMO)"
        clauseRef          = "SRT-9.1"
        sourceResourceId   = $safetyId
        evidenceResourceId = $null
        wbsId              = $wbs11.id
        workItemId         = $wi11
        status             = "open"
        dueDate            = "2026-11-30T00:00:00.000Z"
        note               = $null
    }
)

$created = @()
foreach ($seed in $seeds) {
    $key = ("{0}|{1}" -f $seed.clauseRef.ToLowerInvariant(), $seed.title.ToLowerInvariant())
    if ($existingKeys.ContainsKey($key)) {
        Write-Host "SKIP $($seed.clauseRef) $($seed.title)" -ForegroundColor Yellow
        continue
    }
    $body = @{
        title              = $seed.title
        clauseRef          = $seed.clauseRef
        sourceResourceId   = $seed.sourceResourceId
        evidenceResourceId = $seed.evidenceResourceId
        wbsId              = $seed.wbsId
        workItemId         = $seed.workItemId
        status             = $seed.status
        dueDate            = $seed.dueDate
        note               = $seed.note
    }
    $row = Invoke-Json "$ops/projects/$ProjectId/obligations" "POST" $body
    $created += $row
    Write-Host ("CREATE {0} {1} status={2} open={3} overdue={4} unbound={5}" -f $row.clauseRef, $row.title, $row.status, $row.open, $row.overdue, $row.unbound)
}

$after = Invoke-Json "$ops/projects/$ProjectId/obligations"
$status = Invoke-Json "$ops/projects/$ProjectId/status"

Write-Host ""
Write-Host ("obligations={0} open={1} overdue={2} unbound={3}" -f @($after.items).Count, $after.openCount, $after.overdueCount, $after.unboundCount)
Write-Host ("status counts openObligation={0} overdueObligation={1} unboundObligation={2}" -f $status.counts.openObligation, $status.counts.overdueObligation, $status.counts.unboundObligation)

$flagWbs = @($status.items) | Where-Object {
    @($_.flags) -contains "openObligation" -or
    @($_.flags) -contains "overdueObligation" -or
    @($_.flags) -contains "unboundObligation"
}
foreach ($row in $flagWbs) {
    Write-Host ("WBS {0} {1} flags={2}" -f $row.wbsCode, $row.name, (@($row.flags) -join ","))
}

Write-Host "SEED obligation OK" -ForegroundColor Green
