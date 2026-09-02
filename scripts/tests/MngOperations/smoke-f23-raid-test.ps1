# Smoke: F2-3 RAID (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F23-$stamp"

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

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectId = $null
$otherId = $null

Write-Host "F2-3 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-3 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Entegrasyon"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-3 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/raid" -Body @{
            kind  = "mystery"
            title = "X"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "bilinmeyen tur 400"

    $risk = @(Invoke-Ops -Method POST -Path "/projects/$projectId/raid" -Body @{
            kind        = "risk"
            title       = "Tedarik gecikmesi"
            impact      = "high"
            likelihood  = "high"
            response    = "mitigate"
            wbsIds      = @($wbsId)
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($risk.kind -eq "risk") "risk olusturuldu"
    Assert-True ([int]$risk.score -eq 9) "skor=9"
    Assert-True ($risk.elevated) "elevated"
    Assert-True ($risk.open) "risk acik"

    $issue = @(Invoke-Ops -Method POST -Path "/projects/$projectId/raid" -Body @{
            kind   = "issue"
            title  = "Test ortamı yok"
            status = "open"
            impact = "medium"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($issue.kind -eq "issue") "sorun olusturuldu"

    $assumption = @(Invoke-Ops -Method POST -Path "/projects/$projectId/raid" -Body @{
            kind   = "assumption"
            title  = "Musteri API verecek"
        } -ExpectStatus @(201, 200))[0]
    $dep = @(Invoke-Ops -Method POST -Path "/projects/$projectId/raid" -Body @{
            kind   = "dependency"
            title  = "Elektrik kesintisi penceresi"
            status = "waiting"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($dep.status -eq "waiting") "bagimlilik waiting"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack.counts.openRisk -eq 1) "openRisk=1"
    Assert-True ([int]$pack.counts.openIssue -eq 1) "openIssue=1"
    Assert-True ([int]$pack.counts.openAssumption -eq 1) "openAssumption=1"
    Assert-True ([int]$pack.counts.openDependency -eq 1) "openDependency=1"
    $row = @($pack.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "openRisk") "WBS openRisk bayragi"
    Assert-True ((@($row.raidItems) | ForEach-Object { $_.title }) -contains "Tedarik gecikmesi") "satir RAID"

    Invoke-Ops -Method POST -Path "/projects/$projectId/raid" -Body @{
        kind   = "risk"
        title  = "Yabanci"
        wbsIds = @($foreignWbsId)
    } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    $closed = @(Invoke-Ops -Method PUT -Path "/raid/$($issue.id)" -Body @{ status = "closed" })[0]
    Assert-True ($closed.status -eq "closed") "sorun kapandi"
    Assert-True ($closed.closedBy) "closedBy dolu"
    Assert-True (-not $closed.open) "open=false"

    $pack2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack2.counts.openIssue -eq 0) "kapaninca openIssue=0"

    $validated = @(Invoke-Ops -Method PUT -Path "/raid/$($assumption.id)" -Body @{ status = "validated" })[0]
    Assert-True ($validated.status -eq "validated") "varsayim dogrulandi"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.raidItems).Count -eq 4) "GET detail raidItems=4"

    Invoke-Ops -Method DELETE -Path "/raid/$($dep.id)" -ExpectStatus @(204, 200) | Out-Null
    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftDep = @($after.raidItems) | Where-Object { $_.id -eq $dep.id }
    Assert-True ($leftDep.Count -eq 0) "bagimlilik silindi"

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Method DELETE -Path "/projects/$otherId" -ExpectStatus @(204, 200, 404) | Out-Null
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
        $otherId = $null
    }
    else {
        Write-Host "KeepArtifacts: projeler birakildi $projectId / $otherId" -ForegroundColor Yellow
    }

    Write-Host "F2-3 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-3 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        if ($projectId) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        if ($otherId) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$otherId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
    }
    throw
}
