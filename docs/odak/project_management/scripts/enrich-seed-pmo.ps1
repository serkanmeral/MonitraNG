# SEED-PMO zenginlestirme: tarihler, FS bagimliliklari, ilerleme, baseline (Gantt icin).
#
#   pwsh .\docs\odak\project_management\scripts\enrich-seed-pmo.ps1
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$ProjectCode = "SEED-PMO",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

function Get-Token {
    if (Test-Path $loadToken) {
        $fresh = & $loadToken -AutoRefresh
        if ($fresh) { return $fresh.Trim() }
    }
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    throw "Token alinamadi."
}

function Invoke-Ops {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [int[]]$ExpectStatus = @(200, 201, 204)
    )
    $uri = "$Gateway/operations/api/v1$Path"
    $status = 0
    $params = @{
        Uri                  = $uri
        Method               = $Method
        Headers              = $script:Headers
        TimeoutSec           = 120
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
    }
    $result = Invoke-RestMethod @params
    $script:LastStatus = [int]$status
    if ($ExpectStatus -notcontains $script:LastStatus) {
        $err = $null
        if ($result) {
            try { $err = $result | ConvertTo-Json -Compress -Depth 4 } catch { $err = [string]$result }
        }
        throw "HTTP $script:LastStatus $Method $Path : $err"
    }
    return , $result
}

function Day([string]$ymd) { return ([datetime]::ParseExact($ymd, "yyyy-MM-dd", [cultureinfo]::InvariantCulture)).ToUniversalTime().ToString("o") }

function Get-Name($row) {
    $v = [string]$row.name
    if (-not $v) { $v = [string]$row.Name }
    return $v
}

function Get-Id($row) {
    foreach ($k in @("id", "Id")) {
        $v = [string]$row.$k
        if ($v) { return $v }
    }
    return ""
}

function Find-ByName($rows, [string]$name, [string]$kind = $null) {
    $n = Normalize-Name $name
    $candidates = @($rows | Where-Object { (Normalize-Name (Get-Name $_)) -eq $n })
    if ($kind) {
        $k = $kind.ToLowerInvariant()
        $hit = @($candidates | Where-Object {
                $x = [string]$_.kind; if (-not $x) { $x = [string]$_.Kind }
                $x.ToLowerInvariant() -eq $k
            } | Select-Object -First 1)[0]
        if ($hit) { return $hit }
    }
    return @($candidates | Select-Object -First 1)[0]
}

function Normalize-Name([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return "" }
    $x = $name.Trim().ToLowerInvariant()
    $x = $x.Replace([char]0x0131, "i").Replace([char]0x0130, "i")
    $x = $x.Replace("ı", "i").Replace("İ", "i")
    $x = $x.Replace("ş", "s").Replace("ğ", "g").Replace("ü", "u").Replace("ö", "o").Replace("ç", "c")
    return $x
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host "Enrich $ProjectCode for Gantt -> $Gateway" -ForegroundColor Cyan

$allRaw = Invoke-Ops -Path "/projects"
$all = @($allRaw)
if ($all.Count -eq 1 -and $null -ne $all[0].items) { $all = @($all[0].items) }
elseif ($all.Count -eq 1 -and $null -ne $all[0].Items) { $all = @($all[0].Items) }
$proj = @($all | Where-Object { [string]$_.code -eq $ProjectCode -or [string]$_.Code -eq $ProjectCode } | Select-Object -First 1)[0]
if (-not $proj) { throw "Proje yok: $ProjectCode (liste=$(@($all).Count))" }
$projectId = Get-Id $proj
Write-Host "  project id=$projectId"

Invoke-Ops -Method PUT -Path "/projects/$projectId" -Body @{
    status        = "active"
    plannedStart  = (Day "2026-09-01")
    plannedFinish = (Day "2026-10-31")
    description   = "Demo PMO: Gantt tarihleri, FS baglantilari ve baseline ile zenginlestirilmis ornek."
} | Out-Null
Write-Host "  OK proje tarihler"

$detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
$wbs = @()
if ($detail.wbs) { $wbs = @($detail.wbs) } elseif ($detail.Wbs) { $wbs = @($detail.Wbs) }

# Leaf schedule (summaries inherit via Gantt child envelope)
$plan = @(
    @{ name = "Kick-off"; kind = "task"; start = "2026-09-01"; finish = "2026-09-03"; pct = 100; actualStart = "2026-09-01"; actualFinish = "2026-09-03" }
    @{ name = "Kapsam onayı"; kind = "milestone"; start = "2026-09-05"; finish = "2026-09-05"; pct = 100; actualStart = "2026-09-05"; actualFinish = "2026-09-05" }
    @{ name = "Proje planı"; kind = "task"; start = "2026-09-08"; finish = "2026-09-19"; pct = 80; actualStart = "2026-09-08"; actualFinish = $null }
    @{ name = "Baseline"; kind = "milestone"; start = "2026-09-22"; finish = "2026-09-22"; pct = 0; actualStart = $null; actualFinish = $null }
    @{ name = "Durum raporu"; kind = "task"; start = "2026-09-23"; finish = "2026-10-10"; pct = 35; actualStart = "2026-09-23"; actualFinish = $null }
    @{ name = "Teslimatlar"; kind = "task"; start = "2026-10-06"; finish = "2026-10-24"; pct = 15; actualStart = $null; actualFinish = $null }
    @{ name = "Teslim ve devir"; kind = "task"; start = "2026-10-27"; finish = "2026-10-29"; pct = 0; actualStart = $null; actualFinish = $null }
    @{ name = "Kapanış"; kind = "milestone"; start = "2026-10-31"; finish = "2026-10-31"; pct = 0; actualStart = $null; actualFinish = $null }
)

$idByName = @{}
foreach ($row in $plan) {
    $match = Find-ByName $wbs $row.name $row.kind
    if (-not $match) { throw "WBS bulunamadi: $($row.name) ($($row.kind))" }
    $wid = Get-Id $match
    $idByName[$row.name] = $wid
    $body = @{
        plannedStart    = (Day $row.start)
        plannedFinish   = (Day $row.finish)
        percentComplete = [double]$row.pct
    }
    if ($row.actualStart) { $body.actualStart = (Day $row.actualStart) }
    if ($row.actualFinish) { $body.actualFinish = (Day $row.actualFinish) }
    Invoke-Ops -Method PUT -Path "/wbs/$wid" -Body $body | Out-Null
    Write-Host "  OK $($row.name) $($row.start)->$($row.finish) pct=$($row.pct)"
}

$summaries = @(
    @{ name = "Başlatma"; start = "2026-09-01"; finish = "2026-09-05"; pct = 100 }
    @{ name = "Planlama"; start = "2026-09-08"; finish = "2026-09-22"; pct = 55 }
    @{ name = "Yürütme"; start = "2026-09-23"; finish = "2026-10-24"; pct = 25 }
    @{ name = "Kapanış"; start = "2026-10-27"; finish = "2026-10-31"; pct = 0 }
)
foreach ($s in $summaries) {
    $match = Find-ByName $wbs $s.name "summary"
    if (-not $match) { Write-Host "  SKIP summary $($s.name)"; continue }
    $wid = Get-Id $match
    Invoke-Ops -Method PUT -Path "/wbs/$wid" -Body @{
        plannedStart    = (Day $s.start)
        plannedFinish   = (Day $s.finish)
        percentComplete = [double]$s.pct
    } | Out-Null
    Write-Host "  OK summary $($s.name)"
}

# FS chain for Gantt links
$chain = @(
    @{ Pred = "Kick-off"; Succ = "Kapsam onayı" }
    @{ Pred = "Kapsam onayı"; Succ = "Proje planı" }
    @{ Pred = "Proje planı"; Succ = "Baseline" }
    @{ Pred = "Baseline"; Succ = "Durum raporu" }
    @{ Pred = "Durum raporu"; Succ = "Teslimatlar" }
    @{ Pred = "Teslimatlar"; Succ = "Teslim ve devir" }
    @{ Pred = "Teslim ve devir"; Succ = "Kapanış" }
)

$existingDeps = @()
if ($detail.dependencies) { $existingDeps = @($detail.dependencies) }
elseif ($detail.Dependencies) { $existingDeps = @($detail.Dependencies) }
foreach ($dep in $existingDeps) {
    $did = Get-Id $dep
    if ($did) {
        try { Invoke-Ops -Method DELETE -Path "/dependencies/$did" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
}

foreach ($link in $chain) {
    $pred = $idByName[$link.Pred]
    $succ = $idByName[$link.Succ]
    if (-not $pred -or -not $succ) { throw "Dep id yok: $($link.Pred) -> $($link.Succ)" }
    Invoke-Ops -Method POST -Path "/projects/$projectId/dependencies" -Body @{
        predecessorId = $pred
        successorId   = $succ
        type          = "FS"
        lagDays       = 0
    } | Out-Null
    Write-Host "  OK FS $($link.Pred) -> $($link.Succ)"
}

Invoke-Ops -Method POST -Path "/projects/$projectId/baseline" -Body @{
    note = "Demo baseline — Gantt karsilastirmasi icin"
} | Out-Null
Write-Host "  OK baseline"

# Slight finish drift on Teslimatlar so baselineDrifted can appear after edit optional — skip for clean demo

$check = @(Invoke-Ops -Path "/projects/$projectId")[0]
$cw = if ($check.wbs) { @($check.wbs) } else { @($check.Wbs) }
$cd = if ($check.dependencies) { @($check.dependencies) } else { @($check.Dependencies) }
$dated = @($cw | Where-Object { $_.plannedStart -or $_.PlannedStart }).Count
Write-Host ""
Write-Host "SEED-PMO enrich OK  datedWbs=$dated deps=$(@($cd).Count)" -ForegroundColor Green
Write-Host "UI: /apps/project-management/$projectId  -> Gantt sekmesi"
