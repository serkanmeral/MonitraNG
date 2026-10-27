# Import BVA_KP Word minutes into one project.
# Meetings, attendance, assignments (pm_meeting_actions targets) and the plain minutes page.
# Past meetings are stored as held. Assignments stay open.
# Idempotent: an existing meeting with the same name and start is skipped.
#
# Local: -GatewayBaseUrl https://localhost:5040
# Password is mandatory and is not stored in this file.

param(
    [string]$GatewayBaseUrl = "https://localhost:5040",
    [string]$Domain = "proline",
    [string]$Username = "kivanc.pak",
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$ProjectCode = "TCDD-BV",
    [string]$SourceDir = "C:\Users\serkan.meral\Desktop\Turkcar\CONFLUENCE YEDEKLEME (2026.08.26)\Toplantı Tutanakları\01. BVA_KP Projesi"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

if (-not (Test-Path -LiteralPath $SourceDir)) { throw "Klasor yok: $SourceDir" }

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$parser = Join-Path $scriptDir "parse-bva-minutes.py"
$jsonPath = Join-Path $env:TEMP "import-bva-minutes.json"
python $parser $SourceDir $jsonPath
if ($LASTEXITCODE -ne 0) { throw "Tutanaklar okunamadi" }
$parsed = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Update-Token {
    $loginBody = @{ username = $Username; password = $Password; domain = $Domain } | ConvertTo-Json -Compress
    $loginFile = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($loginFile, $loginBody, [System.Text.UTF8Encoding]::new($false))
    $loginOut = [System.IO.Path]::GetTempFileName()
    & curl.exe -s -k -X POST -H "Content-Type: application/json" --data-binary "@$loginFile" -o $loginOut "$GatewayBaseUrl/keeper/api/auth/token" | Out-Null
    Remove-Item $loginFile -Force
    $loginRaw = [System.IO.File]::ReadAllText($loginOut).Trim().TrimStart([char]0xFEFF)
    Remove-Item $loginOut -Force
    $login = $loginRaw | ConvertFrom-Json
    if (-not $login.accessToken) { throw "Token alinamadi" }
    $script:token = $login.accessToken
}

Update-Token

function Invoke-Api([string]$Method, [string]$Url, [string]$BodyFile = "") {
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        $out = [System.IO.Path]::GetTempFileName()
        $args = @("-s", "-k", "-X", $Method, "-w", "%{http_code}", "-o", $out, "-H", "Authorization: Bearer $script:token", "--max-time", "90")
        if ($BodyFile) { $args += @("-H", "Content-Type: application/json; charset=utf-8", "--data-binary", "@$BodyFile") }
        $code = (& curl.exe @args $Url)
        $text = ""
        if (Test-Path -LiteralPath $out) {
            $text = [System.IO.File]::ReadAllText($out)
            Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue
        }
        $status = "$code".Trim()
        if ($status -eq "401" -and $attempt -eq 1) {
            Update-Token
            continue
        }
        return @{ Code = $status; Body = $text }
    }
}

function ConvertTo-JsonString([string]$Value) {
    if ($null -eq $Value) { return "null" }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $Value.ToCharArray()) {
        $code = [int]$ch
        switch ($code) {
            8 { [void]$sb.Append('\b'); break }
            9 { [void]$sb.Append('\t'); break }
            10 { [void]$sb.Append('\n'); break }
            12 { [void]$sb.Append('\f'); break }
            13 { [void]$sb.Append('\r'); break }
            34 { [void]$sb.Append('\"'); break }
            92 { [void]$sb.Append('\\'); break }
            default {
                if ($code -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', $code) }
                else { [void]$sb.Append($ch) }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Get-TrKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    $text = ([regex]::Replace($Value, '\s+', ' ')).Trim()
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $text.ToCharArray()) {
        if ($ch -eq [char]0x0130) { [void]$sb.Append('i') }
        elseif ($ch -eq 'I') { [void]$sb.Append([char]0x0131) }
        else { [void]$sb.Append(([string]$ch).ToLowerInvariant()) }
    }
    return $sb.ToString()
}

function Get-UtcStamp($Value) {
    if ($null -eq $Value -or "$Value" -eq "") { return $null }
    if ($Value -is [datetime]) {
        $utc = if ($Value.Kind -eq [DateTimeKind]::Utc) {
            $Value
        } elseif ($Value.Kind -eq [DateTimeKind]::Local) {
            $Value.ToUniversalTime()
        } else {
            [DateTime]::SpecifyKind($Value, [DateTimeKind]::Local).ToUniversalTime()
        }
        return $utc.ToString("yyyy-MM-ddTHH:mm:ssZ")
    }
    $text = [string]$Value
    if ($text.StartsWith("t:")) { $text = $text.Substring(2) }
    $parsed = [DateTimeOffset]::Parse($text, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind)
    return $parsed.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
}

function Get-WireInstant($Value) {
    $text = [string]$Value
    if ($text.StartsWith("t:")) { return $text.Substring(2) }
    return (Get-UtcStamp $Value)
}

function Write-BodyFile([string]$Json) {
    $file = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($file, $Json, [System.Text.UTF8Encoding]::new($false))
    return $file
}

$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..\..")).Path
$datasetFile = Join-Path $repoRoot "docs\odak\project_management\datasets\pm_datasets.json"
$datasetPayload = Join-Path $env:TEMP "pm-meeting-actions-schema.json"
python -c @"
import json, sys
src, dest = sys.argv[1], sys.argv[2]
rows = json.load(open(src, encoding='utf-8'))
row = next(item for item in rows if item.get('name') == 'pm_meeting_actions')
payload = {'description': row.get('description'), 'fields': row.get('fields')}
json.dump(payload, open(dest, 'w', encoding='utf-8'), ensure_ascii=False)
"@ $datasetFile $datasetPayload
if ($LASTEXITCODE -ne 0) { throw "Sema paketi hazirlanamadi" }

$current = Invoke-Api GET "$GatewayBaseUrl/data/api/v1/datasets/pm_meeting_actions"
if ($current.Code -ne "200") { throw "Dataset okunamadi HTTP $($current.Code)" }
$live = $current.Body | ConvertFrom-Json
$hasCode = @($live.fields | Where-Object { $_.name -eq "code" }).Count -gt 0
$hasTargets = @($live.fields | Where-Object { $_.name -eq "targets" }).Count -gt 0
if (-not $hasCode -or -not $hasTargets) {
    $updated = Invoke-Api PUT "$GatewayBaseUrl/data/api/v1/datasets/pm_meeting_actions" $datasetPayload
    if ($updated.Code -ne "200") { throw "Dataset guncellenemedi HTTP $($updated.Code) $($updated.Body)" }
    Write-Host "pm_meeting_actions semasi guncellendi"
} else {
    Write-Host "pm_meeting_actions semasi hazir"
}

$projRes = Invoke-Api GET "$GatewayBaseUrl/operations/api/v1/projects"
if ($projRes.Code -ne "200") { throw "projeler HTTP $($projRes.Code)" }
$project = @($projRes.Body | ConvertFrom-Json) | Where-Object { $_.code -eq $ProjectCode } | Select-Object -First 1
if (-not $project) { throw "Proje yok: $ProjectCode" }
$projectId = $project.id
$hubId = "$($project.diFolderId)".Trim()
if (-not $hubId) { throw "Proje kutuphane klasoru yok: $ProjectCode" }

$peopleRes = Invoke-Api GET "$GatewayBaseUrl/operations/api/v1/projects/$projectId/meeting-people"
if ($peopleRes.Code -ne "200") { throw "kisiler HTTP $($peopleRes.Code)" }
$peopleByName = @{}
foreach ($person in @($peopleRes.Body | ConvertFrom-Json)) {
    $key = Get-TrKey $person.name
    if (-not $key) { continue }
    if (-not $peopleByName.ContainsKey($key)) { $peopleByName[$key] = @() }
    $peopleByName[$key] += $person
}

$usersByName = @{}
$page = 1
do {
    $userRes = Invoke-Api GET "$GatewayBaseUrl/keeper/api/user?page=$page&pageSize=200&isActive=true"
    if ($userRes.Code -ne "200") { throw "kullanicilar HTTP $($userRes.Code)" }
    $userPack = $userRes.Body | ConvertFrom-Json
    foreach ($user in @($userPack.users)) {
        $full = ("$($user.firstName) $($user.lastName)").Trim()
        $key = Get-TrKey $full
        if (-not $key) { continue }
        if (-not $usersByName.ContainsKey($key)) { $usersByName[$key] = @() }
        $usersByName[$key] += $user
    }
    $page++
} while ($page -le [int]$userPack.totalPages -and $page -le 20)

$existing = @{}
$skip = 0
do {
    $meetRes = Invoke-Api GET "$GatewayBaseUrl/operations/api/v1/projects/$projectId/meetings?kind=all&includeSeries=false&skip=$skip&take=200"
    if ($meetRes.Code -ne "200") { throw "toplantilar HTTP $($meetRes.Code)" }
    $pack = $meetRes.Body | ConvertFrom-Json
    $batch = @($pack.items)
    foreach ($row in $batch) {
        $start = $row.startAt
        if (-not $start) { $start = $row.heldAt }
        if (-not $start) { continue }
        $stamp = Get-UtcStamp $start
        $existing[(Get-TrKey $row.name) + "|" + $stamp] = $row.id
    }
    if ($batch.Count -lt 200) { break }
    $skip += 200
} while ($skip -lt 2000)

$folderName = "Toplantı notları"
$childRes = Invoke-Api GET "$GatewayBaseUrl/documents/api/v1/resources/children?parentId=$hubId&limit=200"
if ($childRes.Code -ne "200") { throw "kutuphane HTTP $($childRes.Code)" }
$children = $childRes.Body | ConvertFrom-Json
$folder = @($children.items) | Where-Object { $_.type -eq "folder" -and $_.name -eq $folderName } | Select-Object -First 1
if (-not $folder) {
    $folderJson = '{"parentId":' + (ConvertTo-JsonString $hubId) + ',"name":' + (ConvertTo-JsonString $folderName) + '}'
    $folderFile = Write-BodyFile $folderJson
    $made = Invoke-Api POST "$GatewayBaseUrl/documents/api/v1/resources/folder" $folderFile
    Remove-Item $folderFile -Force
    if ($made.Code -ne "201" -and $made.Code -ne "200") { throw "klasor acilamadi HTTP $($made.Code)" }
    $folder = $made.Body | ConvertFrom-Json
}
$folderId = $folder.id

$createdMeetings = 0
$skippedMeetings = 0
$createdActions = 0
$createdPeople = 0
$unmatched = New-Object System.Collections.Generic.List[string]
$failures = New-Object System.Collections.Generic.List[string]
$now = [DateTimeOffset]::UtcNow
$prolineKey = Get-TrKey "PROLINE"

function Add-MeetingAttendance([string]$MeetingId, $MeetingRow) {
    foreach ($person in @($MeetingRow.participants)) {
        $orgKey = Get-TrKey $person.organization
        $nameKey = Get-TrKey $person.name
        if ($orgKey -eq $prolineKey) {
            $matches = @()
            if ($usersByName.ContainsKey($nameKey)) { $matches = @($usersByName[$nameKey]) }
            if ($matches.Count -ne 1) {
                $unmatched.Add("$($MeetingRow.file): PROLINE kullanıcı yok veya birden fazla: $($person.name)")
                continue
            }
            $user = $matches[0]
            $display = ("$($user.firstName) $($user.lastName)").Trim()
            $body = '{"kind":"user","userId":' + (ConvertTo-JsonString $user.userId) +
                ',"displayName":' + (ConvertTo-JsonString $display) +
                ',"presence":"attended","expected":true,"attended":true}'
        } else {
            $matches = @()
            if ($peopleByName.ContainsKey($nameKey)) { $matches = @($peopleByName[$nameKey]) }
            if ($matches.Count -ne 1) {
                $unmatched.Add("$($MeetingRow.file): kişi kartı yok veya birden fazla: $($person.name) ($($person.organization))")
                continue
            }
            $card = $matches[0]
            $body = '{"kind":"external","personId":' + (ConvertTo-JsonString $card.id) +
                ',"displayName":' + (ConvertTo-JsonString $card.name) +
                ',"presence":"attended","expected":true,"attended":true}'
        }
        $attendFile = Write-BodyFile $body
        $attend = Invoke-Api POST "$GatewayBaseUrl/operations/api/v1/meetings/$MeetingId/attendance" $attendFile
        Remove-Item $attendFile -Force
        if ($attend.Code -eq "201" -or $attend.Code -eq "200") { $script:createdPeople++ }
        elseif ($attend.Code -ne "409") {
            $failures.Add("$($MeetingRow.file): katılım $($person.name) HTTP $($attend.Code)")
        }
    }
}

foreach ($meeting in @($parsed.meetings)) {
    if (-not $meeting.name -or -not $meeting.start) {
        $failures.Add("$($meeting.file): ad veya tarih yok ($($meeting.warnings -join '; '))")
        continue
    }
    $stamp = Get-UtcStamp $meeting.start
    $slot = (Get-TrKey $meeting.name) + "|" + $stamp
    if ($existing.ContainsKey($slot)) {
        $skippedMeetings++
        if ($skippedMeetings -eq 1 -or $skippedMeetings % 25 -eq 0) { Write-Host "atlanan=$skippedMeetings" }
        Add-MeetingAttendance $existing[$slot] $meeting
        continue
    }

    $status = "scheduled"
    $startWire = Get-WireInstant $meeting.start
    $endWire = Get-WireInstant $meeting.end
    if ([DateTimeOffset]::Parse($startWire, [System.Globalization.CultureInfo]::InvariantCulture) -le $now) { $status = "held" }
    $meetingJson = '{"name":' + (ConvertTo-JsonString $meeting.name) +
        ',"startAt":' + (ConvertTo-JsonString $startWire) +
        ',"endAt":' + (ConvertTo-JsonString $endWire) +
        ',"heldAt":' + (ConvertTo-JsonString $startWire) +
        ',"status":"' + $status + '"' +
        ',"location":' + ($(if ($meeting.location) { ConvertTo-JsonString $meeting.location } else { "null" })) +
        ',"agenda":' + ($(if ($meeting.agenda) { ConvertTo-JsonString $meeting.agenda } else { "null" })) + '}'
    $meetingFile = Write-BodyFile $meetingJson
    $madeMeeting = Invoke-Api POST "$GatewayBaseUrl/operations/api/v1/projects/$projectId/meetings" $meetingFile
    Remove-Item $meetingFile -Force
    if ($madeMeeting.Code -eq "409") {
        $skippedMeetings++
        $existing[$slot] = $true
        continue
    }
    if ($madeMeeting.Code -ne "201" -and $madeMeeting.Code -ne "200") {
        $failures.Add("$($meeting.file): toplantı HTTP $($madeMeeting.Code)")
        continue
    }
    $saved = $madeMeeting.Body | ConvertFrom-Json
    $meetingId = $saved.id
    $existing[$slot] = $meetingId
    $createdMeetings++
    if ($createdMeetings -eq 1 -or $createdMeetings % 10 -eq 0) { Write-Host "yazılan=$createdMeetings son=$($meeting.file)" }
    Add-MeetingAttendance $meetingId $meeting

    $seenTitles = @{}
    foreach ($action in @($meeting.actions)) {
        $title = "$($action.title)".Trim()
        if (-not $title) { continue }
        $titleKey = Get-TrKey $title
        if ($seenTitles.ContainsKey($titleKey)) {
            $title = "$($action.code) — $title"
            $titleKey = Get-TrKey $title
        }
        $seenTitles[$titleKey] = $true
        if ($title.Length -gt 4000) { $title = $title.Substring(0, 4000) }
        $targetParts = @()
        foreach ($firm in @($action.firms)) {
            $firmName = "$firm".Trim()
            if (-not $firmName) { continue }
            $targetParts += '{"kind":"organization","organization":' + (ConvertTo-JsonString $firmName) + ',"name":' + (ConvertTo-JsonString $firmName) + '}'
        }
        $dueJson = "null"
        if ($action.due) {
            $dueText = [string]$action.due
            if ($dueText.StartsWith("d:")) { $dueText = $dueText.Substring(2) }
            $dueJson = ConvertTo-JsonString ($dueText.Substring(0, 10) + "T00:00:00.000Z")
        }
        $codeJson = "null"
        if ($action.code) { $codeJson = ConvertTo-JsonString "$($action.code)" }
        $actionJson = '{"title":' + (ConvertTo-JsonString $title) +
            ',"code":' + $codeJson +
            ',"targets":[' + ($targetParts -join ",") + ']' +
            ',"dueDate":' + $dueJson +
            ',"status":"open"}'
        $actionFile = Write-BodyFile $actionJson
        $madeAction = Invoke-Api POST "$GatewayBaseUrl/operations/api/v1/meetings/$meetingId/actions" $actionFile
        Remove-Item $actionFile -Force
        if ($madeAction.Code -eq "201" -or $madeAction.Code -eq "200") { $createdActions++ }
        else { $failures.Add("$($meeting.file): $($action.code) HTTP $($madeAction.Code)") }
    }

    $pageBody = "$($meeting.topics)"
    if ([string]::IsNullOrWhiteSpace($pageBody)) { $pageBody = "Bu tutanakta görüşülen konular başlığı yok." }
    $pageTitle = "$($meeting.name) — Tutanak"
    $pageJson = '{"parentId":' + (ConvertTo-JsonString $folderId) +
        ',"title":' + (ConvertTo-JsonString $pageTitle) +
        ',"content":' + (ConvertTo-JsonString $pageBody) +
        ',"isDraft":false}'
    $pageFile = Write-BodyFile $pageJson
    $page = Invoke-Api POST "$GatewayBaseUrl/documents/api/v1/resources/markdown" $pageFile
    Remove-Item $pageFile -Force
    if ($page.Code -ne "201" -and $page.Code -ne "200") {
        $failures.Add("$($meeting.file): tutanak sayfası HTTP $($page.Code)")
        continue
    }
    $pageId = ($page.Body | ConvertFrom-Json).id
    $linkJson = '{"minutesResourceId":' + (ConvertTo-JsonString $pageId) + '}'
    $linkFile = Write-BodyFile $linkJson
    $linked = Invoke-Api PUT "$GatewayBaseUrl/operations/api/v1/meetings/$meetingId" $linkFile
    Remove-Item $linkFile -Force
    if ($linked.Code -ne "200") {
        $failures.Add("$($meeting.file): tutanak bağlanamadı HTTP $($linked.Code)")
    }
}

Write-Host "toplantı yazıldı=$createdMeetings atlandı=$skippedMeetings görev=$createdActions katılım=$createdPeople"
if ($parsed.skippedFiles) { Write-Host "kilit dosya: $($parsed.skippedFiles -join ', ')" }
$warnFiles = @($parsed.meetings | Where-Object { @($_.warnings).Count -gt 0 })
if ($warnFiles.Count -gt 0) {
    Write-Host "ayıklama uyarıları:"
    foreach ($row in $warnFiles) { Write-Host "  $($row.file): $($row.warnings -join '; ')" }
}
if ($unmatched.Count -gt 0) {
    Write-Host "eşleşmeyen kişiler ($($unmatched.Count)):"
    $unmatched | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
}
if ($failures.Count -gt 0) {
    Write-Host "hatalar ($($failures.Count)):"
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}
