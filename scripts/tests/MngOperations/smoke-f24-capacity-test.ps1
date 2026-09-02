# Smoke: F2-4 kaba kaynak/kapasite (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F24-$stamp"

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

Write-Host "F2-4 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-4 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind          = "task"
            name          = "Entegrasyon"
            plannedStart  = "2026-09-07T00:00:00.000Z"
            plannedFinish = "2026-09-11T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-4 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $wbsId
            plannedHours = 8
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "adsiz atama 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            name         = "Ali"
            plannedHours = 8
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "WBS'siz atama 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $wbsId
            name         = "Ali"
            plannedHours = -1
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "negatif saat 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $foreignWbsId
            name         = "Ali"
            plannedHours = 8
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    $over = @(Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $wbsId
            name         = "Ali"
            role         = "lead"
            plannedHours = 80
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($over.name -eq "Ali") "Ali atandi"
    Assert-True ([double]$over.plannedHours -eq 80) "80 saat"
    Assert-True (-not $over.unscheduled) "tarih penceresi WBS'den"

    Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $wbsId
            name         = "Ali"
            plannedHours = 8
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni kaynak+WBS 409"

    $ok = @(Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $wbsId
            name         = "Ayse"
            plannedHours = 16
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($ok.name -eq "Ayse") "Ayse atandi"

    $cap = @(Invoke-Ops -Path "/projects/$projectId/capacity")[0]
    Assert-True ([int]$cap.overloadedCount -eq 1) "overloadedCount=1"
    $ali = @($cap.people) | Where-Object { $_.name -eq "Ali" } | Select-Object -First 1
    $ayse = @($cap.people) | Where-Object { $_.name -eq "Ayse" } | Select-Object -First 1
    Assert-True ($ali.overloaded) "Ali asiri yuk"
    Assert-True ([double]$ali.weeks[0].hours -eq 80) "Ali hafta=80"
    Assert-True (-not $ayse.overloaded) "Ayse uygun"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$pack.counts.overloadedResource -eq 1) "status overloadedResource=1"
    $row = @($pack.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "overloadedResource") "WBS overloadedResource bayragi"

    $reduced = @(Invoke-Ops -Method PUT -Path "/assignments/$($over.id)" -Body @{ plannedHours = 8 })[0]
    Assert-True ([double]$reduced.plannedHours -eq 8) "Ali 8 saate indi"
    $cap2 = @(Invoke-Ops -Path "/projects/$projectId/capacity")[0]
    Assert-True ([int]$cap2.overloadedCount -eq 0) "indirince overloadedCount=0"

    $undated = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Belirsiz"
        } -ExpectStatus @(201, 200))[0]
    $lump = @(Invoke-Ops -Method POST -Path "/projects/$projectId/assignments" -Body @{
            wbsId        = $undated.id
            name         = "Can"
            plannedHours = 80
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($lump.unscheduled) "tarihsiz atama"
    $cap3 = @(Invoke-Ops -Path "/projects/$projectId/capacity")[0]
    $can = @($cap3.people) | Where-Object { $_.name -eq "Can" } | Select-Object -First 1
    Assert-True ($can.overloaded) "tarihsiz 80s asiri yuk"
    Assert-True ([double]$can.unscheduledHours -eq 80) "unscheduledHours=80"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.assignments).Count -eq 3) "GET detail assignments=3"

    Invoke-Ops -Method DELETE -Path "/assignments/$($ok.id)" -ExpectStatus @(204, 200) | Out-Null
    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftAyse = @($after.assignments) | Where-Object { $_.id -eq $ok.id }
    Assert-True ($leftAyse.Count -eq 0) "Ayse silindi"

    Invoke-Ops -Method DELETE -Path "/wbs/$($undated.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftCan = @($afterWbs.assignments) | Where-Object { $_.id -eq $lump.id }
    Assert-True ($leftCan.Count -eq 0) "WBS silince atama dustu"

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

    Write-Host "F2-4 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-4 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
