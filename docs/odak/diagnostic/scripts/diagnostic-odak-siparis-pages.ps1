# Odak Siparis hub — sayfa bazli API yuku simulasyonu
#
# UI'nin packages/index, packages/[id] ve OdakSiparisLinesPanel akisini DG uzerinden olcer.
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\diagnostic\scripts\diagnostic-odak-siparis-pages.ps1
#   .\docs\odak\diagnostic\scripts\diagnostic-odak-siparis-pages.ps1 -OutputJson .\docs\odak\diagnostic\reports\oc_pages_odak_siparis.json

param(
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [int]$ListPageSize = 20,
    [int]$ClientFilterLimit = 500,
    [int]$LineStatsChunkSize = 8,
    [int]$WarmIterations = 2,
    [string]$OutputJson = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path $scriptDir "../../operationcore/scripts"
$dgBase = "$($GatewayBaseUrl.TrimEnd('/'))/data/api/v1/data"

$PackagesDataset = "odak_is_paketleri"
$LinesDataset = "odak_siparis_kalemleri"
$CustomersDataset = "odak_musteriler"

$pageTargets = @{
    packages_list_open       = 1500
    packages_list_tab_switch = 1200
    packages_list_adv_filter = 8000
    package_detail_summary   = 1200
    package_detail_with_lines = 1800
}

function Invoke-Timed {
    param(
        [string]$Label,
        [string]$Uri,
        [hashtable]$Headers
    )
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-WebRequest -Uri $Uri -Method GET -Headers $Headers -UseBasicParsing -ErrorAction Stop
        $sw.Stop()
        $len = $null
        if ($r.Headers["Content-Length"]) { $len = [int]$r.Headers["Content-Length"] }
        return [PSCustomObject]@{
            Label = $Label; Uri = $Uri; Success = $true
            Ms = [long]$sw.ElapsedMilliseconds
            StatusCode = [int]$r.StatusCode
            ContentLength = $len
            BodyBytes = $r.RawContentLength
            Error = $null
        }
    }
    catch {
        $sw.Stop()
        return [PSCustomObject]@{
            Label = $Label; Uri = $Uri; Success = $false
            Ms = [long]$sw.ElapsedMilliseconds; StatusCode = $null
            ContentLength = $null; BodyBytes = $null
            Error = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        }
    }
}

function Get-ListItems {
    param($Response)
    if ($null -eq $Response) { return @() }
    if ($Response -is [System.Array]) { return ,@($Response) }
    if ($Response.items) { return ,@($Response.items) }
    return ,@($Response)
}

function Get-ItemId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

function Build-LineStatsChunks {
    param([string[]]$PackageIds, [int]$ChunkSize)
    $chunks = @()
    for ($i = 0; $i -lt $PackageIds.Count; $i += $ChunkSize) {
        $slice = @($PackageIds[$i..([Math]::Min($i + $ChunkSize - 1, $PackageIds.Count - 1))])
        $filter = ($slice | ForEach-Object { "parentPackageId eq '$_'" }) -join " or "
        $chunks += ,@{
            Ids = $slice
            Filter = $filter
            Uri = "${dgBase}/${LinesDataset}?filter=" + [uri]::EscapeDataString($filter) + "&limit=2000"
        }
    }
    return $chunks
}

function Invoke-LineStatsSequential {
    param([string[]]$PackageIds, [hashtable]$Headers, [int]$ChunkSize)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $chunks = Build-LineStatsChunks -PackageIds $PackageIds -ChunkSize $ChunkSize
    $calls = @()
    foreach ($c in $chunks) {
        $calls += Invoke-Timed -Label "lines_stats_chunk" -Uri $c.Uri -Headers $Headers
    }
    $sw.Stop()
    return [PSCustomObject]@{
        Mode = "sequential"
        ChunkCount = $chunks.Count
        PackageCount = $PackageIds.Count
        WallMs = $sw.ElapsedMilliseconds
        SumMs = ($calls | Measure-Object -Property Ms -Sum).Sum
        Calls = $calls
        AllSuccess = -not ($calls | Where-Object { -not $_.Success })
    }
}

function Invoke-LineStatsParallel {
    param([string[]]$PackageIds, [string]$Token, [int]$ChunkSize)
    $chunks = Build-LineStatsChunks -PackageIds $PackageIds -ChunkSize $ChunkSize
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $pool = [runspacefactory]::CreateRunspacePool(1, [Math]::Min(8, [Math]::Max(1, $chunks.Count)))
    $pool.Open()
    $handles = @()
    $sb = {
        param($Uri, $Token)
        $inner = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $h = @{ Authorization = "Bearer $Token" }
            Invoke-WebRequest -Uri $Uri -Method GET -Headers $h -UseBasicParsing -ErrorAction Stop | Out-Null
            $inner.Stop()
            return @{ Success = $true; Ms = $inner.ElapsedMilliseconds; Error = $null }
        }
        catch {
            $inner.Stop()
            return @{ Success = $false; Ms = $inner.ElapsedMilliseconds; Error = $_.Exception.Message }
        }
    }
    foreach ($c in $chunks) {
        $ps = [powershell]::Create().AddScript($sb).AddArgument($c.Uri).AddArgument($Token)
        $ps.RunspacePool = $pool
        $handles += [PSCustomObject]@{ Ps = $ps; Async = $ps.BeginInvoke() }
    }
    $calls = @()
    foreach ($h in $handles) {
        $out = $h.Ps.EndInvoke($h.Async)
        $h.Ps.Dispose()
        $calls += [PSCustomObject]@{ Label = "lines_stats_chunk_parallel"; Success = $out.Success; Ms = $out.Ms; Error = $out.Error }
    }
    $pool.Close(); $pool.Dispose()
    $sw.Stop()
    return [PSCustomObject]@{
        Mode = "parallel"
        ChunkCount = $chunks.Count
        PackageCount = $PackageIds.Count
        WallMs = $sw.ElapsedMilliseconds
        SumMs = ($calls | Measure-Object -Property Ms -Sum).Sum
        Calls = $calls
        AllSuccess = -not ($calls | Where-Object { -not $_.Success })
    }
}

function Measure-PageScenario {
    param(
        [string]$PageId,
        [string]$Title,
        [scriptblock]$RunOnce,
        [int]$WarmCount = 2
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
    $warmP95 = if ($warmWalls.Count -gt 0) {
        $sorted = $warmWalls | Sort-Object
        $idx = [Math]::Min($sorted.Count - 1, [Math]::Ceiling(0.95 * $sorted.Count) - 1)
        if ($idx -lt 0) { $idx = 0 }
        $sorted[$idx]
    } else { $null }
    $ok = $null -ne $targetMs -and $null -ne $warmP95 -and $warmP95 -le $targetMs -and $details[-1].AllSuccess
    [PSCustomObject]@{
        PageId = $PageId
        Title = $Title
        TargetWallMs = $targetMs
        SessionColdWallMs = $walls[0]
        WarmP95WallMs = $warmP95
        WarmMedianWallMs = if ($warmWalls.Count -gt 0) {
            $s = $warmWalls | Sort-Object
            $s[[Math]::Floor(($s.Count - 1) / 2)]
        } else { $null }
        Ok = $ok
        LastRun = $details[-1]
    }
}

# --- Main ---

Write-Host ""
Write-Host "Odak Siparis — Sayfa API yuku (Odak test)" -ForegroundColor Cyan
Write-Host "  Gateway: $GatewayBaseUrl" -ForegroundColor Gray
Write-Host ""

$loadToken = Join-Path $ocScriptDir "load-operationcore-token.ps1"
$token = & $loadToken
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi. Once get-operationcore-token.ps1 calistirin." }

$auth = @{ Authorization = "Bearer $token" }

# Ornek paket id
$samplePackageId = $null
$pkgList = Invoke-RestMethod -Uri "${dgBase}/${PackagesDataset}?filter=status:eq:open&sort=packageNo:desc&limit=$ListPageSize&skip=0" -Headers $auth
$pkgItems = Get-ListItems $pkgList
if ($pkgItems.Count -ge 1) {
    $samplePackageId = Get-ItemId $pkgItems[0]
}
if ([string]::IsNullOrEmpty($samplePackageId)) {
    throw "Ornek acik paket bulunamadi ($PackagesDataset)."
}

Write-Host "Ornek paket: $samplePackageId | Liste sayfa boyutu: $ListPageSize" -ForegroundColor Gray
Write-Host ""

function Invoke-PackagesListScenario {
    param([int]$Limit, [string]$LabelPrefix = "list")
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $calls = @()

    $calls += Invoke-Timed -Label "${LabelPrefix}_customers" -Uri "${dgBase}/${CustomersDataset}?sort=unvan:asc&limit=3000" -Headers $auth
    $calls += Invoke-Timed -Label "${LabelPrefix}_packages" -Uri "${dgBase}/${PackagesDataset}?filter=status:eq:open&sort=packageNo:desc&limit=$Limit&skip=0" -Headers $auth

    $ids = @()
    foreach ($row in $pkgItems) {
        $id = Get-ItemId $row
        if ($id) { $ids += $id }
    }
    if ($Limit -ne $ListPageSize) {
        $big = Invoke-RestMethod -Uri "${dgBase}/${PackagesDataset}?filter=status:eq:open&sort=packageNo:desc&limit=$Limit&skip=0" -Headers $auth
        $ids = @()
        foreach ($row in (Get-ListItems $big)) {
            $id = Get-ItemId $row
            if ($id) { $ids += $id }
        }
    }

    $stats = Invoke-LineStatsSequential -PackageIds $ids -Headers $auth -ChunkSize $LineStatsChunkSize
    $sw.Stop()

    return [PSCustomObject]@{
        WallMs = $sw.ElapsedMilliseconds
        SumMs = ($calls | Measure-Object -Property Ms -Sum).Sum + $stats.SumMs
        ApiCallCount = $calls.Count + $stats.ChunkCount
        LineStatsChunks = $stats.ChunkCount
        PackageCount = $ids.Count
        Calls = @($calls + $stats.Calls)
        LineStats = $stats
        AllSuccess = (-not ($calls | Where-Object { -not $_.Success })) -and $stats.AllSuccess
    }
}

$scenarios = @(
    @{
        Id = "packages_list_open"
        Title = "Is Paketleri listesi — Acik sekmesi (UI: customers + 20 paket + line stats)"
        Run = { Invoke-PackagesListScenario -Limit $ListPageSize -LabelPrefix "open" }
    }
    @{
        Id = "packages_list_tab_switch"
        Title = "Liste — sekme degisimi / sayfa yenileme (ayni API deseni)"
        Run = { Invoke-PackagesListScenario -Limit $ListPageSize -LabelPrefix "tab" }
    }
    @{
        Id = "packages_list_adv_filter"
        Title = "Liste — musteri/kalem aramasi (UI limit=500 + line stats x63 chunk)"
        Run = { Invoke-PackagesListScenario -Limit $ClientFilterLimit -LabelPrefix "adv" }
    }
    @{
        Id = "package_detail_summary"
        Title = "Paket detay — Ozet sekmesi (UI yine customers + expand + lines panel mount)"
        Run = {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $calls = @()
            $calls += Invoke-Timed -Label "detail_customers" -Uri "${dgBase}/${CustomersDataset}?sort=unvan:asc&limit=3000" -Headers $auth
            $calls += Invoke-Timed -Label "detail_package_expand" -Uri "${dgBase}/${PackagesDataset}/${samplePackageId}?expand=true" -Headers $auth
            $lineUri = "${dgBase}/${LinesDataset}?filter=" + [uri]::EscapeDataString("parentPackageId eq '$samplePackageId'") + "&sort=lineNo:asc&limit=500"
            $calls += Invoke-Timed -Label "detail_lines_mount" -Uri $lineUri -Headers $auth
            $sw.Stop()
            [PSCustomObject]@{
                WallMs = $sw.ElapsedMilliseconds
                SumMs = ($calls | Measure-Object -Property Ms -Sum).Sum
                ApiCallCount = $calls.Count
                Calls = $calls
                AllSuccess = -not ($calls | Where-Object { -not $_.Success })
                Note = "OdakSiparisLinesPanel v-show ile mount oldugundan lines API ozet sekmesinde de tetiklenir"
            }
        }
    }
    @{
        Id = "package_detail_with_lines"
        Title = "Paket detay — Kalemler sekmesi (customers + expand + lines)"
        Run = {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $calls = @()
            $calls += Invoke-Timed -Label "lines_customers" -Uri "${dgBase}/${CustomersDataset}?sort=unvan:asc&limit=3000" -Headers $auth
            $calls += Invoke-Timed -Label "lines_package_expand" -Uri "${dgBase}/${PackagesDataset}/${samplePackageId}?expand=true" -Headers $auth
            $lineUri = "${dgBase}/${LinesDataset}?filter=" + [uri]::EscapeDataString("parentPackageId eq '$samplePackageId'") + "&sort=lineNo:asc&limit=500"
            $calls += Invoke-Timed -Label "lines_grid" -Uri $lineUri -Headers $auth
            $sw.Stop()
            [PSCustomObject]@{
                WallMs = $sw.ElapsedMilliseconds
                SumMs = ($calls | Measure-Object -Property Ms -Sum).Sum
                ApiCallCount = $calls.Count
                Calls = $calls
                AllSuccess = -not ($calls | Where-Object { -not $_.Success })
            }
        }
    }
)

$pageResults = @()
foreach ($sc in $scenarios) {
    Write-Host "  $($sc.Id)..." -ForegroundColor DarkGray
    $pageResults += Measure-PageScenario -PageId $sc.Id -Title $sc.Title -RunOnce $sc.Run -WarmCount $WarmIterations
}

# Line stats sequential vs parallel (20 paket)
$listIds = @()
foreach ($row in $pkgItems) {
    $id = Get-ItemId $row
    if ($id) { $listIds += $id }
}
$seqStats = Invoke-LineStatsSequential -PackageIds $listIds -Headers $auth -ChunkSize $LineStatsChunkSize
$parStats = Invoke-LineStatsParallel -PackageIds $listIds -Token $token -ChunkSize $LineStatsChunkSize

# Tek endpoint baz
Write-Host ""
Write-Host "Tek endpoint (cold):" -ForegroundColor Cyan
$singleCalls = @(
    Invoke-Timed -Label "customers_3000" -Uri "${dgBase}/${CustomersDataset}?sort=unvan:asc&limit=3000" -Headers $auth
    Invoke-Timed -Label "packages_20_open" -Uri "${dgBase}/${PackagesDataset}?filter=status:eq:open&sort=packageNo:desc&limit=20&skip=0" -Headers $auth
    Invoke-Timed -Label "package_expand" -Uri "${dgBase}/${PackagesDataset}/${samplePackageId}?expand=true" -Headers $auth
    Invoke-Timed -Label "lines_single_parent" -Uri "${dgBase}/${LinesDataset}?filter=parentPackageId%20eq%20'$samplePackageId'&sort=lineNo:asc&limit=500" -Headers $auth
)
foreach ($c in $singleCalls) {
    $bytes = if ($c.BodyBytes) { "$($c.BodyBytes) B" } else { "-" }
    $st = if ($c.Success) { "OK" } else { "FAIL" }
    Write-Host ("  {0,-22} {1,6} ms  {2}" -f $c.Label, $c.Ms, $bytes) -ForegroundColor $(if ($c.Success) { "Gray" } else { "Red" })
}

Write-Host ""
Write-Host ("{0,-28} {1,10} {2,10} {3,10} {4,6}" -f "Sayfa", "Cold(ms)", "P95(ms)", "Hedef", "OK?") -ForegroundColor Cyan
Write-Host ("-" * 70) -ForegroundColor DarkGray
foreach ($p in $pageResults) {
    $ok = if ($p.Ok) { "YES" } elseif ($p.LastRun.AllSuccess) { "WARN" } else { "FAIL" }
    $color = switch ($ok) { "YES" { "Green" } "WARN" { "Yellow" } default { "Red" } }
    Write-Host ("{0,-28} {1,10} {2,10} {3,10} {4,6}" -f $p.PageId, $p.SessionColdWallMs, $p.WarmP95WallMs, $p.TargetWallMs, $ok) -ForegroundColor $color
    $lr = $p.LastRun
    if ($lr.ApiCallCount) {
        Write-Host ("    -> {0} API cagrisi, wall={1} ms, sum={2} ms" -f $lr.ApiCallCount, $lr.WallMs, $lr.SumMs) -ForegroundColor DarkGray
    }
}
Write-Host ""
Write-Host "Line stats karsilastirma ($ListPageSize paket, chunk=$LineStatsChunkSize):" -ForegroundColor Cyan
Write-Host ("  Sequential: wall={0} ms, sum={1} ms, chunks={2}" -f $seqStats.WallMs, $seqStats.SumMs, $seqStats.ChunkCount) -ForegroundColor Gray
Write-Host ("  Parallel:   wall={0} ms, sum={1} ms, chunks={2}" -f $parStats.WallMs, $parStats.SumMs, $parStats.ChunkCount) -ForegroundColor Gray
Write-Host ""

$findings = @(
    [ordered]@{
        id = "F1"
        severity = "high"
        title = "Line stats N+1 chunk (sequential)"
        detail = "fetchPackageLineStatsMap her 8 paket icin ayri DG list cagrisi yapar; 20 satir = 3 sequential, 500 satir = 63 sequential."
    }
    [ordered]@{
        id = "F2"
        severity = "high"
        title = "Musteri haritasi her sayfa yuklemesinde"
        detail = "fetchCustomerLabelMap limit=3000 tek seferde cekilir; liste + detay her acilista tekrarlanir (oturum cache yok)."
    }
    [ordered]@{
        id = "F3"
        severity = "medium"
        title = "Detayda Kalemler paneli erken mount"
        detail = "OdakSiparisLinesPanel v-show ile her zaman mount; ozet sekmesinde bile lines API tetiklenir."
    }
    [ordered]@{
        id = "F4"
        severity = "medium"
        title = "Gelismis arama limit=500"
        detail = "hasClientFilter aktifken 500 paket + line stats chunk patlamasi."
    }
    [ordered]@{
        id = "F5"
        severity = "low"
        title = "Dataset alanlari kullanilmiyor"
        detail = "odak_is_paketleri.lineCount/partCount mevcut; PO/proje icin line stats zorunlu degil (lazy veya arama aninda)."
    }
)

$report = @{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    module = "odak-siparis"
    environment = @{
        gatewayBaseUrl = $GatewayBaseUrl
        listPageSize = $ListPageSize
        clientFilterLimit = $ClientFilterLimit
        lineStatsChunkSize = $LineStatsChunkSize
        samplePackageId = $samplePackageId
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
            lastRunApiCallCount = $_.LastRun.ApiCallCount
            lastRunWallMs = $_.LastRun.WallMs
            lastRunSumMs = $_.LastRun.SumMs
            lastRunLineStatsChunks = $_.LastRun.LineStatsChunks
            note = $_.LastRun.Note
        }
    })
    singleEndpointMs = @($singleCalls | ForEach-Object {
        @{ label = $_.Label; ms = $_.Ms; success = $_.Success; bodyBytes = $_.BodyBytes }
    })
    lineStatsComparison = @{
        sequentialWallMs = $seqStats.WallMs
        sequentialSumMs = $seqStats.SumMs
        sequentialChunks = $seqStats.ChunkCount
        parallelWallMs = $parStats.WallMs
        parallelSumMs = $parStats.SumMs
        parallelChunks = $parStats.ChunkCount
    }
    findings = $findings
}

if ([string]::IsNullOrEmpty($OutputJson)) {
    $reportDir = Join-Path $scriptDir "../reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $OutputJson = Join-Path $reportDir ("oc_pages_odak_siparis_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
$report | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON rapor: $OutputJson" -ForegroundColor Cyan
Write-Host ""

$warn = @($pageResults | Where-Object { -not $_.Ok }).Count
Write-Host "Ozet: $($pageResults.Count) senaryo, $warn hedef disi" -ForegroundColor $(if ($warn -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

if (@($pageResults | Where-Object { -not $_.LastRun.AllSuccess }).Count -gt 0) { exit 1 }
exit 0
