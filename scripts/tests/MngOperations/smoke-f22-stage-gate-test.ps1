# Smoke: F2-2 asama kapisi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F22-$stamp"

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

Write-Host "F2-2 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-2 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $ms = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "milestone"
            name = "Kapsam onayi"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$ms.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-2 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "milestone"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    $open = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
            name      = "Baslatma kapisi"
            wbsId     = $wbsId
            status    = "open"
            criteria  = @("Kapsam net", "Plan onayli")
            satisfied = @()
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($open.status -eq "open") "acik kapi"
    Assert-True (@($open.criteria).Count -eq 2) "kriter=2"
    Assert-True ($open.wbsId -eq $wbsId) "wbs bagli"

    Invoke-Ops -Method PUT -Path "/stage-gates/$($open.id)" -Body @{ status = "passed" } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "eksik kriterle gecis 400"

    Invoke-Ops -Method PUT -Path "/stage-gates/$($open.id)" -Body @{ status = "failed" } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "notsuz red 400"

    $passed = @(Invoke-Ops -Method PUT -Path "/stage-gates/$($open.id)" -Body @{
            satisfied = @("Kapsam net", "Plan onayli")
            status    = "passed"
        })[0]
    Assert-True ($passed.status -eq "passed") "gecildi"
    Assert-True ($passed.decidedBy) "decidedBy dolu"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack.counts.openGate -eq 0) "openGate=0"
    $row = @($pack.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    $rowFlags = @($row.flags)
    Assert-True ($rowFlags -notcontains "openGate") "satirda acik kapi yok"

    $fail = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
            name   = "Kalite kapisi"
            status = "failed"
            note   = "Kanit eksik"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($fail.status -eq "failed") "red olusturuldu"

    $waive = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
            name   = "Sponsor feragati"
            status = "waived"
            note   = "Sponsor kabul etti"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($waive.status -eq "waived") "feragat"

    $emptyPass = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
            name   = "Bos kontrol listesi"
            status = "passed"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($emptyPass.status -eq "passed") "kriter yokken gecis"

    $pack2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack2.counts.failedGate -eq 1) "failedGate=1"
    Assert-True ([int]$pack2.counts.openGate -eq 0) "hala acik kapi yok"
    $gateNames = @($pack2.gates | ForEach-Object { $_.name })
    Assert-True ($gateNames -contains "Kalite kapisi") "status pack gates"

    Invoke-Ops -Method POST -Path "/projects/$projectId/stage-gates" -Body @{
        name  = "Yabanci"
        wbsId = $foreignWbsId
    } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.stageGates).Count -ge 4) "GET detail stageGates"

    Invoke-Ops -Method DELETE -Path "/stage-gates/$($fail.id)" -ExpectStatus @(204, 200) | Out-Null
    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftFail = @($after.stageGates) | Where-Object { $_.id -eq $fail.id }
    Assert-True ($leftFail.Count -eq 0) "red kapisi silindi"

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

    Write-Host "F2-2 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-2 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
