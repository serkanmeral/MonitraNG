# Smoke: F1-8 karar kaydi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F18-$stamp"

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

Write-Host "F1-8 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code          = $code
            name          = "F1-8 smoke $stamp"
            status        = "active"
            plannedStart  = "2026-07-01T00:00:00.000Z"
            plannedFinish = "2026-09-01T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($projectId)) "proje olusturuldu id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Kapsam gorevi"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($wbsId)) "wbs id=$wbsId"

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F1-8 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreignWbs = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci WBS"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreignWbs.id

    $general = @(Invoke-Ops -Method POST -Path "/projects/$projectId/decisions" -Body @{
            title = "Genel karar"
            body  = "Toplanti notu"
            kind  = "general"
            status = "open"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($general.title -eq "Genel karar") "genel karar olusturuldu"
    Assert-True ($general.kind -eq "general") "kind=general"
    Assert-True ($general.status -eq "open") "status=open"

    $scope = @(Invoke-Ops -Method POST -Path "/projects/$projectId/decisions" -Body @{
            title  = "Kapsam degisikligi"
            kind   = "scopeChange"
            status = "open"
            wbsIds = @($wbsId)
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($scope.kind -eq "scopeChange") "scopeChange olusturuldu"
    $scopeWbs = @($scope.wbsIds)
    Assert-True ($scopeWbs -contains $wbsId) "wbsIds icinde hedef WBS"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $decisionCount = @($detail.decisions).Count
    Assert-True ($decisionCount -ge 2) "GET detail decisions=$decisionCount"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack.counts.openScopeChange -ge 1) "openScopeChange=$($pack.counts.openScopeChange)"
    $row = @($pack.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    $rowTitles = @($row.decisions) | ForEach-Object { $_.title }
    Assert-True ($rowTitles -contains "Kapsam degisikligi") "status satirinda kapsam karari"

    Invoke-Ops -Method POST -Path "/projects/$projectId/decisions" -Body @{
        title  = "Yabanci WBS"
        kind   = "scopeChange"
        wbsIds = @($foreignWbsId)
    } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    $updated = @(Invoke-Ops -Method PUT -Path "/decisions/$($scope.id)" -Body @{
            status = "accepted"
            title  = "Kapsam degisikligi (kabul)"
        })[0]
    Assert-True ($updated.status -eq "accepted") "status accepted"
    Assert-True ($updated.title -eq "Kapsam degisikligi (kabul)") "baslik guncellendi"

    $pack2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack2.counts.openScopeChange -eq 0) "kabul sonrasi openScopeChange=0"

    Invoke-Ops -Method DELETE -Path "/decisions/$($general.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterDelete = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $remaining = @($afterDelete.decisions) | Where-Object { $_.id -eq $general.id }
    Assert-True ($remaining.Count -eq 0) "genel karar silindi"

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

    Write-Host "F1-8 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F1-8 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
