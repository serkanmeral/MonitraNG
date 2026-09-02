# Smoke: F1-9 PMO/kalite is paketi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F19-$stamp"

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

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

function Count-Wbs($nodes) {
    $n = 0
    foreach ($node in @($nodes)) {
        $n++
        if ($node.children) { $n += Count-Wbs $node.children }
    }
    return $n
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectId = $null

Write-Host "F1-9 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $packs = @(Invoke-Ops -Path "/job-packs")[0]
    $packList = @($packs)
    $codes = @($packList | ForEach-Object { $_.code })
    Assert-True ($codes -contains "pmo") "katalog pmo"
    Assert-True ($codes -contains "quality") "katalog quality"
    $pmo = $packList | Where-Object { $_.code -eq "pmo" } | Select-Object -First 1
    $expected = Count-Wbs $pmo.wbs
    Assert-True ($expected -ge 8) "pmo wbs onizleme=$expected"

    Invoke-Ops -Method POST -Path "/projects" -Body @{
        code     = "$code-BAD"
        name     = "bad pack"
        packCode = "no-such-pack"
    } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "bilinmeyen paket 400"

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F1-9 smoke $stamp"
            status   = "active"
            packCode = "pmo"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($projectId)) "proje olusturuldu"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $wbsCount = @($detail.wbs).Count
    Assert-True ($wbsCount -eq $expected) "WBS sayisi=$wbsCount beklenen=$expected"
    $names = @($detail.wbs | ForEach-Object { $_.name })
    Assert-True ($names -contains "Kick-off") "Kick-off var"
    Assert-True ($names -contains "Baseline") "Baseline var"
    $ms = @($detail.wbs | Where-Object { $_.kind -eq "milestone" })
    Assert-True ($ms.Count -ge 3) "kilometre tasi=$($ms.Count)"

    $q = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = "$code-Q"
            name     = "F1-9 quality $stamp"
            packCode = "quality"
        } -ExpectStatus @(201, 200))[0]
    $qid = [string]$q.id
    $qDetail = @(Invoke-Ops -Path "/projects/$qid")[0]
    $qNames = @($qDetail.wbs | ForEach-Object { $_.name })
    Assert-True ($qNames -contains "Doküman kontrolü" -or $qNames -contains "Dokuman kontrolu" -or ($qNames -join ",") -match "kontrol") "kalite WBS"
    Invoke-Ops -Method DELETE -Path "/projects/$qid" -ExpectStatus @(204, 200) | Out-Null

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
    }
    else {
        Write-Host "KeepArtifacts: proje birakildi $projectId" -ForegroundColor Yellow
    }

    Write-Host "F1-9 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F1-9 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
