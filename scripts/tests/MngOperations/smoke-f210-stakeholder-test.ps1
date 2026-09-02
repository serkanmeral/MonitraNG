# Smoke: F2-10 dis paydas / sinirli gorunurluk (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F210-$stamp"
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

Write-Host "F2-10 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-10 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Tedarik paylasimi"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-10 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            kind = "supplier"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "adsiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            name  = "Acme Tedarik"
            wbsId = $foreignWbsId
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            name   = "Acme Tedarik"
            status = "revoked"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "revoked notesuz 400"

    $open = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            name        = "Acme Tedarik"
            kind        = "supplier"
            wbsId       = $wbsId
            accessUntil = $pastDue
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($open.open) "olusturma open"
    Assert-True ($open.incomplete) "paylasim yok incomplete"
    Assert-True ($open.overdue) "gecmis accessUntil overdue"
    Assert-True ($open.itemCount -eq 0) "itemCount=0"
    Assert-True (-not $open.revokedAt) "open damgasiz"

    Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            name = "Acme Tedarik"
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni ad 409"

    $projectLevel = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            name   = "Musteri FAT izleyici"
            kind   = "customer"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($projectLevel.open) "active open"
    Assert-True ($projectLevel.incomplete) "active paylasimsiz incomplete"
    Assert-True (-not $projectLevel.wbsId) "proje duzeyi WBS bos"
    Assert-True (-not $projectLevel.overdue) "proje duzeyi overdue degil"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status.counts.openStakeholder -eq 2) "status openStakeholder=2"
    Assert-True ([int]$status.counts.incompleteStakeholder -eq 2) "status incompleteStakeholder=2"
    Assert-True ([int]$status.counts.overdueStakeholder -eq 1) "status overdueStakeholder=1"
    $row = @($status.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "openStakeholder") "WBS openStakeholder bayragi"
    Assert-True (@($row.flags) -contains "incompleteStakeholder") "WBS incompleteStakeholder bayragi"
    Assert-True (@($row.flags) -contains "overdueStakeholder") "WBS overdueStakeholder bayragi"

    $withShare = @(Invoke-Ops -Method PUT -Path "/stakeholders/$($open.id)" -Body @{
            resourceIds = @("doc-share-1", "doc-share-2")
        })[0]
    Assert-True ($withShare.itemCount -eq 2) "paylasim eklenince itemCount=2"
    Assert-True (-not $withShare.incomplete) "paylasim eklenince incomplete kalkti"
    Assert-True ($withShare.open) "paylasimli hâlâ open"

    $statusEv = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$statusEv.counts.incompleteStakeholder -eq 1) "paylasim sonrasi incomplete=1 (proje duzeyi)"
    $rowEv = @($statusEv.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($rowEv.flags) -notcontains "incompleteStakeholder") "WBS incompleteStakeholder kalkti"
    Assert-True (@($rowEv.flags) -contains "openStakeholder") "WBS openStakeholder duruyor"

    $revoked = @(Invoke-Ops -Method PUT -Path "/stakeholders/$($open.id)" -Body @{
            status      = "revoked"
            note        = "Sozlesme bitti"
            resourceIds = @("doc-share-1", "doc-share-2")
        })[0]
    Assert-True ($revoked.status -eq "revoked") "revoked"
    Assert-True (-not $revoked.open) "artik open degil"
    Assert-True (-not $revoked.overdue) "artik overdue degil"
    Assert-True ([bool]$revoked.revokedAt) "damga revokedAt"
    Assert-True ([bool]$revoked.revokedBy) "damga revokedBy"

    $status2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status2.counts.openStakeholder -eq 1) "revoke sonrasi open=1"
    Assert-True ([int]$status2.counts.overdueStakeholder -eq 0) "revoke sonrasi overdue=0"
    $row2 = @($status2.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row2.flags) -notcontains "openStakeholder") "WBS openStakeholder kalkti"
    Assert-True (@($row2.flags) -notcontains "overdueStakeholder") "WBS overdueStakeholder kalkti"

    $reopen = @(Invoke-Ops -Method PUT -Path "/stakeholders/$($open.id)" -Body @{ status = "invited" })[0]
    Assert-True ($reopen.open) "invited'a donus"
    Assert-True (-not $reopen.revokedAt) "damga silindi"

    $withdrawn = @(Invoke-Ops -Method PUT -Path "/stakeholders/$($projectLevel.id)" -Body @{
            status = "revoked"
            note   = "Musteri erteledi"
        })[0]
    Assert-True ($withdrawn.status -eq "revoked") "proje duzeyi revoked"
    Assert-True ([bool]$withdrawn.revokedAt) "proje duzeyi revoked damgasi"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/stakeholders")[0]
    Assert-True ([int]$pack.openCount -eq 1) "GET stakeholders openCount=1 (yeniden acilan)"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.stakeholders).Count -eq 2) "GET detail stakeholders=2"

    Invoke-Ops -Method DELETE -Path "/stakeholders/$($projectLevel.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterDel = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftProjectLevel = @($afterDel.stakeholders) | Where-Object { $_.id -eq $projectLevel.id }
    Assert-True ($leftProjectLevel.Count -eq 0) "proje duzeyi paydas silindi"

    $extraWbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Gecici"
        } -ExpectStatus @(201, 200))[0]
    $tempStakeholder = @(Invoke-Ops -Method POST -Path "/projects/$projectId/stakeholders" -Body @{
            name  = "Gecici paydas"
            wbsId = $extraWbs.id
        } -ExpectStatus @(201, 200))[0]
    Invoke-Ops -Method DELETE -Path "/wbs/$($extraWbs.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftTemp = @($afterWbs.stakeholders) | Where-Object { $_.id -eq $tempStakeholder.id }
    Assert-True ($leftTemp.Count -eq 0) "WBS silince bagli paydas dustu"
    $kept = @($afterWbs.stakeholders) | Where-Object { $_.id -eq $open.id }
    Assert-True ($kept.Count -eq 1) "diger paydas duruyor"

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

    Write-Host "F2-10 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-10 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
