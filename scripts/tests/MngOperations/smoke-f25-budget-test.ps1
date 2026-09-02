# Smoke: F2-5 is paketi butcesi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F25-$stamp"

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

Write-Host "F2-5 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-5 smoke $stamp"
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
            name   = "F2-5 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $wbsId
            plannedAmount = 100
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "adsiz kalem 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            name          = "Iscilik"
            plannedAmount = 100
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "WBS'siz kalem 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $wbsId
            name          = "Iscilik"
            category      = "mystery"
            plannedAmount = 100
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "bilinmeyen tur 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $wbsId
            name          = "Iscilik"
            plannedAmount = -1
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "negatif tutar 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $foreignWbsId
            name          = "Iscilik"
            plannedAmount = 100
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    $over = @(Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $wbsId
            name          = "Iscilik"
            category      = "labor"
            plannedAmount = 100
            actualAmount  = 150
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($over.over) "100/150 asım"
    Assert-True ([double]$over.variance -eq -50) "kalan=-50"
    Assert-True ($over.currency -eq "TRY") "varsayilan TRY"

    Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $wbsId
            name          = "Iscilik"
            category      = "labor"
            plannedAmount = 10
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni kalem+WBS 409"

    $ok = @(Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $wbsId
            name          = "Kablo"
            category      = "material"
            plannedAmount = 40
            actualAmount  = 20
        } -ExpectStatus @(201, 200))[0]
    Assert-True (-not $ok.over) "malzeme uygun"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/budget")[0]
    Assert-True ([int]$pack.overCount -eq 1) "paket asimi=1 (WBS toplam 170>140)"
    Assert-True ([double]$pack.plannedAmount -eq 140) "plan=140"
    Assert-True ([double]$pack.actualAmount -eq 170) "gercek=170"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status.counts.overBudget -eq 1) "status overBudget=1"
    $row = @($status.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "overBudget") "WBS overBudget bayragi"

    $reduced = @(Invoke-Ops -Method PUT -Path "/budget/$($over.id)" -Body @{ actualAmount = 80 })[0]
    Assert-True (-not $reduced.over) "iscilik 80'e indi, kalem uygun"
    $pack2 = @(Invoke-Ops -Path "/projects/$projectId/budget")[0]
    Assert-True ([int]$pack2.overCount -eq 0) "WBS toplam 100<=140, asım yok"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.budgetLines).Count -eq 2) "GET detail budgetLines=2"

    Invoke-Ops -Method DELETE -Path "/budget/$($ok.id)" -ExpectStatus @(204, 200) | Out-Null
    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftMat = @($after.budgetLines) | Where-Object { $_.id -eq $ok.id }
    Assert-True ($leftMat.Count -eq 0) "malzeme silindi"

    $extraWbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Gecici"
        } -ExpectStatus @(201, 200))[0]
    $tempLine = @(Invoke-Ops -Method POST -Path "/projects/$projectId/budget" -Body @{
            wbsId         = $extraWbs.id
            name          = "Gecici"
            category      = "other"
            plannedAmount = 5
        } -ExpectStatus @(201, 200))[0]
    Invoke-Ops -Method DELETE -Path "/wbs/$($extraWbs.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftTemp = @($afterWbs.budgetLines) | Where-Object { $_.id -eq $tempLine.id }
    Assert-True ($leftTemp.Count -eq 0) "WBS silince kalem dustu"

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

    Write-Host "F2-5 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-5 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
