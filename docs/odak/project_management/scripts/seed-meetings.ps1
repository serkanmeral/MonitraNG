# TEST-only: SEED-PMO meeting calendar demo. Do not run against prod.
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

Write-Host "SEED-PMO meeting seed  project=$ProjectId" -ForegroundColor Cyan

$detail = Invoke-Json "$ops/projects/$ProjectId"
$wbs = @($detail.wbs)
$project = $detail.project
if (-not $project) { $project = $detail }
if ($wbs.Count -eq 0) { throw "WBS bos." }
$wbs11 = Find-Wbs $wbs "1.1"

$workRaw = Invoke-Json "$ops/projects/$ProjectId/work-items"
$workItems = @($workRaw)
if ($workItems.Count -eq 1 -and $null -ne $workRaw.items) { $workItems = @($workRaw.items) }
$wi11 = @($workItems) | Where-Object { [string]$_.key -eq "SEEDPMO-0002" } | Select-Object -First 1
if (-not $wi11) { $wi11 = @($workItems) | Where-Object { [string]$_.title -eq "Kick-off" } | Select-Object -First 1 }
$workItemId = if ($wi11) { [string]$wi11.id } else { $null }

$hubId = [string]$project.diFolderId
if (-not $hubId) { throw "Proje diFolderId yok." }
$hubKids = Invoke-Json "$doc/children?parentId=$([uri]::EscapeDataString($hubId))&limit=100"
$wiki = @($hubKids.items) | Where-Object { $_.type -eq "folder" -and $_.name -eq "Wiki" } | Select-Object -First 1
if (-not $wiki) { throw "Wiki klasoru yok." }
$wikiId = [string]$wiki.id

$kickMinutesId = Ensure-Markdown $wikiId "Kick-off tutanak (DEMO)" @"
# Kick-off tutanak (DEMO)

Tarih: 15 Eylul 2026
Katilimcilar: PM, is paketi sahipleri

- Omurga ve WBS gozden gecirildi
- Haftalik PMO serisi Pazartesi 09:00 olarak kararlastirildi
"@

$pmoMinutesId = Ensure-Markdown $wikiId "Haftalik PMO 21 Eylul (DEMO)" @"
# Haftalik PMO 21 Eylul (DEMO)

Gundem: durum, riskler, sonraki hafta.
Tutanak ornegi; Outlook / Teams yok.
"@

Write-Host "Docs kick=$kickMinutesId pmo=$pmoMinutesId  WBS 1.1=$($wbs11.id)  WI=$workItemId"

$pack = Invoke-Json "$ops/projects/$ProjectId/meetings"
$existingMeetings = @($pack.items)
$existingSeries = @($pack.series)
$seriesByName = @{}
foreach ($row in $existingSeries) {
    $seriesByName[([string]$row.name).Trim().ToLowerInvariant()] = $row
}
$meetingByName = @{}
foreach ($row in $existingMeetings) {
    $meetingByName[([string]$row.name).Trim().ToLowerInvariant()] = $row
}

$seriesName = "Haftalik PMO (DEMO)"
$series = $seriesByName[$seriesName.ToLowerInvariant()]
if ($series) {
    Write-Host "SKIP series $seriesName" -ForegroundColor Yellow
}
else {
    $series = Invoke-Json "$ops/projects/$ProjectId/meeting-series" "POST" @{
        name            = $seriesName
        wbsId           = $wbs11.id
        weekday         = 1
        startTime       = "09:00"
        durationMinutes = 60
        firstStart      = "2026-09-21T09:00:00+03:00"
        until           = "2026-12-16T00:00:00+03:00"
        location        = "PMO odasi"
        attendees       = "PM, is paketi sahipleri"
        agenda          = "Durum, riskler, kararlar, sonraki hafta."
        note            = "Demo seri. Dis takvim yok."
    }
    Write-Host "CREATE series $seriesName occurrences=$($series.occurrenceCount)" -ForegroundColor Green
}

$kickName = "Kick-off tutanak (DEMO)"
$kick = $meetingByName[$kickName.ToLowerInvariant()]
if ($kick) {
    Write-Host "SKIP meeting $kickName" -ForegroundColor Yellow
}
else {
    $kick = Invoke-Json "$ops/projects/$ProjectId/meetings" "POST" @{
        name              = $kickName
        startAt           = "2026-09-15T10:00:00+03:00"
        endAt             = "2026-09-15T11:00:00+03:00"
        status            = "held"
        minutesResourceId = $kickMinutesId
        wbsId             = $wbs11.id
        attendees         = "PM, is paketi sahipleri"
        location          = "PMO odasi"
        agenda            = "Proje acilisi, WBS, haftalik ritim."
        note              = "Gecmis olay; tutanak bagli."
    }
    Write-Host "CREATE meeting $kickName" -ForegroundColor Green
}

$pack = Invoke-Json "$ops/projects/$ProjectId/meetings"
$existingMeetings = @($pack.items)
$seriesId = [string]$series.id

$firstOcc = @($existingMeetings) |
    Where-Object { [string]$_.seriesId -eq $seriesId } |
    Sort-Object { [datetime]($_.startAt ?? $_.heldAt) } |
    Select-Object -First 1

if ($firstOcc) {
    if ([string]$firstOcc.minutesResourceId) {
        Write-Host "SKIP minutes on first occurrence" -ForegroundColor Yellow
    }
    else {
        $null = Invoke-Json "$ops/meetings/$($firstOcc.id)" "PUT" @{
            minutesResourceId = $pmoMinutesId
            status            = "scheduled"
        }
        Write-Host "PATCH first occurrence minutes" -ForegroundColor Green
    }

    $actionTitle = "WBS tarihlerini netlestir (DEMO)"
    $hasAction = @($firstOcc.actions) | Where-Object { ([string]$_.title).Trim().ToLowerInvariant() -eq $actionTitle.ToLowerInvariant() }
    if ($hasAction) {
        Write-Host "SKIP action $actionTitle" -ForegroundColor Yellow
    }
    else {
        $actionBody = @{
            title     = $actionTitle
            ownerName = "PM"
            dueDate   = "2026-09-30T00:00:00.000Z"
            status    = "open"
            wbsId     = $wbs11.id
            note      = "Toplanti aksiyonu; Bitti OC kapatmaz."
        }
        if ($workItemId) { $actionBody.workItemId = $workItemId }
        $null = Invoke-Json "$ops/meetings/$($firstOcc.id)/actions" "POST" $actionBody
        Write-Host "CREATE action $actionTitle" -ForegroundColor Green
    }
}

$alreadyCancelled = @($existingMeetings) | Where-Object {
    [string]$_.seriesId -eq $seriesId -and ([string]$_.status).ToLowerInvariant() -eq "cancelled"
} | Select-Object -First 1
if ($alreadyCancelled) {
    Write-Host "SKIP cancel occurrence" -ForegroundColor Yellow
}
else {
    $cancelOcc = @($existingMeetings) |
        Where-Object {
            [string]$_.seriesId -eq $seriesId -and
            ([string]$_.id) -ne ([string]$firstOcc.id)
        } |
        Sort-Object { [datetime]($_.startAt ?? $_.heldAt) } |
        Select-Object -Skip 1 -First 1
    if ($cancelOcc) {
        $null = Invoke-Json "$ops/meetings/$($cancelOcc.id)" "PUT" @{ status = "cancelled" }
        Write-Host "CANCEL occurrence $($cancelOcc.startAt)" -ForegroundColor Green
    }
    else {
        Write-Host "SKIP cancel occurrence" -ForegroundColor Yellow
    }
}

$final = Invoke-Json "$ops/projects/$ProjectId/meetings"
Write-Host ("DONE meetings={0} series={1} openActions={2}" -f @($final.items).Count, @($final.series).Count, $final.openActionCount) -ForegroundColor Green
