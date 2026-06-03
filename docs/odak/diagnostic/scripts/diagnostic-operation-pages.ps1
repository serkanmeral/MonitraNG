# Operation Core — sayfa bazlı API yükü simülasyonu (Odak)
#
# UI sayfalarının ilk açılışında tetiklediği backend çağrılarını gruplar;
# diagnostic-benchmark.ps1 (tek endpoint) ile birlikte kullanın.
#
# Usage (repo kökünden):
#   .\docs\odak\diagnostic\scripts\diagnostic-operation-pages.ps1
#   .\docs\odak\diagnostic\scripts\diagnostic-operation-pages.ps1 -OutputJson .\docs\odak\diagnostic\reports\oc_pages.json
#
# Sayfa hedefleri (wall-clock, warm): OPERATIONAL_WORKSPACE_PERF.md §5

param(
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$WorkspaceId = "",
    [string]$BoardId = "",
    [string]$DashboardId = "",
    [string]$WorkItemId = "",
    [int]$WarmIterations = 3,
    [string]$OutputJson = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path $scriptDir "../../operationcore/scripts"
$seedFile = Join-Path $ocScriptDir "operationcore-demo-seed.json"
$dgBase = "$($GatewayBaseUrl.TrimEnd('/'))/data/api/v1/data"
$moGw = "$($GatewayBaseUrl.TrimEnd('/'))/operations"
$schedGw = "$($GatewayBaseUrl.TrimEnd('/'))/scheduler"

# Sayfa wall-clock hedefleri (ms) — warm
$pageTargets = @{
    explorer_open          = 1200
    explorer_select_board  = 900
    board_list_open        = 1200
    board_kanban_open      = 3500
    profile_open           = 1800
    dashboard_view         = 1200
    work_item_new          = 2000
    notifications_inbox    = 1500
    admin_scheduled_jobs   = 2500
    admin_ws_defs_shell    = 800
}

function Invoke-Timed {
    param(
        [string]$Label,
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $p = @{
            Uri = $Uri; Method = $Method; Headers = $Headers
            UseBasicParsing = $true; ErrorAction = "Stop"
        }
        if ($Body) { $p.Body = $Body; $p.ContentType = "application/json" }
        $r = Invoke-WebRequest @p
        $sw.Stop()
        return [PSCustomObject]@{
            Label = $Label; Uri = $Uri; Success = $true
            Ms = [long]$sw.ElapsedMilliseconds
            StatusCode = [int]$r.StatusCode
            Error = $null
        }
    }
    catch {
        $sw.Stop()
        return [PSCustomObject]@{
            Label = $Label; Uri = $Uri; Success = $false
            Ms = [long]$sw.ElapsedMilliseconds; StatusCode = $null
            Error = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        }
    }
}

function Invoke-ParallelJobs {
    param([array]$Jobs, [string]$Token)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $pool = [runspacefactory]::CreateRunspacePool(1, [Math]::Min(12, [Math]::Max(1, $Jobs.Count)))
    $pool.Open()
    $handles = @()
    $sb = {
        param($Uri, $Method, $Body, $Token)
        $inner = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $h = @{ Authorization = "Bearer $Token" }
            if ($Method -eq "POST" -and $Body) {
                $h["Content-Type"] = "application/json"
                $r = Invoke-WebRequest -Uri $Uri -Method POST -Headers $h -Body $Body -UseBasicParsing -ErrorAction Stop
            }
            else {
                $r = Invoke-WebRequest -Uri $Uri -Method GET -Headers $h -UseBasicParsing -ErrorAction Stop
            }
            $inner.Stop()
            return @{ Success = $true; Ms = $inner.ElapsedMilliseconds; Error = $null }
        }
        catch {
            $inner.Stop()
            return @{ Success = $false; Ms = $inner.ElapsedMilliseconds; Error = $_.Exception.Message }
        }
    }
    foreach ($j in $Jobs) {
        $ps = [powershell]::Create().AddScript($sb).AddArgument($j.Uri).AddArgument($j.Method).AddArgument($j.Body).AddArgument($Token)
        $ps.RunspacePool = $pool
        $handles += [PSCustomObject]@{ Label = $j.Label; Ps = $ps; Async = $ps.BeginInvoke() }
    }
    $results = @()
    foreach ($h in $handles) {
        $out = $h.Ps.EndInvoke($h.Async)
        $h.Ps.Dispose()
        $results += [PSCustomObject]@{
            Label = $h.Label; Success = $out.Success; Ms = $out.Ms; Error = $out.Error
        }
    }
    $pool.Close(); $pool.Dispose()
    $sw.Stop()
    return [PSCustomObject]@{
        WallMs = $sw.ElapsedMilliseconds
        SumMs = ($results | Measure-Object -Property Ms -Sum).Sum
        Results = $results
        AllSuccess = -not ($results | Where-Object { -not $_.Success })
    }
}

function Invoke-SequentialJobs {
    param([array]$Jobs, [hashtable]$Headers, [hashtable]$JsonHeaders)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $results = @()
    foreach ($j in $Jobs) {
        $hdr = if ($j.Method -eq "POST") { $JsonHeaders } else { $Headers }
        $results += Invoke-Timed -Label $j.Label -Uri $j.Uri -Method $j.Method -Headers $hdr -Body $j.Body
    }
    $sw.Stop()
    return [PSCustomObject]@{
        WallMs = $sw.ElapsedMilliseconds
        SumMs = ($results | Measure-Object -Property Ms -Sum).Sum
        Results = $results
        AllSuccess = -not ($results | Where-Object { -not $_.Success })
    }
}

function Measure-PageScenario {
    param(
        [string]$PageId,
        [string]$Title,
        [scriptblock]$RunOnce,
        [int]$WarmCount = 3
    )
    $targetMs = $pageTargets[$PageId]
    $walls = @()
    $details = @()

    for ($i = 0; $i -le $WarmCount; $i++) {
        $run = & $RunOnce
        $walls += $run.WallMs
        $details += $run
    }

    $warmWalls = @($walls | Select-Object -Skip 1)
    $coldWall = $walls[0]
    $warmP95 = if ($warmWalls.Count -gt 0) {
        $sorted = $warmWalls | Sort-Object
        $idx = [Math]::Min($sorted.Count - 1, [Math]::Ceiling(0.95 * $sorted.Count) - 1)
        if ($idx -lt 0) { $idx = 0 }
        $sorted[$idx]
    } else { $null }

    $ok = $null -ne $targetMs -and $null -ne $warmP95 -and $warmP95 -le $targetMs -and $details[-1].AllSuccess

    [PSCustomObject]@{
        PageId       = $PageId
        Title        = $Title
        TargetWallMs = $targetMs
        SessionColdWallMs = $coldWall
        WarmP95WallMs = $warmP95
        WarmMedianWallMs = if ($warmWalls.Count -gt 0) {
            $s = $warmWalls | Sort-Object
            $s[[Math]::Floor(($s.Count - 1) / 2)]
        } else { $null }
        Ok           = $ok
        LastRun      = $details[-1]
    }
}

# --- Main ---

Write-Host ""
Write-Host "Operation Core — Sayfa API yuku (Odak)" -ForegroundColor Cyan
Write-Host "  Gateway: $GatewayBaseUrl" -ForegroundColor Gray
Write-Host ""

$loadToken = Join-Path $ocScriptDir "load-operationcore-token.ps1"
$token = & $loadToken
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$auth = @{ Authorization = "Bearer $token" }
$json = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

if (Test-Path $seedFile) {
    $seed = Get-Content $seedFile -Raw | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($WorkspaceId)) { $WorkspaceId = $seed.workspaceId }
    if ([string]::IsNullOrEmpty($BoardId)) { $BoardId = $seed.boardId }
    if ([string]::IsNullOrEmpty($DashboardId)) { $DashboardId = $seed.dashboardId }
}

if ([string]::IsNullOrEmpty($WorkItemId)) {
    $listBody = (@{ skip = 0; take = 1 } | ConvertTo-Json -Compress)
    $list = Invoke-RestMethod -Uri "$moGw/api/v1/runtime/boards/$BoardId/list" -Method POST -Body $listBody -Headers $json
    if ($list.items -and $list.items.Count -ge 1) {
        $wi = $list.items[0]
        $WorkItemId = $wi.id
        if ([string]::IsNullOrEmpty($WorkItemId) -and $wi.PSObject.Properties['__dataId']) {
            $WorkItemId = [string]$wi.__dataId
        }
    }
}
if ([string]::IsNullOrEmpty($WorkItemId)) { throw "WorkItemId gerekli (board listesi bos)." }

Write-Host "Seed: ws=$WorkspaceId board=$BoardId dashboard=$DashboardId wi=$WorkItemId" -ForegroundColor Gray
Write-Host ""

$boardListBody = (@{ skip = 0; take = 50 } | ConvertTo-Json -Compress)
$columnQueryBody = (@{
    dataset = "op_work_items"
    parameters = @{ boardId = $BoardId; columnId = "" }
    skip = 0; take = 30
} | ConvertTo-Json -Depth 5 -Compress)

# Board context — kolon sayisi kanban sim icin
$boardCtx = Invoke-RestMethod -Uri "$moGw/api/v1/runtime/boards/$BoardId" -Method GET -Headers $auth
$columnCount = 0
if ($boardCtx.columns) { $columnCount = @($boardCtx.columns).Count }
if ($columnCount -lt 1) { $columnCount = 3 }

$scenarios = @(
    @{
        Id = "explorer_open"
        Title = "Workspace explorer — ilk acilis (workspaces + MO live)"
        Run = {
            $jobs = @(
                @{ Label = "dg_workspaces"; Uri = "$dgBase/op_workspaces?sort=name:asc&limit=200"; Method = "GET"; Body = $null }
                @{ Label = "mo_live"; Uri = "$moGw/api/v1/health/live"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    @{
        Id = "explorer_select_board"
        Title = "Explorer — workspace sec + board listesi (lazy)"
        Run = {
            $jobs = @(
                @{ Label = "dg_boards_ws"; Uri = "$dgBase/op_boards?filter=workspaceId:eq:$WorkspaceId&limit=200"; Method = "GET"; Body = $null }
                @{ Label = "dg_dashboard_record"; Uri = "$dgBase/op_dashboards/$DashboardId"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    @{
        Id = "board_list_open"
        Title = "Board sayfasi — list gorunumu (context + list + pool fields)"
        Run = {
            $jobs = @(
                @{ Label = "mo_board"; Uri = "$moGw/api/v1/runtime/boards/$BoardId"; Method = "GET"; Body = $null }
                @{ Label = "mo_list"; Uri = "$moGw/api/v1/runtime/boards/$BoardId/list"; Method = "POST"; Body = $boardListBody }
                @{ Label = "dg_pool_fields"; Uri = "$dgBase/op_fields?filter=workspaceId:eq:$WorkspaceId&limit=500"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    @{
        Id = "board_kanban_open"
        Title = "Board — kanban (context + kolon sorgulari, batch 4)"
        Run = {
            $ctx = Invoke-Timed -Label "mo_board" -Uri "$moGw/api/v1/runtime/boards/$BoardId" -Headers $auth
            $colJobs = @()
            $cols = @($boardCtx.columns)
            if ($cols.Count -lt 1 -and $seed.states) {
                foreach ($prop in $seed.states.PSObject.Properties) {
                    $cols += [PSCustomObject]@{ stateId = [string]$prop.Value }
                }
            }
            $idx = 0
            foreach ($col in $cols) {
                $sid = [string]$col.stateId
                if ([string]::IsNullOrWhiteSpace($sid)) { continue }
                $body = (@{
                    dataset    = "op_work_items"
                    parameters = @{
                        workspaceId = $WorkspaceId
                        boardId     = $BoardId
                        stateId     = $sid
                    }
                    skip       = 0
                    take       = 30
                } | ConvertTo-Json -Depth 5 -Compress)
                $colJobs += @{
                    Label  = "col_$idx"
                    Uri    = "$moGw/api/v1/runtime/queries/wi_board_column/execute"
                    Method = "POST"
                    Body   = $body
                }
                $idx++
            }
            if ($colJobs.Count -lt 1) { $colJobs = @() }
            $wall = $ctx.Ms
            $allOk = $ctx.Success
            for ($i = 0; $i -lt $colJobs.Count; $i += 4) {
                $end = [Math]::Min($i + 3, $colJobs.Count - 1)
                $batch = $colJobs[$i..$end]
                $batchRun = Invoke-ParallelJobs -Jobs $batch -Token $token
                $wall += $batchRun.WallMs
                if (-not $batchRun.AllSuccess) { $allOk = $false }
            }
            [PSCustomObject]@{ WallMs = $wall; SumMs = $wall; AllSuccess = $allOk; Results = @($ctx) }
        }
    }
    @{
        Id = "profile_open"
        Title = "Is profili — profile-view (tek toplu MO)"
        Run = {
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "profile_view"; Uri = "$moGw/api/v1/runtime/work-items/$WorkItemId/profile-view"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
    @{
        Id = "dashboard_view"
        Title = "Pano — runtime dashboard (widget execute)"
        Run = {
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "mo_dashboard"; Uri = "$moGw/api/v1/runtime/dashboards/$DashboardId"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
    @{
        Id = "work_item_new"
        Title = "Yeni is — form create context + boards"
        Run = {
            $jobs = @(
                @{ Label = "form_create"; Uri = "$moGw/api/v1/runtime/work-items/form?workspaceId=$WorkspaceId&mode=create"; Method = "GET"; Body = $null }
                @{ Label = "dg_boards"; Uri = "$dgBase/op_boards?filter=workspaceId:eq:$WorkspaceId&limit=200"; Method = "GET"; Body = $null }
                @{ Label = "dg_pool_fields"; Uri = "$dgBase/op_fields?filter=workspaceId:eq:$WorkspaceId&limit=500"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    @{
        Id = "notifications_inbox"
        Title = "Bildirimler — in-app liste"
        Run = {
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "mo_notifications"; Uri = "$moGw/api/v1/notifications?skip=0&take=50"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
    @{
        Id = "admin_scheduled_jobs"
        Title = "Admin — zamanlanmis joblar (scheduler + DG schedules)"
        Run = {
            $jobs = @(
                @{ Label = "sched_system"; Uri = "$schedGw/api/v1/system/jobs"; Method = "GET"; Body = $null }
                @{ Label = "sched_user"; Uri = "$schedGw/api/v1/user/jobs"; Method = "GET"; Body = $null }
                @{ Label = "dg_schedules"; Uri = "$dgBase/op_work_item_schedules?limit=500"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    @{
        Id = "admin_ws_defs_shell"
        Title = "Admin — workspace tanimlari kabuk (workspace listesi)"
        Run = {
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "dg_workspaces"; Uri = "$dgBase/op_workspaces?sort=name:asc&limit=200"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
)

$pageResults = @()
foreach ($sc in $scenarios) {
    Write-Host "  $($sc.Id)..." -ForegroundColor DarkGray
    $pageResults += Measure-PageScenario -PageId $sc.Id -Title $sc.Title -RunOnce $sc.Run -WarmCount $WarmIterations
}

Write-Host ""
Write-Host ("{0,-28} {1,10} {2,10} {3,10} {4,6}" -f "Sayfa", "Cold(ms)", "P95(ms)", "Hedef", "OK?") -ForegroundColor Cyan
Write-Host ("-" * 70) -ForegroundColor DarkGray
foreach ($p in $pageResults) {
    $ok = if ($p.Ok) { "YES" } elseif ($p.LastRun.AllSuccess) { "WARN" } else { "FAIL" }
    $color = switch ($ok) { "YES" { "Green" } "WARN" { "Yellow" } default { "Red" } }
    Write-Host ("{0,-28} {1,10} {2,10} {3,10} {4,6}" -f $p.PageId, $p.SessionColdWallMs, $p.WarmP95WallMs, $p.TargetWallMs, $ok) -ForegroundColor $color
}
Write-Host ""

$warn = @($pageResults | Where-Object { -not $_.Ok }).Count
Write-Host "Ozet: $($pageResults.Count) sayfa, $warn hedef disi (WARN/FAIL)" -ForegroundColor $(if ($warn -eq 0) { "Green" } else { "Yellow" })
Write-Host "Not: Wall-clock paralel batch'leri yansitir; tarayici waterfall icin DevTools Network kullanin." -ForegroundColor DarkGray
Write-Host ""

$report = @{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    environment = @{
        gatewayBaseUrl = $GatewayBaseUrl
        workspaceId = $WorkspaceId
        boardId = $BoardId
        dashboardId = $DashboardId
        workItemId = $WorkItemId
        kanbanColumnCountSimulated = $columnCount
        warmIterations = $WarmIterations
    }
    pageTargets = $pageTargets
    pages = @($pageResults | ForEach-Object {
        @{
            pageId = $_.PageId
            title = $_.Title
            targetWallMs = $_.TargetWallMs
            sessionColdWallMs = $_.SessionColdWallMs
            warmP95WallMs = $_.WarmP95WallMs
            warmMedianWallMs = $_.WarmMedianWallMs
            ok = $_.Ok
            lastRunSuccess = $_.LastRun.AllSuccess
        }
    })
}

if ([string]::IsNullOrEmpty($OutputJson)) {
    $reportDir = Join-Path $scriptDir "../reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $OutputJson = Join-Path $reportDir ("oc_pages_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON rapor: $OutputJson" -ForegroundColor Cyan
Write-Host ""

if (@($pageResults | Where-Object { -not $_.LastRun.AllSuccess }).Count -gt 0) { exit 1 }
exit 0
