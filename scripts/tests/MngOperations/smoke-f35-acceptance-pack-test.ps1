# Smoke: F3-5 musteri kabul ve kapanis paketi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F35-$stamp"

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

Write-Host "F3-5 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $packs = @(Invoke-Ops -Path "/job-packs")[0]
    $packList = @($packs)
    $codes = @($packList | ForEach-Object { $_.code })
    Assert-True ($codes -contains "onboarding") "katalog onboarding duruyor"
    Assert-True ($codes -contains "acceptance") "katalog acceptance"
    Assert-True ($codes.Count -ge 7) "katalog en az 7 paket ($($codes.Count))"

    $pack = $packList | Where-Object { $_.code -eq "acceptance" } | Select-Object -First 1
    $expected = Count-Wbs $pack.wbs
    Assert-True ($expected -ge 8) "kabul wbs onizleme=$expected"
    Assert-True (@($pack.kinds) -contains "acceptance") "tur acceptance"
    Assert-True (@($pack.kinds) -contains "closeout") "tur closeout"
    Assert-True (@($pack.kinds) -contains "punchlist") "tur punchlist"
    Assert-True (@($pack.folders) -contains "Kabul") "klasor Kabul"
    Assert-True ((@($pack.folders) -join ",") -match "Kapanış|Kapanis") "klasor Kapanis"

    $empty = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-E"
            name   = "F3-5 empty $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $emptyId = [string]$empty.id
    $preview = @(Invoke-Ops -Path "/projects/$emptyId/packs/acceptance/preview?intent=apply")[0]
    Assert-True ($preview.createCount -eq $expected) "bos proje createCount=$($preview.createCount)"
    Invoke-Ops -Method DELETE -Path "/projects/$emptyId" -ExpectStatus @(204, 200) | Out-Null

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F3-5 smoke $stamp"
            status   = "active"
            packCode = "acceptance"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $wbsCount = @($detail.wbs).Count
    Assert-True ($wbsCount -eq $expected) "WBS sayisi=$wbsCount beklenen=$expected"
    $names = @($detail.wbs | ForEach-Object { $_.name })
    $joined = $names -join ","
    Assert-True ($names -contains "Teslimat listesi") "teslimat listesi WBS"
    Assert-True ($names -contains "Eksik listesi") "eksik listesi WBS"
    Assert-True ($names -contains "Kabul tutanağı" -or $joined -match "Kabul") "kabul tutanagi WBS"
    Assert-True ($names -contains "Proje kapanışı" -or $joined -match "kapanış|kapanis") "proje kapanisi"
    $ms = @($detail.wbs | Where-Object { $_.kind -eq "milestone" })
    Assert-True ($ms.Count -ge 2) "kilometre tasi=$($ms.Count)"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/acceptance?mode=skip")[0]
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

    Write-Host "F3-5 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F3-5 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
