# Document Intelligence (MngDocument) — sayfa bazlı API yükü simülasyonu (Odak)
#
# UI /apps/document-intelligence ilk açılışında tetiklenen backend çağrılarını ölçer.
# diagnostic-operation-pages.ps1 ile aynı metrik modeli (cold + warm P95 wall-clock).
#
# Usage (repo kökünden):
#   .\docs\odak\diagnostic\scripts\diagnostic-document-intelligence-pages.ps1
#   .\docs\odak\diagnostic\scripts\diagnostic-document-intelligence-pages.ps1 -OutputJson .\docs\odak\diagnostic\reports\di_pages.json
#
# Not: Her MngDocument endpoint'i içeride PermissionSnapshot için 2 DG sorgusu yapabilir
# (tüm klasörler + dm_resource_permissions, showHistory=true).

param(
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$FolderId = "",
    [string]$MarkdownId = "",
    [int]$WarmIterations = 3,
    [string]$OutputJson = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path $scriptDir "../../operationcore/scripts"
$dgBase = "$($GatewayBaseUrl.TrimEnd('/'))/data/api/v1/data"
$docGw = "$($GatewayBaseUrl.TrimEnd('/'))/documents/api/v1/resources"

# Sayfa wall-clock hedefleri (ms) — warm; küçük veri seti için sıkı hedef
$pageTargets = @{
    di_bootstrap_open     = 1200
    di_browse_root        = 800
    di_browse_folder      = 1200
    di_initial_open       = 2000
    di_select_folder      = 2500
    di_open_markdown      = 2000
    di_search             = 2500
    di_permissions_dialog = 2000
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
    $pool = [runspacefactory]::CreateRunspacePool(1, [Math]::Min(8, [Math]::Max(1, $Jobs.Count)))
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
Write-Host "Document Intelligence — Sayfa API yuku (Odak)" -ForegroundColor Cyan
Write-Host "  Gateway: $GatewayBaseUrl" -ForegroundColor Gray
Write-Host "  MngDocument: $docGw" -ForegroundColor Gray
Write-Host ""

$loadToken = Join-Path $ocScriptDir "load-operationcore-token.ps1"
$token = & $loadToken
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$auth = @{ Authorization = "Bearer $token" }
$json = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

# DG referans: dm_resources / dm_resource_permissions kayit sayisi
$dgRef = @()
foreach ($ds in @("dm_resources", "dm_resource_permissions", "dm_resource_versions")) {
    $dgRef += Invoke-Timed -Label "dg_$ds" -Uri "$dgBase/$ds`?limit=1" -Headers $auth
}

Write-Host "DG referans (limit=1, toplam kayit header/body):" -ForegroundColor DarkGray
foreach ($r in $dgRef) {
    $st = if ($r.Success) { "OK $($r.Ms)ms" } else { "FAIL" }
    Write-Host "  $($r.Label): $st" -ForegroundColor $(if ($r.Success) { "Gray" } else { "Red" })
}
Write-Host ""

# Ornek kaynak id'leri
$childrenRoot = Invoke-RestMethod -Uri "$docGw/children" -Method GET -Headers $auth
$items = @($childrenRoot.items)
if ($items.Count -lt 1) {
    $tree = Invoke-RestMethod -Uri "$docGw/tree" -Method GET -Headers $auth
    Write-Host "Uyari: kok children bos; tree node sayisi: $(@($tree).Count)" -ForegroundColor Yellow
}

if ([string]::IsNullOrEmpty($FolderId)) {
    $folder = $items | Where-Object { $_.type -eq "folder" } | Select-Object -First 1
    if ($folder) { $FolderId = [string]$folder.id }
}
if ([string]::IsNullOrEmpty($MarkdownId)) {
    $md = $items | Where-Object { $_.type -eq "markdown" } | Select-Object -First 1
    if ($md) { $MarkdownId = [string]$md.id }
    if ([string]::IsNullOrEmpty($MarkdownId) -and $FolderId) {
        $childMd = Invoke-RestMethod -Uri "$docGw/children?parentId=$([uri]::EscapeDataString($FolderId))" -Method GET -Headers $auth
        $md2 = @($childMd.items) | Where-Object { $_.type -eq "markdown" } | Select-Object -First 1
        if ($md2) { $MarkdownId = [string]$md2.id }
    }
}

Write-Host "Ornek id: folder=$FolderId markdown=$MarkdownId" -ForegroundColor Gray
Write-Host ""

$scenarios = @(
    @{
        Id = "di_bootstrap_open"
        Title = "Resources — bootstrap (tree + kok children, tek istek)"
        Run = {
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "bootstrap"; Uri = "$docGw/bootstrap"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
    @{
        Id = "di_initial_open"
        Title = "Resources — eski ilk acilis (tree + children paralel, karsilastirma)"
        Run = {
            $jobs = @(
                @{ Label = "tree"; Uri = "$docGw/tree"; Method = "GET"; Body = $null }
                @{ Label = "children_root"; Uri = "$docGw/children"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    @{
        Id = "di_browse_root"
        Title = "Kok klasor — browse (tek istek)"
        Run = {
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "browse"; Uri = "$docGw/browse"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
)

if (-not [string]::IsNullOrEmpty($FolderId)) {
    $scenarios += @{
        Id = "di_browse_folder"
        Title = "Klasor secimi — browse (tek istek)"
        Run = {
            $fid = [uri]::EscapeDataString($FolderId)
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "browse"; Uri = "$docGw/browse?folderId=$fid"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
    $scenarios += @{
        Id = "di_select_folder"
        Title = "Klasor secimi — eski (3 API paralel, karsilastirma)"
        Run = {
            $fid = [uri]::EscapeDataString($FolderId)
            $jobs = @(
                @{ Label = "children"; Uri = "$docGw/children?parentId=$fid"; Method = "GET"; Body = $null }
                @{ Label = "breadcrumb"; Uri = "$docGw/$fid/breadcrumb"; Method = "GET"; Body = $null }
                @{ Label = "getById"; Uri = "$docGw/$fid"; Method = "GET"; Body = $null }
            )
            Invoke-ParallelJobs -Jobs $jobs -Token $token
        }
    }
    $scenarios += @{
        Id = "di_permissions_dialog"
        Title = "Izinler diyalogu (GET permissions — snapshot + folder)"
        Run = {
            $fid = [uri]::EscapeDataString($FolderId)
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "permissions"; Uri = "$docGw/$fid/permissions"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
}

if (-not [string]::IsNullOrEmpty($MarkdownId)) {
    $scenarios += @{
        Id = "di_open_markdown"
        Title = "Markdown ac (content + breadcrumb parent, sirali)"
        Run = {
            $mid = [uri]::EscapeDataString($MarkdownId)
            Invoke-SequentialJobs -Jobs @(
                @{ Label = "markdown_content"; Uri = "$docGw/markdown/$mid/content"; Method = "GET"; Body = $null }
            ) -Headers $auth -JsonHeaders $json
        }
    }
}

$scenarios += @{
    Id = "di_search"
    Title = "Arama (q=test, limit=50)"
    Run = {
        Invoke-SequentialJobs -Jobs @(
            @{ Label = "search"; Uri = "$docGw/search?q=test&skip=0&limit=50"; Method = "GET"; Body = $null }
        ) -Headers $auth -JsonHeaders $json
    }
}

# Tekil endpoint kırılımı (warm 1)
Write-Host "Tekil endpoint (1 warm):" -ForegroundColor DarkGray
$endpointBreakdown = @(
    @{ Label = "ep_bootstrap"; Uri = "$docGw/bootstrap" }
    @{ Label = "ep_browse_root"; Uri = "$docGw/browse" }
    @{ Label = "ep_tree"; Uri = "$docGw/tree" }
    @{ Label = "ep_children"; Uri = "$docGw/children" }
)
if ($FolderId) {
    $fid = [uri]::EscapeDataString($FolderId)
    $endpointBreakdown += @(
        @{ Label = "ep_children_folder"; Uri = "$docGw/children?parentId=$fid" }
        @{ Label = "ep_breadcrumb"; Uri = "$docGw/$fid/breadcrumb" }
        @{ Label = "ep_getById"; Uri = "$docGw/$fid" }
        @{ Label = "ep_permissions"; Uri = "$docGw/$fid/permissions" }
    )
}
if ($MarkdownId) {
    $mid = [uri]::EscapeDataString($MarkdownId)
    $endpointBreakdown += @{ Label = "ep_markdown_content"; Uri = "$docGw/markdown/$mid/content" }
}
$endpointBreakdown += @{ Label = "ep_search"; Uri = "$docGw/search?q=a&skip=0&limit=50" }

$breakdownResults = @()
foreach ($ep in $endpointBreakdown) {
    $breakdownResults += Invoke-Timed -Label $ep.Label -Uri $ep.Uri -Headers $auth
}
Write-Host ("{0,-28} {1,8}" -f "Endpoint", "ms") -ForegroundColor Cyan
foreach ($b in $breakdownResults) {
    $color = if ($b.Success) { if ($b.Ms -gt 1500) { "Yellow" } else { "Gray" } } else { "Red" }
    Write-Host ("{0,-28} {1,8} {2}" -f $b.Label, $b.Ms, $(if ($b.Success) { "" } else { "FAIL" })) -ForegroundColor $color
}
Write-Host ""

$pageResults = @()
foreach ($sc in $scenarios) {
    Write-Host "  $($sc.Id)..." -ForegroundColor DarkGray
    $pageResults += Measure-PageScenario -PageId $sc.Id -Title $sc.Title -RunOnce $sc.Run -WarmCount $WarmIterations
}

Write-Host ""
Write-Host ("{0,-28} {1,10} {2,10} {3,10} {4,6}" -f "Senaryo", "Cold(ms)", "P95(ms)", "Hedef", "OK?") -ForegroundColor Cyan
Write-Host ("-" * 70) -ForegroundColor DarkGray
foreach ($p in $pageResults) {
    $ok = if ($p.Ok) { "YES" } elseif ($p.LastRun.AllSuccess) { "WARN" } else { "FAIL" }
    $color = switch ($ok) { "YES" { "Green" } "WARN" { "Yellow" } default { "Red" } }
    $detail = ($p.LastRun.Results | ForEach-Object { "$($_.Label)=$($_.Ms)ms" }) -join ", "
    Write-Host ("{0,-28} {1,10} {2,10} {3,10} {4,6}" -f $p.PageId, $p.SessionColdWallMs, $p.WarmP95WallMs, $p.TargetWallMs, $ok) -ForegroundColor $color
    if ($detail) { Write-Host "    -> $detail" -ForegroundColor DarkGray }
}
Write-Host ""

$warn = @($pageResults | Where-Object { -not $_.Ok }).Count
Write-Host "Ozet: $($pageResults.Count) senaryo, $warn hedef disi" -ForegroundColor $(if ($warn -eq 0) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "Yorum:" -ForegroundColor DarkGray
Write-Host "  - bootstrap/browse: tek HTTP istegi = tek snapshot (2x DG, showHistory=false)." -ForegroundColor DarkGray
Write-Host "  - Eski tree+children veya 3x browse: coklu snapshot / daha fazla round-trip." -ForegroundColor DarkGray
Write-Host "  - Tarayici: Nuxt layout + auth + /api/documents proxy; DevTools Network waterfall onerilir." -ForegroundColor DarkGray
Write-Host ""

$report = @{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    module = "document_intelligence"
    environment = @{
        gatewayBaseUrl = $GatewayBaseUrl
        documentApiBase = $docGw
        sampleFolderId = $FolderId
        sampleMarkdownId = $MarkdownId
        warmIterations = $WarmIterations
    }
    dgReference = @($dgRef | ForEach-Object {
        @{ label = $_.Label; ms = $_.Ms; success = $_.Success }
    })
    endpointBreakdownWarm1 = @($breakdownResults | ForEach-Object {
        @{ label = $_.Label; ms = $_.Ms; success = $_.Success; uri = $_.Uri }
    })
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
            lastRunDetails = @($_.LastRun.Results | ForEach-Object {
                @{ label = $_.Label; ms = $_.Ms; success = $_.Success; error = $_.Error }
            })
        }
    })
    analysisNotes = @(
        "Optimized: bootstrap/browse use one snapshot per HTTP request (showHistory=false on snapshot queries)."
        "Legacy: parallel tree+children or triple folder APIs multiply snapshot/DG load."
    )
}

if ([string]::IsNullOrEmpty($OutputJson)) {
    $reportDir = Join-Path $scriptDir "../reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $OutputJson = Join-Path $reportDir ("di_pages_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON rapor: $OutputJson" -ForegroundColor Cyan
Write-Host ""

if (@($pageResults | Where-Object { -not $_.LastRun.AllSuccess }).Count -gt 0) { exit 1 }
exit 0
