# Smoke: F2-11 portfoy ozeti (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$codeLate = "F211L-$stamp"
$codeQuiet = "F211Q-$stamp"
$codeClosed = "F211C-$stamp"
$pastDue = "2020-01-15T00:00:00.000Z"

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
        [int[]]$ExpectStatus = @(200, 201, 204),
        [int]$TimeoutSec = 60
    )
    $uri = "$Gateway/operations/api/v1$Path"
    $status = 0
    $params = @{
        Uri                  = $uri
        Method               = $Method
        Headers              = $script:Headers
        TimeoutSec           = $TimeoutSec
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

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$lateId = $null
$quietId = $null
$closedId = $null

Write-Host "F2-11 smoke  late=$codeLate  gateway=$Gateway" -ForegroundColor Cyan

try {
    $late = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $codeLate
            name   = "F2-11 delayed $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $lateId = [string]$late.id
    Assert-True ($lateId) "geciken proje id=$lateId"

    Invoke-Ops -Method POST -Path "/projects/$lateId/wbs" -Body @{
            kind          = "task"
            name          = "Geciken gorev"
            plannedFinish = $pastDue
            percentComplete = 10
        } -ExpectStatus @(201, 200) | Out-Null

    $quiet = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $codeQuiet
            name   = "F2-11 quiet $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $quietId = [string]$quiet.id
    Assert-True ($quietId) "sakin proje id=$quietId"

    Invoke-Ops -Method POST -Path "/projects/$quietId/wbs" -Body @{
            kind            = "task"
            name            = "Zamaninda"
            plannedFinish   = "2030-12-31T00:00:00.000Z"
            percentComplete = 0
        } -ExpectStatus @(201, 200) | Out-Null

    $closed = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $codeClosed
            name   = "F2-11 closed $stamp"
            status = "closed"
        } -ExpectStatus @(201, 200))[0]
    $closedId = [string]$closed.id
    Invoke-Ops -Method POST -Path "/projects/$closedId/wbs" -Body @{
            kind            = "task"
            name            = "Kapanmis gecikme"
            plannedFinish   = $pastDue
            percentComplete = 40
        } -ExpectStatus @(201, 200) | Out-Null

    $pack = @(Invoke-Ops -Path "/projects/portfolio" -TimeoutSec 180)[0]
    Assert-True ([int]$pack.projectCount -ge 3) "projectCount>=3 ($($pack.projectCount))"
    Assert-True ($null -ne $pack.totals) "totals var"
    Assert-True ([int]$pack.totals.delayed -ge 1) "totals.delayed>=1"

    $lateRow = @($pack.items) | Where-Object { $_.id -eq $lateId } | Select-Object -First 1
    $quietRow = @($pack.items) | Where-Object { $_.id -eq $quietId } | Select-Object -First 1
    $closedRow = @($pack.items) | Where-Object { $_.id -eq $closedId } | Select-Object -First 1
    Assert-True ($lateRow) "geciken satir portfoyde"
    Assert-True ($quietRow) "sakin satir portfoyde"
    Assert-True ($closedRow) "kapali satir portfoyde"
    Assert-True ([bool]$lateRow.attention) "geciken attention"
    Assert-True (@($lateRow.flags) -contains "delayed") "geciken flags delayed"
    Assert-True ([int]$lateRow.counts.delayed -ge 1) "geciken counts.delayed"
    Assert-True (-not $quietRow.attention) "sakin attention yok"
    Assert-True (-not $closedRow.attention) "kapali attention yok (status=closed)"
    Assert-True ([int]$pack.attentionCount -ge 1) "attentionCount>=1"
    Assert-True ([int]$pack.activeCount -ge 1) "activeCount>=1"
    Assert-True ([int]$pack.draftCount -ge 1) "draftCount>=1"
    Assert-True ([int]$pack.closedCount -ge 1) "closedCount>=1"

    $ids = @($pack.items | ForEach-Object { [string]$_.id })
    $latePos = $ids.IndexOf($lateId)
    $quietPos = $ids.IndexOf($quietId)
    Assert-True ($latePos -ge 0 -and $quietPos -ge 0 -and $latePos -lt $quietPos) "attention satirlar once ($latePos < $quietPos)"

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$lateId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Method DELETE -Path "/projects/$quietId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Method DELETE -Path "/projects/$closedId" -ExpectStatus @(204, 200) | Out-Null
        $lateId = $null
        $quietId = $null
        $closedId = $null
        Assert-True $true "projeler silindi"
    }
    else {
        Write-Host "KeepArtifacts: $lateId / $quietId / $closedId" -ForegroundColor Yellow
    }

    Write-Host "F2-11 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-11 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in @($lateId, $quietId, $closedId)) {
            if ($id) {
                try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
            }
        }
    }
    throw
}
