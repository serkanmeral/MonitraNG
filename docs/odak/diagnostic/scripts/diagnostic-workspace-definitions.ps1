# Workspace Definitions — DG/API response time (Odak)
#
# Kullanıcı şikayeti: workspace tanımlama ekranında zamanlanmış görevler listesi 20-30 sn.
# Bu script scheduled sekmesinin backend çağrılarını ve sayfa açılışında tetiklenen
# "eager tab" fırtınasını simüle eder (UI kodundan türetilmiş URL listesi).
#
# Usage (repo root):
#   .\docs\odak\diagnostic\scripts\diagnostic-workspace-definitions.ps1
#   .\docs\odak\diagnostic\scripts\diagnostic-workspace-definitions.ps1 -OutputJson .\reports\ws-def.json

param(
    [string]$GatewayBaseUrl = "http://192.168.20.20:5040",
    [string]$WorkspaceId = "",
    [int]$WarmIterations = 3,
    [string]$OutputJson = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$ocScriptDir = Join-Path $scriptDir "../../operationcore/scripts"
$seedFile = Join-Path $ocScriptDir "operationcore-demo-seed.json"
$dgBase = "$($GatewayBaseUrl.TrimEnd('/'))/data/api/v1/data"

function Invoke-TimedGet {
    param([string]$Label, [string]$Uri, [hashtable]$Headers)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-WebRequest -Uri $Uri -Method GET -Headers $Headers -UseBasicParsing -ErrorAction Stop
        $sw.Stop()
        return [PSCustomObject]@{
            Label = $Label; Uri = $Uri; Success = $true
            Ms = [long]$sw.ElapsedMilliseconds
            Bytes = [long]$r.RawContentLength; StatusCode = [int]$r.StatusCode; Error = $null
        }
    }
    catch {
        $sw.Stop()
        return [PSCustomObject]@{
            Label = $Label; Uri = $Uri; Success = $false
            Ms = [long]$sw.ElapsedMilliseconds; Bytes = 0; StatusCode = $null
            Error = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        }
    }
}

function Invoke-SequentialBatch {
    param([array]$Jobs, [hashtable]$Headers)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $results = @()
    foreach ($j in $Jobs) {
        $results += Invoke-TimedGet -Label $j.Label -Uri $j.Uri -Headers $Headers
    }
    $sw.Stop()
    return [PSCustomObject]@{
        WallMs = [long]$sw.ElapsedMilliseconds
        SumMs = ($results | Measure-Object -Property Ms -Sum).Sum
        Results = $results
    }
}

function Invoke-ParallelBatchRunspaces {
    param([array]$Jobs, [string]$Token)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $poolSize = [Math]::Max(1, [Math]::Min(16, $Jobs.Count))
    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $poolSize)
    $runspacePool.Open()
    $handles = @()

    $scriptBlock = {
        param($Uri, $Token)
        $inner = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $h = @{ Authorization = "Bearer $Token" }
            $r = Invoke-WebRequest -Uri $Uri -Method GET -Headers $h -UseBasicParsing -ErrorAction Stop
            $inner.Stop()
            return @{ Success = $true; Ms = $inner.ElapsedMilliseconds; Bytes = $r.RawContentLength; Error = $null }
        }
        catch {
            $inner.Stop()
            return @{ Success = $false; Ms = $inner.ElapsedMilliseconds; Bytes = 0; Error = $_.Exception.Message }
        }
    }

    foreach ($j in $Jobs) {
        $ps = [powershell]::Create().AddScript($scriptBlock).AddArgument($j.Uri).AddArgument($Token)
        $ps.RunspacePool = $runspacePool
        $handles += [PSCustomObject]@{ Job = $j; Handle = $ps.BeginInvoke(); Ps = $ps }
    }

    $out = @()
    foreach ($h in $handles) {
        [void]$h.Handle.AsyncWaitHandle.WaitOne()
        $data = $h.Ps.EndInvoke($h.Handle)
        $h.Ps.Dispose()
        $out += [PSCustomObject]@{
            Label = $h.Job.Label; Uri = $h.Job.Uri
            Success = $data.Success; Ms = [long]$data.Ms; Bytes = [long]$data.Bytes; Error = $data.Error
        }
    }
    $runspacePool.Close()
    $runspacePool.Dispose()
    $sw.Stop()
    return [PSCustomObject]@{ WallMs = [long]$sw.ElapsedMilliseconds; Results = $out }
}

Write-Host ""
Write-Host "Workspace Definitions Diagnostic (Odak DG)" -ForegroundColor Cyan
Write-Host "  Gateway/DG: $dgBase" -ForegroundColor Gray
Write-Host ""

$loadTokenScript = Join-Path $ocScriptDir "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }
$headers = @{ Authorization = "Bearer $token" }

if ([string]::IsNullOrEmpty($WorkspaceId) -and (Test-Path $seedFile)) {
    $WorkspaceId = (Get-Content $seedFile -Raw | ConvertFrom-Json).workspaceId
}
if ([string]::IsNullOrEmpty($WorkspaceId)) { throw "WorkspaceId gerekli." }
Write-Host "Workspace: $WorkspaceId" -ForegroundColor Gray
Write-Host ""

$ws = $WorkspaceId
$encWs = [uri]::EscapeDataString($ws)

# --- Scheduled tab (OcWorkspaceDefinitionsScheduledWorkItemsTab.loadAll) ---
$scheduledJobs = @(
    @{ Label = "schedules"; Uri = "$dgBase/op_work_item_schedules?filter=workspaceId:eq:$encWs&sort=name:asc&limit=200" }
    @{ Label = "boards"; Uri = "$dgBase/op_boards?filter=workspaceId:eq:$encWs&sort=name:asc&limit=200" }
    @{ Label = "types_global"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "types_scoped"; Uri = "$dgBase/op_work_item_types?filter=workspaceId:eq:$encWs&sort=category:asc,sortOrder:asc,name:asc&limit=200" }
    @{ Label = "workspace"; Uri = "$dgBase/op_workspaces/$encWs" }
    @{ Label = "priorities_global"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
)

Write-Host "1) Scheduled tab — ardisik (6 istek)..." -ForegroundColor Yellow
$schedSeq = Invoke-SequentialBatch -Jobs $scheduledJobs -Headers $headers
Write-Host ("   wall={0}ms sum={1}ms" -f $schedSeq.WallMs, $schedSeq.SumMs) -ForegroundColor $(if ($schedSeq.WallMs -le 3000) { "Green" } elseif ($schedSeq.WallMs -le 8000) { "Yellow" } else { "Red" })
foreach ($r in $schedSeq.Results) {
    $c = if ($r.Success) { "Gray" } else { "Red" }
    Write-Host ("     {0,-22} {1,6} ms  {2}" -f $r.Label, $r.Ms, $(if ($r.Success) { "OK" } else { "FAIL" })) -ForegroundColor $c
}

Write-Host "2) Scheduled tab — paralel (Promise.all sim.)..." -ForegroundColor Yellow
$schedPar = Invoke-ParallelBatchRunspaces -Jobs $scheduledJobs -Token $token
Write-Host ("   wall={0}ms (max single={1}ms)" -f $schedPar.WallMs, ($schedPar.Results | Measure-Object -Property Ms -Maximum).Maximum) -ForegroundColor $(if ($schedPar.WallMs -le 3000) { "Green" } elseif ($schedPar.WallMs -le 8000) { "Yellow" } else { "Red" })

# --- Eager page storm (index.vue eager + values eager) — benzersiz URL'ler, tek seferde paralel ---
# Kaynak: workspace-definitions/*.vue loadAll/watch immediate:true + v-tabs-window eager
$eagerJobs = @(
    @{ Label = "page_workspaces"; Uri = "$dgBase/op_workspaces?sort=name:asc&limit=200" }
    @{ Label = "general_workspace"; Uri = "$dgBase/op_workspaces/$encWs" }
    # tags
    @{ Label = "tags"; Uri = "$dgBase/op_tags?filter=workspaceId:eq:$encWs&sort=name:asc&limit=200" }
    # flows
    @{ Label = "flows"; Uri = "$dgBase/op_state_flows?filter=workspaceId:eq:$encWs&sort=name:asc&limit=100" }
    # forms tab
    @{ Label = "forms"; Uri = "$dgBase/op_forms?filter=workspaceId:eq:$encWs&sort=name:asc&limit=100" }
    @{ Label = "forms_types_global"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "forms_types_scoped"; Uri = "$dgBase/op_work_item_types?filter=workspaceId:eq:$encWs&sort=category:asc,sortOrder:asc,name:asc&limit=200" }
    @{ Label = "forms_states"; Uri = "$dgBase/op_states?sort=sortOrder:asc,name:asc&limit=500" }
    @{ Label = "forms_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    @{ Label = "forms_pool_fields"; Uri = "$dgBase/op_fields?filter=workspaceId:eq:$encWs&sort=key:asc&limit=500" }
    @{ Label = "forms_boards"; Uri = "$dgBase/op_boards?filter=workspaceId:eq:$encWs&sort=name:asc&limit=200" }
    # boards tab
    @{ Label = "boards_list"; Uri = "$dgBase/op_boards?filter=workspaceId:eq:$encWs&sort=name:asc&limit=200" }
    @{ Label = "boards_types_global"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "boards_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    # dashboards
    @{ Label = "dashboards"; Uri = "$dgBase/op_dashboards?filter=workspaceId:eq:$encWs&sort=name:asc&limit=100" }
    @{ Label = "dashboards_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    # rules
    @{ Label = "rules"; Uri = "$dgBase/op_rules?filter=workspaceId:eq:$encWs&sort=priority:asc,name:asc&limit=200" }
    @{ Label = "rules_types_global"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "rules_states"; Uri = "$dgBase/op_states?sort=sortOrder:asc,name:asc&limit=500" }
    @{ Label = "rules_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    # scheduled (duplicate labels ok — measures contention)
    @{ Label = "sched_schedules"; Uri = "$dgBase/op_work_item_schedules?filter=workspaceId:eq:$encWs&sort=name:asc&limit=200" }
    @{ Label = "sched_types_global"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "sched_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    # sla
    @{ Label = "sla_policies"; Uri = "$dgBase/op_sla_policies?filter=workspaceId:eq:$encWs&sort=name:asc&limit=100" }
    @{ Label = "sla_types"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "sla_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    # values sub-tabs (eager)
    @{ Label = "values_types_global"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
    @{ Label = "values_types_scoped"; Uri = "$dgBase/op_work_item_types?filter=workspaceId:eq:$encWs&sort=category:asc,sortOrder:asc,name:asc&limit=200" }
    @{ Label = "values_states"; Uri = "$dgBase/op_states?sort=sortOrder:asc,name:asc&limit=500" }
    @{ Label = "values_priorities"; Uri = "$dgBase/op_priorities?sort=sortOrder:asc,level:asc,name:asc&limit=500" }
    @{ Label = "values_fields_global"; Uri = "$dgBase/op_fields?sort=key:asc&limit=500" }
    @{ Label = "values_fields_scoped"; Uri = "$dgBase/op_fields?filter=workspaceId:eq:$encWs&sort=key:asc&limit=500" }
    # policies (FieldPolicyExplorer)
    @{ Label = "policies_pool"; Uri = "$dgBase/op_fields?filter=workspaceId:eq:$encWs&sort=key:asc&limit=500" }
    @{ Label = "policies_states"; Uri = "$dgBase/op_states?sort=sortOrder:asc,name:asc&limit=500" }
    @{ Label = "policies_types"; Uri = "$dgBase/op_work_item_types?sort=category:asc,sortOrder:asc,name:asc&limit=500" }
)

Write-Host ""
Write-Host ("3) Eager page storm — {0} paralel DG istegi (UI'daki tekrarli cagri modeli)..." -f $eagerJobs.Count) -ForegroundColor Yellow
$eagerPar = Invoke-ParallelBatchRunspaces -Jobs $eagerJobs -Token $token
$failEager = @($eagerPar.Results | Where-Object { -not $_.Success }).Count
$maxSingle = ($eagerPar.Results | Measure-Object -Property Ms -Maximum).Maximum
$p95Single = ($eagerPar.Results | Sort-Object Ms | Select-Object -Index ([Math]::Ceiling(0.95 * $eagerPar.Results.Count) - 1)).Ms
Write-Host ("   wall={0}ms | max={1}ms | P95={2}ms | fail={3}" -f $eagerPar.WallMs, $maxSingle, $p95Single, $failEager) -ForegroundColor $(if ($eagerPar.WallMs -le 8000) { "Yellow" } else { "Red" })

# Tekrar sayımı (aynı URL pattern kaç tab çağırıyor)
Write-Host ""
Write-Host "4) Tekrarli global katalog cagrilari (UI mimarisi — backend degil, istek sayisi):" -ForegroundColor Yellow
$dupPatterns = @(
    @{ Pattern = "op_states?sort"; ApproxTabs = 6; Note = "forms, rules, values, policies, boards(states), flows" }
    @{ Pattern = "op_priorities?sort"; ApproxTabs = 8; Note = "forms, rules, boards, scheduled, dashboards, sla, values, policies" }
    @{ Pattern = "op_work_item_types?sort"; ApproxTabs = 9; Note = "forms, rules, boards, scheduled, sla, values, policies + scoped" }
    @{ Pattern = "op_workspaces/$encWs"; ApproxTabs = 10; Note = "her catalog filtre tab'i ocGetWorkspace cagiriyor" }
)
foreach ($d in $dupPatterns) {
    Write-Host ("   ~{0}x  {1}  ({2})" -f $d.ApproxTabs, $d.Pattern, $d.Note) -ForegroundColor Gray
}

Write-Host ""
Write-Host "Ozet:" -ForegroundColor Cyan
Write-Host ("  Scheduled tab (paralel):     {0,6} ms  hedef <=3000ms" -f $schedPar.WallMs)
Write-Host ("  Eager page storm (paralel):  {0,6} ms  ({1} eszamanli istek)" -f $eagerPar.WallMs, $eagerJobs.Count)
Write-Host ""
Write-Host "Not: UI v-tabs-window eager=true → tum sekmeler sayfa acilisinda yuklenir." -ForegroundColor DarkGray
Write-Host "     Scheduled sekmesi tek basina yavas degilse bile sayfa storm'u 20-30sn hissi uretir." -ForegroundColor DarkGray
Write-Host ""

$report = [PSCustomObject]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    workspaceId = $WorkspaceId
    gatewayBaseUrl = $GatewayBaseUrl
    scheduledTab = @{
        sequentialWallMs = $schedSeq.WallMs
        sequentialSumMs = $schedSeq.SumMs
        parallelWallMs = $schedPar.WallMs
        calls = @($schedSeq.Results | ForEach-Object { @{ label = $_.Label; ms = $_.Ms; success = $_.Success } })
    }
    eagerPageStorm = @{
        parallelWallMs = $eagerPar.WallMs
        requestCount = $eagerJobs.Count
        maxSingleMs = $maxSingle
        p95SingleMs = $p95Single
        failed = $failEager
    }
    duplicateCatalogPatterns = $dupPatterns
}

if ([string]::IsNullOrEmpty($OutputJson)) {
    $reportDir = Join-Path $scriptDir "../reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $OutputJson = Join-Path $reportDir ("ws_definitions_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "JSON: $OutputJson" -ForegroundColor Cyan
Write-Host ""
