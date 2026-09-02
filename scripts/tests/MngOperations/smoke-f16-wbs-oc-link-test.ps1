# Smoke: F1-6 WBS ↔ OC bağ / ağırlıklı rollup (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F16-$stamp"

function Get-Token {
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
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
        TimeoutSec           = 60
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }
    $result = Invoke-RestMethod @params
    $script:LastStatus = [int]$status
    if ($ExpectStatus -notcontains $script:LastStatus) {
        $err = $null
        if ($result) {
            try { $err = $result | ConvertTo-Json -Compress -Depth 6 } catch { $err = [string]$result }
        }
        throw "HTTP $script:LastStatus $Method $Path : $err"
    }
    return , $result
}

function Invoke-DgList {
    param([string]$Dataset, [string]$Query = "limit=5")
    $uri = "$Gateway/data/api/v1/data/$([Uri]::EscapeDataString($Dataset))?$Query"
    $status = 0
    $result = Invoke-RestMethod -Uri $uri -Method GET -Headers $script:Headers -TimeoutSec 60 `
        -SkipCertificateCheck -SkipHttpErrorCheck -StatusCodeVariable status
    $script:LastDgStatus = [int]$status
    if ([int]$status -ge 400) {
        Write-Host "  DG $Dataset HTTP $status" -ForegroundColor Yellow
        return @()
    }
    if ($null -eq $result) { return @() }
    if ($result -is [System.Array]) { return @($result) }
    $items = @($result.items)
    if (-not $items.Count -and $result.data) { $items = @($result.data) }
    if (-not $items.Count -and $result.__dataId) { $items = @($result) }
    return @($items)
}

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectId = $null

Write-Host "F1-6 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code          = $code
            name          = "F1-6 smoke $stamp"
            status        = "active"
            plannedStart  = "2026-09-01T00:00:00.000Z"
            plannedFinish = "2026-09-30T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($projectId)) "proje olusturuldu id=$projectId"

    $parent = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind          = "summary"
            name          = "Paket"
            plannedStart  = "2026-09-01T00:00:00.000Z"
            plannedFinish = "2026-09-20T00:00:00.000Z"
            weight        = 1
            percentComplete = 0
        } -ExpectStatus @(201, 200))[0]
    $parentId = [string]$parent.id

    $leafA = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            parentId        = $parentId
            kind            = "task"
            name            = "Hazirlik"
            weight          = 1
            percentComplete = 0
        } -ExpectStatus @(201, 200))[0]
    $leafAId = [string]$leafA.id

    $leafB = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            parentId        = $parentId
            kind            = "task"
            name            = "Kurulum"
            weight          = 3
            percentComplete = 100
        } -ExpectStatus @(201, 200))[0]
    $leafBId = [string]$leafB.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($leafBId)) "yaprak WBS olusturuldu"

    $rolled = @(Invoke-Ops -Method POST -Path "/projects/$projectId/rollup")[0]
    $parentRow = @($rolled.wbs) | Where-Object { $_.id -eq $parentId } | Select-Object -First 1
    $parentPct = [double]$parentRow.percentComplete
    Assert-True ([math]::Abs($parentPct - 75) -lt 0.6) "agirlikli rollup parent=$parentPct (beklenen 75)"

    Invoke-Ops -Method POST -Path "/wbs/$leafAId/work-item" -Body @{
        workItemId = "00000000-0000-0000-0000-000000000001"
    } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "workspace yokken bag reddedildi"

    $workspaces = Invoke-DgList -Dataset "op_workspaces" -Query "limit=20&sort=name"
    $workItemsAny = Invoke-DgList -Dataset "op_work_items" -Query "limit=20&sort=-createdAt&expand=false"
    $wi = $workItemsAny | Where-Object { $_.__dataId } | Select-Object -First 1
    $wsId = $null
    if ($wi) {
        $rawWs = $wi.workspaceId
        if ($rawWs -is [string]) { $wsId = $rawWs }
        elseif ($rawWs.__dataId) { $wsId = [string]$rawWs.__dataId }
    }
    if (-not $wsId) {
        $ws = $workspaces | Where-Object { $_.__dataId } | Select-Object -First 1
        if ($ws) { $wsId = [string]$ws.__dataId }
    }
    if (-not $wsId) {
        Write-Host "  SKIP workspace/WI bag (op_workspaces bos)" -ForegroundColor Yellow
    }
    else {
        Invoke-Ops -Method PUT -Path "/projects/$projectId" -Body @{ workspaceId = $wsId } | Out-Null
        Assert-True ($script:LastStatus -eq 200) "proje workspace=$wsId"

        Invoke-Ops -Method POST -Path "/wbs/$parentId/work-item" -Body @{
            workItemId = "does-not-exist-$stamp"
        } -ExpectStatus @(400) | Out-Null
        Assert-True ($script:LastStatus -eq 400) "ozet WBS bag reddedildi"

        Invoke-Ops -Method POST -Path "/wbs/$leafAId/work-item" -Body @{
            workItemId = "does-not-exist-$stamp"
        } -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "olmayan WI 404"

        if (-not $wi) {
            Write-Host "  SKIP WI bag (workspace'te is yok)" -ForegroundColor Yellow
        }
        else {
            $wiId = [string]$wi.__dataId
            $bound = @(Invoke-Ops -Method POST -Path "/wbs/$leafAId/work-item" -Body @{
                    workItemId = $wiId
                })[0]
            Assert-True ($bound.workItemId -eq $wiId) "WI baglandi $($bound.workItemKey)"

            Invoke-Ops -Method POST -Path "/wbs/$leafBId/work-item" -Body @{
                workItemId = $wiId
            } -ExpectStatus @(409) | Out-Null
            Assert-True ($script:LastStatus -eq 409) "ayni WI ikinci WBS 409"

            $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
            $linked = @($detail.wbs) | Where-Object { $_.id -eq $leafAId } | Select-Object -First 1
            Assert-True (-not [string]::IsNullOrWhiteSpace([string]$linked.workItemKey)) "hydrate key=$($linked.workItemKey)"

            $unbound = @(Invoke-Ops -Method DELETE -Path "/wbs/$leafAId/work-item")[0]
            Assert-True ([string]::IsNullOrWhiteSpace([string]$unbound.workItemId)) "bag cozuldu"
        }
    }

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
    }
    else {
        Write-Host "KeepArtifacts: proje birakildi $projectId" -ForegroundColor Yellow
    }

    Write-Host "F1-6 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F1-6 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
