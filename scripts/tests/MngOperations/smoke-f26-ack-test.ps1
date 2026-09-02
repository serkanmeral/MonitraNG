# Smoke: F2-6 okundu-anlasildi kaydi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F26-$stamp"
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

Write-Host "F2-6 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-6 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Prosedur"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-6 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId = "doc-qp-12"
            personName = "Ali"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "basliksiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            title      = "QP-12"
            personName = "Ali"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "belgesiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId = "doc-qp-12"
            title      = "QP-12"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "kisisiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId = "doc-qp-12"
            title      = "QP-12"
            personName = "Ali"
            wbsId      = $foreignWbsId
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId = "doc-qp-12"
            title      = "QP-12"
            personName = "Ali"
            status     = "waived"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "feragat notesuz 400"

    $pending = @(Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId   = "doc-qp-12"
            title        = "QP-12 Kaynak"
            versionLabel = "3"
            personName   = "Ali"
            wbsId        = $wbsId
            dueDate      = $pastDue
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($pending.pending) "olusturma pending"
    Assert-True ($pending.overdue) "gecmis dueDate overdue"
    Assert-True (-not $pending.acknowledgedAt) "pending damgasiz"

    Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId   = "doc-qp-12"
            title        = "QP-12 Kaynak"
            versionLabel = "3"
            personName   = "Ali"
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni belge+revizyon+kisi 409"

    $rev = @(Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId   = "doc-qp-12"
            title        = "QP-12 Kaynak"
            versionLabel = "4"
            personName   = "Ali"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($rev.pending) "yeni revizyon ayri kayit"

    $projectLevel = @(Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId = "doc-policy"
            title      = "Kalite politikasi"
            personName = "Ayse"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($projectLevel.pending) "proje duzeyi pending"
    Assert-True (-not $projectLevel.wbsId) "proje duzeyi WBS bos"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status.counts.pendingAck -eq 3) "status pendingAck=3"
    Assert-True ([int]$status.counts.overdueAck -eq 1) "status overdueAck=1"
    $row = @($status.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "pendingAck") "WBS pendingAck bayragi"
    Assert-True (@($row.flags) -contains "overdueAck") "WBS overdueAck bayragi"

    $done = @(Invoke-Ops -Method PUT -Path "/acks/$($pending.id)" -Body @{ status = "acknowledged" })[0]
    Assert-True ($done.status -eq "acknowledged") "okundu"
    Assert-True (-not $done.pending) "artik pending degil"
    Assert-True (-not $done.overdue) "artik overdue degil"
    Assert-True ([bool]$done.acknowledgedAt) "damga acknowledgedAt"
    Assert-True ([bool]$done.acknowledgedBy) "damga acknowledgedBy"

    $status2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status2.counts.pendingAck -eq 2) "okundu sonrasi pendingAck=2"
    Assert-True ([int]$status2.counts.overdueAck -eq 0) "okundu sonrasi overdueAck=0"
    $row2 = @($status2.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row2.flags) -notcontains "overdueAck") "WBS overdueAck kalkti"
    Assert-True (@($row2.flags) -notcontains "pendingAck") "WBS pendingAck kalkti (proje duzeyi bayrak yazmaz)"

    $reopen = @(Invoke-Ops -Method PUT -Path "/acks/$($pending.id)" -Body @{ status = "pending" })[0]
    Assert-True ($reopen.pending) "pendinge donus"
    Assert-True (-not $reopen.acknowledgedAt) "damga silindi"

    $waived = @(Invoke-Ops -Method PUT -Path "/acks/$($projectLevel.id)" -Body @{
            status = "waived"
            note   = "Eski personel"
        })[0]
    Assert-True ($waived.status -eq "waived") "feragat"
    Assert-True ([bool]$waived.acknowledgedAt) "feragat damgasi"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/acks")[0]
    Assert-True ([int]$pack.pendingCount -eq 2) "GET acks pendingCount=2 (rev4 + yeniden acilan)"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.acknowledgements).Count -eq 3) "GET detail acknowledgements=3"

    Invoke-Ops -Method DELETE -Path "/acks/$($rev.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterDel = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftRev = @($afterDel.acknowledgements) | Where-Object { $_.id -eq $rev.id }
    Assert-True ($leftRev.Count -eq 0) "revizyon 4 silindi"

    $extraWbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Gecici"
        } -ExpectStatus @(201, 200))[0]
    $tempAck = @(Invoke-Ops -Method POST -Path "/projects/$projectId/acks" -Body @{
            resourceId = "doc-temp"
            title      = "Gecici"
            personName = "Can"
            wbsId      = $extraWbs.id
        } -ExpectStatus @(201, 200))[0]
    Invoke-Ops -Method DELETE -Path "/wbs/$($extraWbs.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftTemp = @($afterWbs.acknowledgements) | Where-Object { $_.id -eq $tempAck.id }
    Assert-True ($leftTemp.Count -eq 0) "WBS silince bagli ack dustu"
    $kept = @($afterWbs.acknowledgements) | Where-Object { $_.id -eq $pending.id }
    Assert-True ($kept.Count -eq 1) "diger ack duruyor"

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

    Write-Host "F2-6 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-6 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
