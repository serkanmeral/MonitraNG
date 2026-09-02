# Smoke: F3-4 onboarding / yetkinlik paketi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F34-$stamp"

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

Write-Host "F3-4 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $packs = @(Invoke-Ops -Path "/job-packs")[0]
    $packList = @($packs)
    $codes = @($packList | ForEach-Object { $_.code })
    Assert-True ($codes -contains "eco") "katalog eco duruyor"
    Assert-True ($codes -contains "onboarding") "katalog onboarding"

    $pack = $packList | Where-Object { $_.code -eq "onboarding" } | Select-Object -First 1
    $expected = Count-Wbs $pack.wbs
    Assert-True ($expected -ge 8) "onboarding wbs onizleme=$expected"
    Assert-True (@($pack.kinds) -contains "onboarding") "tur onboarding"
    Assert-True (@($pack.kinds) -contains "competency") "tur competency"
    Assert-True ((@($pack.folders) -join ",") -match "Yetkinlik") "klasor Yetkinlik"
    Assert-True ((@($pack.folders) -join ",") -match "Karşılama|Karsilama") "klasor Karsilama"

    $empty = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-E"
            name   = "F3-4 empty $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $emptyId = [string]$empty.id
    $preview = @(Invoke-Ops -Path "/projects/$emptyId/packs/onboarding/preview?intent=apply")[0]
    Assert-True ($preview.createCount -eq $expected) "bos proje createCount=$($preview.createCount)"
    Invoke-Ops -Method DELETE -Path "/projects/$emptyId" -ExpectStatus @(204, 200) | Out-Null

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F3-4 smoke $stamp"
            status   = "active"
            packCode = "onboarding"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $wbsCount = @($detail.wbs).Count
    Assert-True ($wbsCount -eq $expected) "WBS sayisi=$wbsCount beklenen=$expected"
    $names = @($detail.wbs | ForEach-Object { $_.name })
    $joined = $names -join ","
    Assert-True ($names -contains "Yetkinlik matrisi") "yetkinlik matrisi WBS"
    Assert-True ($joined -match "Oryantasyon") "oryantasyon WBS"
    Assert-True ($names -contains "Göreve hazır" -or $joined -match "hazır|hazir") "goreve hazir"
    $ms = @($detail.wbs | Where-Object { $_.kind -eq "milestone" })
    Assert-True ($ms.Count -ge 1) "kilometre tasi=$($ms.Count)"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/onboarding?mode=skip")[0]
    Assert-True (($reapply.created -as [int]) -eq 0) "reapply created=0"
    Assert-True ($reapply.skipped -ge $expected) "reapply skipped=$($reapply.skipped)"

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
    }
    else {
        Write-Host "KeepArtifacts: proje birakildi $projectId" -ForegroundColor Yellow
    }

    Write-Host "F3-4 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F3-4 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
