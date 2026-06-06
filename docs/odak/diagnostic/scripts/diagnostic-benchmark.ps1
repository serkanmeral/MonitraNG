# Backend response time benchmark — Odak (MngOperations odakli, P1 servisler dahil)
#
# Usage (repo root):
#   .\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1
#   .\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1 -CompareDirect
#   .\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1 -WarmIterations 7 -OutputJson .\reports\bench.json
#
# On kosullar:
#   - Odak stack ayakta (mngoperations, gateway, keeper, datagateway)
#   - get-operationcore-token.ps1 kimlik bilgileri gecerli
#   - operationcore-demo-seed.json mevcut (seed-operation-core-demo.ps1)
#
# Metrikler:
#   - session_cold: benchmark oturumunda endpoint'e ilk istek
#   - warm: sonraki N tekrar — medyan, P95, min, max
#   Hedef (plan): warm P95 <= 3000 ms, session_cold <= 4000 ms (runtime endpoint'leri)

param(
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$MoDirectBaseUrl = "http://192.168.20.20:5086",
    [string]$DgDirectBaseUrl = "http://192.168.20.20:5010",
    [string]$KeeperDirectBaseUrl = "http://192.168.20.20:5001",
    [string]$WorkspaceId = "",
    [string]$BoardId = "",
    [string]$DashboardId = "",
    [string]$WorkItemId = "",
    [int]$WarmIterations = 5,
    [switch]$CompareDirect,
    [switch]$SkipMutating,
    [string]$OutputJson = "",
    [int]$TargetWarmP95Ms = 3000,
    [int]$TargetColdMs = 4000
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path $scriptDir "../../operationcore/scripts"
$seedFile = Join-Path $ocScriptDir "operationcore-demo-seed.json"
$dataPath = "/data/api/v1/data"

$MoGatewayBaseUrl = "$($GatewayBaseUrl.TrimEnd('/'))/operations"

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 1) { return $sorted[0] }
    $rank = [math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    if ($rank -lt 0) { $rank = 0 }
    if ($rank -ge $sorted.Count) { $rank = $sorted.Count - 1 }
    return $sorted[$rank]
}

function Get-OcDataId {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value.Trim() }
    if ($Value.PSObject.Properties['__dataId']) { return [string]$Value.__dataId }
    return $null
}

function Invoke-TimedRequest {
    param(
        [string]$Label,
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $params = @{
            Uri             = $Uri
            Method          = $Method
            Headers         = $Headers
            UseBasicParsing = $true
            ErrorAction     = "Stop"
        }
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @params
        $sw.Stop()

        return [PSCustomObject]@{
            Label      = $Label
            Uri        = $Uri
            Method     = $Method
            Success    = $true
            StatusCode = [int]$response.StatusCode
            Ms         = [long]$sw.ElapsedMilliseconds
            Bytes      = if ($null -ne $response.RawContentLength -and $response.RawContentLength -ge 0) {
                [long]$response.RawContentLength
            } else {
                [long]$response.Content.Length
            }
            Error      = $null
        }
    }
    catch {
        $sw.Stop()
        $status = $null
        if ($_.Exception.Response) {
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }
        }
        return [PSCustomObject]@{
            Label      = $Label
            Uri        = $Uri
            Method     = $Method
            Success    = $false
            StatusCode = $status
            Ms         = [long]$sw.ElapsedMilliseconds
            Bytes      = 0
            Error      = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        }
    }
}

function Measure-Endpoint {
    param(
        [string]$Name,
        [string]$BaseUrl,
        [string]$Path,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [int]$WarmCount = 5,
        [string]$Category = "runtime"
    )

    $base = $BaseUrl.TrimEnd('/')
    $uri = "$base$Path"
    $runs = @()

    $cold = Invoke-TimedRequest -Label $Name -Uri $uri -Method $Method -Headers $Headers -Body $Body
    $runs += $cold

    for ($i = 1; $i -le $WarmCount; $i++) {
        $runs += Invoke-TimedRequest -Label $Name -Uri $uri -Method $Method -Headers $Headers -Body $Body
    }

    $warmMs = @($runs | Select-Object -Skip 1 | Where-Object { $_.Success } | ForEach-Object { [double]$_.Ms })
    $warmP95 = Get-Percentile -Values $warmMs -Percentile 95
    $warmMedian = if ($warmMs.Count -gt 0) { (Get-Percentile -Values $warmMs -Percentile 50) } else { $null }

    $targetMs = if ($Category -eq "reference") { 100 } else { $TargetWarmP95Ms }
    $coldTargetMs = if ($Category -eq "reference") { 200 } else { $TargetColdMs }

    $warmOk = $null -ne $warmP95 -and $warmP95 -le $targetMs
    $coldOk = $cold.Success -and $cold.Ms -le $coldTargetMs

    return [PSCustomObject]@{
        Name         = $Name
        Category     = $Category
        BaseUrl      = $base
        Path         = $Path
        Method       = $Method
        SessionColdMs = $cold.Ms
        SessionColdOk = $coldOk
        WarmMedianMs = $warmMedian
        WarmP95Ms    = $warmP95
        WarmMinMs    = if ($warmMs.Count -gt 0) { ($warmMs | Measure-Object -Minimum).Minimum } else { $null }
        WarmMaxMs    = if ($warmMs.Count -gt 0) { ($warmMs | Measure-Object -Maximum).Maximum } else { $null }
        WarmOk       = $warmOk
        StatusCode   = $cold.StatusCode
        Success      = $cold.Success
        Bytes        = $cold.Bytes
        Error        = $cold.Error
        Runs         = $runs
    }
}

function Write-BenchmarkTable {
    param([object[]]$Results)

    Write-Host ""
    Write-Host ("{0,-36} {1,10} {2,10} {3,10} {4,6} {5}" -f "Endpoint", "Cold(ms)", "P95(ms)", "Med(ms)", "OK?", "Cat") -ForegroundColor Cyan
    Write-Host ("-" * 86) -ForegroundColor DarkGray

    foreach ($r in $Results) {
        $ok = if ($r.Success -and $r.WarmOk -and $r.SessionColdOk) { "YES" }
              elseif ($r.Success) { "WARN" }
              else { "FAIL" }
        $color = switch ($ok) {
            "YES" { "Green" }
            "WARN" { "Yellow" }
            default { "Red" }
        }
        $cold = if ($null -ne $r.SessionColdMs) { "{0,10}" -f $r.SessionColdMs } else { "{0,10}" -f "-" }
        $p95 = if ($null -ne $r.WarmP95Ms) { "{0,10}" -f [math]::Round($r.WarmP95Ms, 0) } else { "{0,10}" -f "-" }
        $med = if ($null -ne $r.WarmMedianMs) { "{0,10}" -f [math]::Round($r.WarmMedianMs, 0) } else { "{0,10}" -f "-" }
        Write-Host ("{0,-36} {1} {2} {3} {4,6} {5}" -f $r.Name, $cold, $p95, $med, $ok, $r.Category) -ForegroundColor $color
        if (-not $r.Success -and $r.Error) {
            Write-Host ("  -> {0}" -f ($r.Error -replace "`n", " ")) -ForegroundColor Red
        }
    }
    Write-Host ""
}

# --- Main ---

Write-Host ""
Write-Host "Backend Diagnostic Benchmark (Odak)" -ForegroundColor Cyan
Write-Host "  Gateway : $GatewayBaseUrl" -ForegroundColor Gray
Write-Host "  MO (gw) : $MoGatewayBaseUrl" -ForegroundColor Gray
if ($CompareDirect) {
    Write-Host "  MO dir  : $MoDirectBaseUrl" -ForegroundColor Gray
    Write-Host "  DG dir  : $DgDirectBaseUrl" -ForegroundColor Gray
    Write-Host "  Keeper  : $KeeperDirectBaseUrl" -ForegroundColor Gray
}
Write-Host "  Warm N  : $WarmIterations | Hedef warm P95 <= ${TargetWarmP95Ms}ms, session cold <= ${TargetColdMs}ms" -ForegroundColor Gray
Write-Host ""

$isProdGateway = $GatewayBaseUrl -match "192\.168\.20\.8"
$loadTokenScript = Join-Path $ocScriptDir $(if ($isProdGateway) { "load-operationcore-token-prod.ps1" } else { "load-operationcore-token.ps1" })
if (-not (Test-Path $loadTokenScript)) { throw "Token script bulunamadi: $loadTokenScript" }

if ($isProdGateway) {
    $prodSeed = Join-Path $ocScriptDir "operationcore-helpdesk-prod-seed.json"
    if (([string]::IsNullOrEmpty($WorkspaceId) -or [string]::IsNullOrEmpty($BoardId)) -and (Test-Path $prodSeed)) {
        $seedFile = $prodSeed
    }
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$authHeaders = @{
    Authorization = "Bearer $token"
}
$jsonHeaders = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
$moParams = @{ Headers = $jsonHeaders; ErrorAction = "Stop" }

if (Test-Path $seedFile) {
    $seed = Get-Content $seedFile -Raw | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($WorkspaceId)) { $WorkspaceId = $seed.workspaceId }
    if ([string]::IsNullOrEmpty($BoardId)) { $BoardId = $seed.boardId }
    if ([string]::IsNullOrEmpty($DashboardId)) { $DashboardId = $seed.dashboardId }
}

if ([string]::IsNullOrEmpty($WorkspaceId)) { throw "WorkspaceId gerekli (param veya operationcore-demo-seed.json)." }
if ([string]::IsNullOrEmpty($BoardId)) { throw "BoardId gerekli (param veya operationcore-demo-seed.json)." }

Write-Host "Seed: workspace=$WorkspaceId board=$BoardId" -ForegroundColor Gray

if ([string]::IsNullOrEmpty($WorkItemId)) {
    Write-Host "Work item cozumleniyor (board list)..." -ForegroundColor Yellow
    $listBody = @{ skip = 0; take = 1 } | ConvertTo-Json -Compress
    $list = Invoke-RestMethod -Uri "$MoGatewayBaseUrl/api/v1/runtime/boards/$BoardId/list" -Method POST -Body $listBody @moParams
    if ($list.items -and $list.items.Count -ge 1) {
        $first = $list.items[0]
        $WorkItemId = Get-OcDataId $first
        if ([string]::IsNullOrEmpty($WorkItemId) -and $first.PSObject.Properties['id']) {
            $WorkItemId = [string]$first.id
        }
    }
}

if ([string]::IsNullOrEmpty($WorkItemId) -and -not $SkipMutating) {
    Write-Host "Board bos — smoke work item olusturuluyor..." -ForegroundColor Yellow
    $typeId = $null
    if (Test-Path $seedFile) { $typeId = (Get-Content $seedFile -Raw | ConvertFrom-Json).typeId }
    if ([string]::IsNullOrEmpty($typeId)) {
        $types = Invoke-RestMethod -Uri "$GatewayBaseUrl$dataPath/op_work_item_types?limit=5" @moParams
        if ($types -is [System.Array] -and $types.Count -ge 1) {
            $typeId = Get-OcDataId $types[0]
        }
    }
    if ([string]::IsNullOrEmpty($typeId)) { throw "Work item yok ve typeId cozulemedi; -WorkItemId verin veya seed calistirin." }
    $createBody = @{
        workspaceId = $WorkspaceId
        boardId     = $BoardId
        typeId      = $typeId
        title       = "Diagnostic bench $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        description = "diagnostic-benchmark.ps1 otomatik"
    } | ConvertTo-Json -Compress
    $created = Invoke-RestMethod -Uri "$MoGatewayBaseUrl/api/v1/work-items" -Method POST -Body $createBody @moParams
    $WorkItemId = $created.workItem.id
    if ([string]::IsNullOrEmpty($WorkItemId)) { $WorkItemId = Get-OcDataId $created.workItem.dataId }
}

if ([string]::IsNullOrEmpty($WorkItemId)) {
    throw "WorkItemId gerekli; board listesi bos veya -SkipMutating aktif."
}

Write-Host "Work item: $WorkItemId" -ForegroundColor Gray
Write-Host ""

$boardListBody = (@{ skip = 0; take = 50 } | ConvertTo-Json -Compress)
$formCreatePath = "/api/v1/runtime/work-items/form?workspaceId=$WorkspaceId&mode=create"
$formEditPath = "/api/v1/runtime/work-items/$WorkItemId/form?mode=edit"
$queryBody = (@{
    dataset    = "op_work_items"
    parameters = @{
        workspaceId = $WorkspaceId
        stateId     = if ($seed.states.open) { $seed.states.open } else { "" }
    }
    skip       = 0
    take       = 10
} | ConvertTo-Json -Depth 5 -Compress)

$endpointDefs = @(
    @{ Name = "mo_health_live"; Category = "reference"; Path = "/api/v1/health/live"; Method = "GET"; Headers = @{}; Body = $null }
    @{ Name = "mo_version"; Category = "reference"; Path = "/api/v1/version"; Method = "GET"; Headers = @{}; Body = $null }
    @{ Name = "runtime_board"; Category = "runtime"; Path = "/api/v1/runtime/boards/$BoardId"; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_board_list"; Category = "runtime"; Path = "/api/v1/runtime/boards/$BoardId/list"; Method = "POST"; Headers = $jsonHeaders; Body = $boardListBody }
    @{ Name = "runtime_profile"; Category = "runtime"; Path = "/api/v1/runtime/work-items/$WorkItemId/profile"; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_profile_view"; Category = "runtime"; Path = "/api/v1/runtime/work-items/$WorkItemId/profile-view"; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_timeline"; Category = "runtime"; Path = "/api/v1/runtime/work-items/$WorkItemId/timeline?skip=0&take=50"; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_state_segments"; Category = "runtime"; Path = "/api/v1/runtime/work-items/$WorkItemId/state-segments"; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_form_create"; Category = "runtime"; Path = $formCreatePath; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_form_edit"; Category = "runtime"; Path = $formEditPath; Method = "GET"; Headers = $authHeaders; Body = $null }
    @{ Name = "runtime_query_execute"; Category = "runtime"; Path = "/api/v1/runtime/queries/wi_by_workspace_and_state/execute"; Method = "POST"; Headers = $jsonHeaders; Body = $queryBody }
)

if (-not [string]::IsNullOrEmpty($DashboardId)) {
    $endpointDefs += @{
        Name = "runtime_dashboard"; Category = "runtime"; Path = "/api/v1/runtime/dashboards/$DashboardId"
        Method = "GET"; Headers = $authHeaders; Body = $null
    }
}

$p1Defs = @(
    @{ Name = "dg_health_live"; Category = "downstream"; Base = $DgDirectBaseUrl; Path = "/api/v1/health/live" }
    @{ Name = "keeper_version"; Category = "downstream"; Base = $KeeperDirectBaseUrl; Path = "/api/version/short" }
)

$allResults = @()
$startedAt = (Get-Date).ToUniversalTime().ToString("o")

Write-Host "MngOperations (gateway) olculuyor..." -ForegroundColor Yellow
foreach ($def in $endpointDefs) {
    Write-Host "  $($def.Name)..." -ForegroundColor DarkGray
    $allResults += Measure-Endpoint `
        -Name $def.Name `
        -BaseUrl $MoGatewayBaseUrl `
        -Path $def.Path `
        -Method $def.Method `
        -Headers $def.Headers `
        -Body $def.Body `
        -WarmCount $WarmIterations `
        -Category $def.Category
}

Write-Host "P1 downstream (direct) olculuyor..." -ForegroundColor Yellow
foreach ($def in $p1Defs) {
    Write-Host "  $($def.Name)..." -ForegroundColor DarkGray
    $allResults += Measure-Endpoint `
        -Name $def.Name `
        -BaseUrl $def.Base `
        -Path $def.Path `
        -Method "GET" `
        -Headers $authHeaders `
        -WarmCount $WarmIterations `
        -Category $def.Category
}

$directCompare = @()
if ($CompareDirect) {
    Write-Host "Gateway vs direct karsilastirmasi (MO runtime)..." -ForegroundColor Yellow
    $compareNames = @("runtime_board_list", "runtime_profile")
    foreach ($def in $endpointDefs | Where-Object { $compareNames -contains $_.Name }) {
        $direct = Measure-Endpoint `
            -Name ($def.Name + "_direct") `
            -BaseUrl $MoDirectBaseUrl `
            -Path $def.Path `
            -Method $def.Method `
            -Headers $def.Headers `
            -Body $def.Body `
            -WarmCount $WarmIterations `
            -Category "compare"
        $directCompare += $direct
        $gw = $allResults | Where-Object { $_.Name -eq $def.Name } | Select-Object -First 1
        if ($gw) {
            $delta = if ($null -ne $gw.WarmP95Ms -and $null -ne $direct.WarmP95Ms) {
                [math]::Round($direct.WarmP95Ms - $gw.WarmP95Ms, 0)
            } else { $null }
            Write-Host ("  {0}: gw P95={1}ms direct P95={2}ms delta={3}ms" -f $def.Name, $gw.WarmP95Ms, $direct.WarmP95Ms, $delta) -ForegroundColor Gray
        }
    }
    $allResults += $directCompare
}

Write-BenchmarkTable -Results $allResults

$failCount = @($allResults | Where-Object { -not $_.Success }).Count
$warnCount = @($allResults | Where-Object { $_.Success -and (-not $_.WarmOk -or -not $_.SessionColdOk) }).Count

Write-Host "Ozet: $($allResults.Count) endpoint, $failCount hata, $warnCount hedef disi (WARN)" -ForegroundColor $(if ($failCount -gt 0) { "Red" } elseif ($warnCount -gt 0) { "Yellow" } else { "Green" })
Write-Host ""
Write-Host "Not: 'session cold' servis restart sonrasi gercek cold degildir." -ForegroundColor DarkGray
Write-Host "     Gercek cold icin: docker restart mngoperations && scripti hemen calistirin." -ForegroundColor DarkGray
Write-Host "     OC_PERF (dgCalls/dgMs): MngOperationsSettings__PerfDiagnostics=true + docker logs mngoperations | grep OC_PERF" -ForegroundColor DarkGray
Write-Host ""

$report = [PSCustomObject]@{
    generatedAtUtc = $startedAt
    environment    = @{
        gatewayBaseUrl    = $GatewayBaseUrl
        moGatewayBaseUrl  = $MoGatewayBaseUrl
        moDirectBaseUrl   = if ($CompareDirect) { $MoDirectBaseUrl } else { $null }
        workspaceId       = $WorkspaceId
        boardId           = $BoardId
        workItemId        = $WorkItemId
        dashboardId       = $DashboardId
        warmIterations    = $WarmIterations
        targetWarmP95Ms   = $TargetWarmP95Ms
        targetColdMs      = $TargetColdMs
    }
    summary = @{
        total    = $allResults.Count
        failed   = $failCount
        warn     = $warnCount
    }
    endpoints = @($allResults | ForEach-Object {
        @{
            name          = $_.Name
            category      = $_.Category
            baseUrl       = $_.BaseUrl
            path          = $_.Path
            method        = $_.Method
            sessionColdMs = $_.SessionColdMs
            warmMedianMs  = $_.WarmMedianMs
            warmP95Ms     = $_.WarmP95Ms
            warmMinMs     = $_.WarmMinMs
            warmMaxMs     = $_.WarmMaxMs
            warmOk        = $_.WarmOk
            sessionColdOk = $_.SessionColdOk
            success       = $_.Success
            statusCode    = $_.StatusCode
            bytes         = $_.Bytes
            error         = $_.Error
        }
    })
}

if ([string]::IsNullOrEmpty($OutputJson)) {
    $reportDir = Join-Path $scriptDir "../reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $OutputJson = Join-Path $reportDir ("benchmark_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON rapor: $OutputJson" -ForegroundColor Cyan
Write-Host ""

if ($failCount -gt 0) { exit 1 }
exit 0
