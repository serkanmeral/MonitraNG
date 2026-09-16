# TEST-only: SEED-PMO audit pack demo records. Do not run against prod.
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

Write-Host "SEED-PMO audit pack seed  project=$ProjectId" -ForegroundColor Cyan

$detail = Invoke-Json "$ops/projects/$ProjectId"
$wbs = @($detail.wbs)
$project = $detail.project
if (-not $project) { $project = $detail }
$wbs11 = Find-Wbs $wbs "1.1"
$wbs32 = Find-Wbs $wbs "3.2"
$hubId = [string]$project.diFolderId
if (-not $hubId) { throw "Proje diFolderId yok." }

$hubKids = Invoke-Json "$doc/children?parentId=$([uri]::EscapeDataString($hubId))&limit=100"
$wiki = @($hubKids.items) | Where-Object { $_.type -eq "folder" -and $_.name -eq "Wiki" } | Select-Object -First 1
if (-not $wiki) { throw "Wiki klasoru yok." }
$wikiId = [string]$wiki.id

$sourceId = Ensure-Markdown $wikiId "Sartname ozeti (DEMO)" "# Sartname ozeti (DEMO)`nDenetim paketi kaynagi."
$evidenceId = Ensure-Markdown $wikiId "Yangin butonu tutanak (DEMO)" "# Yangin butonu tutanak (DEMO)`nSaha kontrolu tamamlandi."
$safetyId = Ensure-Markdown $wikiId "Guvenlik talimati (DEMO)" "# Guvenlik talimati (DEMO)`nKick-off brifingi."
Write-Host "Docs source=$sourceId evidence=$evidenceId safety=$safetyId"

$pack = Invoke-Json "$ops/projects/$ProjectId/audit-packs"
$existing = @($pack.items)
$existingNames = @{}
foreach ($row in $existing) {
    $existingNames[([string]$row.name).Trim().ToLowerInvariant()] = $true
}

$seeds = @(
    @{
        name        = "ISO saha denetimi Ekim (DEMO)"
        kind        = "audit"
        wbsId       = $wbs32.id
        status      = "issued"
        dueDate     = "2026-08-31T00:00:00.000Z"
        resourceIds = @($sourceId, $evidenceId, $safetyId)
        recipient   = "Dis denetci (DEMO)"
        note        = $null
    }
    @{
        name        = "Musteri teslim dosyasi (DEMO)"
        kind        = "customer"
        wbsId       = $wbs11.id
        status      = "assembled"
        dueDate     = "2026-12-15T00:00:00.000Z"
        resourceIds = @($sourceId, $safetyId)
        recipient   = "Musteri saha muduru"
        note        = $null
    }
    @{
        name        = "Ic kalite gozden gecirme (DEMO)"
        kind        = "internal"
        wbsId       = $null
        status      = "draft"
        dueDate     = "2020-01-15T00:00:00.000Z"
        resourceIds = @()
        recipient   = "Kalite muduru"
        note        = $null
    }
    @{
        name        = "Haftalik durum paketi (DEMO)"
        kind        = "internal"
        wbsId       = $null
        status      = "draft"
        dueDate     = "2026-11-30T00:00:00.000Z"
        resourceIds = @()
        recipient   = $null
        note        = $null
    }
    @{
        name        = "Eski ISO paketi (DEMO)"
        kind        = "audit"
        wbsId       = $wbs32.id
        status      = "withdrawn"
        dueDate     = "2026-06-01T00:00:00.000Z"
        resourceIds = @($sourceId)
        recipient   = "Dis denetci (DEMO)"
        note        = "Kapsam degisti; yeni ISO paketi acildi."
    }
    @{
        name        = "Kick-off tutanak seti (DEMO)"
        kind        = "customer"
        wbsId       = $wbs11.id
        status      = "assembled"
        dueDate     = "2020-03-01T00:00:00.000Z"
        resourceIds = @($safetyId)
        recipient   = "Musteri saha muduru"
        note        = $null
    }
)

foreach ($seed in $seeds) {
    $key = $seed.name.ToLowerInvariant()
    if ($existingNames.ContainsKey($key)) {
        Write-Host "SKIP $($seed.name)" -ForegroundColor Yellow
        continue
    }
    $row = Invoke-Json "$ops/projects/$ProjectId/audit-packs" "POST" $seed
    Write-Host ("CREATE {0} status={1} open={2} incomplete={3} overdue={4} items={5}" -f $row.name, $row.status, $row.open, $row.incomplete, $row.overdue, $row.itemCount)
}

$after = Invoke-Json "$ops/projects/$ProjectId/audit-packs"
$status = Invoke-Json "$ops/projects/$ProjectId/status"
Write-Host ""
Write-Host ("packs={0} open={1} incomplete={2} overdue={3}" -f @($after.items).Count, $after.openCount, $after.incompleteCount, $after.overdueCount)
Write-Host ("status counts openAuditPack={0} incompleteAuditPack={1} overdueAuditPack={2}" -f $status.counts.openAuditPack, $status.counts.incompleteAuditPack, $status.counts.overdueAuditPack)
$flagWbs = @($status.items) | Where-Object {
    @($_.flags) -contains "openAuditPack" -or
    @($_.flags) -contains "incompleteAuditPack" -or
    @($_.flags) -contains "overdueAuditPack"
}
foreach ($row in $flagWbs) {
    Write-Host ("WBS {0} {1} flags={2}" -f $row.wbsCode, $row.name, (@($row.flags) -join ","))
}
Write-Host "SEED audit pack OK" -ForegroundColor Green
