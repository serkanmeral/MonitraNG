# Smoke: F1-7 iz / durum paketi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F17-$stamp"

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
    if ([int]$status -ge 400) { return @() }
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

Write-Host "F1-7 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code          = $code
            name          = "F1-7 smoke $stamp"
            status        = "active"
            plannedStart  = "2026-07-01T00:00:00.000Z"
            plannedFinish = "2026-08-15T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($projectId)) "proje olusturuldu id=$projectId"

    $late = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind            = "task"
            name            = "Geciken gorev"
            plannedStart    = "2026-07-01T00:00:00.000Z"
            plannedFinish   = "2026-08-01T00:00:00.000Z"
            percentComplete = 0
        } -ExpectStatus @(201, 200))[0]
    $lateId = [string]$late.id

    $ms = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind            = "milestone"
            name            = "Riskli KM"
            plannedStart    = "2026-08-20T00:00:00.000Z"
            plannedFinish   = "2026-08-20T00:00:00.000Z"
            percentComplete = 0
        } -ExpectStatus @(201, 200))[0]
    $msId = [string]$ms.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($msId)) "kilometre tasi id=$msId"

    Invoke-Ops -Method POST -Path "/projects/$projectId/baseline" -Body @{ note = "f17" } | Out-Null
    Invoke-Ops -Method PUT -Path "/wbs/$lateId" -Body @{
        plannedFinish = "2026-08-25T00:00:00.000Z"
    } | Out-Null

    $pack = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ($pack.projectId -eq $projectId) "status pack proje id"
    Assert-True ([int]$pack.counts.delayed -ge 1) "geciken=$($pack.counts.delayed)"
    Assert-True ([int]$pack.counts.milestoneAtRisk -ge 1) "km risk=$($pack.counts.milestoneAtRisk)"
    Assert-True ([int]$pack.counts.drifted -ge 1) "sapma=$($pack.counts.drifted)"
    Assert-True ([int]$pack.counts.unboundLeaf -ge 2) "bagsiz yaprak=$($pack.counts.unboundLeaf)"

    $lateRow = @($pack.items) | Where-Object { $_.wbsId -eq $lateId } | Select-Object -First 1
    Assert-True ($lateRow.flags -contains "delayed") "gorev delayed flag"
    Assert-True ($lateRow.flags -contains "drifted") "gorev drifted flag"
    Assert-True ($lateRow.flags -contains "unbound") "gorev unbound flag"

    $msRow = @($pack.items) | Where-Object { $_.wbsId -eq $msId } | Select-Object -First 1
    Assert-True ($msRow.flags -contains "milestoneAtRisk") "km milestoneAtRisk flag"

    $workItemsAny = Invoke-DgList -Dataset "op_work_items" -Query "limit=20&sort=-createdAt&expand=false"
    $wi = $workItemsAny | Where-Object { $_.__dataId } | Select-Object -First 1
    if (-not $wi) {
        Write-Host "  SKIP WI/kanit (op_work_items bos)" -ForegroundColor Yellow
    }
    else {
        $rawWs = $wi.workspaceId
        $wsId = $null
        if ($rawWs -is [string]) { $wsId = $rawWs }
        elseif ($rawWs.__dataId) { $wsId = [string]$rawWs.__dataId }
        if ($wsId) {
            Invoke-Ops -Method PUT -Path "/projects/$projectId" -Body @{ workspaceId = $wsId } | Out-Null
            $bound = @(Invoke-Ops -Method POST -Path "/wbs/$lateId/work-item" -Body @{
                    workItemId = [string]$wi.__dataId
                })[0]
            Assert-True ($bound.workItemId -eq [string]$wi.__dataId) "WI baglandi $($bound.workItemKey)"

            $after = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
            $linked = @($after.items) | Where-Object { $_.wbsId -eq $lateId } | Select-Object -First 1
            Assert-True ($linked.workItemKey) "hydrate key=$($linked.workItemKey)"
            Assert-True ($linked.flags -contains "missingEvidence") "bagli is kanit yok"
            Invoke-Ops -Method DELETE -Path "/wbs/$lateId/work-item" | Out-Null
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

    Write-Host "F1-7 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F1-7 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
