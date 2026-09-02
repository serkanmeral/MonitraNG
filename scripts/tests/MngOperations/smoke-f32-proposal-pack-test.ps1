# Smoke: F3-2 teklif / sartname yanit paketi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F32-$stamp"

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

Write-Host "F3-2 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $packs = @(Invoke-Ops -Path "/job-packs")[0]
    $packList = @($packs)
    $codes = @($packList | ForEach-Object { $_.code })
    Assert-True ($codes -contains "architecture") "katalog architecture duruyor"
    Assert-True ($codes -contains "proposal") "katalog proposal"

    $pack = $packList | Where-Object { $_.code -eq "proposal" } | Select-Object -First 1
    $expected = Count-Wbs $pack.wbs
    Assert-True ($expected -ge 8) "teklif wbs onizleme=$expected"
    Assert-True (@($pack.kinds) -contains "proposal") "tur proposal"
    Assert-True (@($pack.kinds) -contains "compliance") "tur compliance"
    Assert-True (@($pack.kinds) -contains "specification") "tur specification"
    Assert-True (@($pack.folders) -contains "Teklif") "klasor Teklif"
    Assert-True ((@($pack.folders) -join ",") -match "Sartname|Şartname") "klasor Sartname"

    $empty = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-E"
            name   = "F3-2 empty $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $emptyId = [string]$empty.id
    $preview = @(Invoke-Ops -Path "/projects/$emptyId/packs/proposal/preview?intent=apply")[0]
    Assert-True ($preview.createCount -eq $expected) "bos proje createCount=$($preview.createCount)"
    Invoke-Ops -Method DELETE -Path "/projects/$emptyId" -ExpectStatus @(204, 200) | Out-Null

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F3-2 smoke $stamp"
            status   = "active"
            packCode = "proposal"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $wbsCount = @($detail.wbs).Count
    Assert-True ($wbsCount -eq $expected) "WBS sayisi=$wbsCount beklenen=$expected"
    $names = @($detail.wbs | ForEach-Object { $_.name })
    $joined = $names -join ","
    Assert-True ($names -contains "Şartname" -or $joined -match "Sartname") "Sartname WBS"
    Assert-True ($names -contains "Uyum matrisi") "uyum matrisi WBS"
    Assert-True ($names -contains "Teklif metni") "teklif metni WBS"
    Assert-True ($names -contains "Teklif teslimi") "teklif teslimi"
    $ms = @($detail.wbs | Where-Object { $_.kind -eq "milestone" })
    Assert-True ($ms.Count -ge 2) "kilometre tasi=$($ms.Count)"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/proposal?mode=skip")[0]
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

    Write-Host "F3-2 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F3-2 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
