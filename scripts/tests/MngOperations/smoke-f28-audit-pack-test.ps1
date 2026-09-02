# Smoke: F2-8 denetim/musteri paketi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F28-$stamp"
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

Write-Host "F2-8 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-8 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "FAT paketi"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-8 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            kind = "audit"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "adsiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name  = "FAT kanit paketi"
            wbsId = $foreignWbsId
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name   = "FAT kanit paketi"
            status = "issued"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "issued kanitsiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name   = "FAT kanit paketi"
            status = "withdrawn"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "withdrawn notesuz 400"

    $open = @(Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name    = "FAT kanit paketi"
            kind    = "audit"
            wbsId   = $wbsId
            dueDate = $pastDue
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($open.open) "olusturma open"
    Assert-True ($open.incomplete) "kanit yok incomplete"
    Assert-True ($open.overdue) "gecmis dueDate overdue"
    Assert-True ($open.itemCount -eq 0) "itemCount=0"
    Assert-True (-not $open.issuedAt) "open damgasiz"

    Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name = "FAT kanit paketi"
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni ad 409"

    $assembled = @(Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name   = "Musteri FAT ozeti"
            kind   = "customer"
            status = "assembled"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($assembled.open) "assembled open"
    Assert-True ($assembled.incomplete) "assembled kanitsiz incomplete"
    Assert-True (-not $assembled.wbsId) "proje duzeyi WBS bos"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status.counts.openAuditPack -eq 2) "status openAuditPack=2"
    Assert-True ([int]$status.counts.incompleteAuditPack -eq 2) "status incompleteAuditPack=2"
    Assert-True ([int]$status.counts.overdueAuditPack -eq 1) "status overdueAuditPack=1"
    $row = @($status.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "openAuditPack") "WBS openAuditPack bayragi"
    Assert-True (@($row.flags) -contains "incompleteAuditPack") "WBS incompleteAuditPack bayragi"
    Assert-True (@($row.flags) -contains "overdueAuditPack") "WBS overdueAuditPack bayragi"

    $withEvidence = @(Invoke-Ops -Method PUT -Path "/audit-packs/$($open.id)" -Body @{
            resourceIds = @("doc-fat-1", "doc-fat-2")
        })[0]
    Assert-True ($withEvidence.itemCount -eq 2) "kanit eklenince itemCount=2"
    Assert-True (-not $withEvidence.incomplete) "kanit eklenince incomplete kalkti"
    Assert-True ($withEvidence.open) "kanitli hâlâ open"

    $statusEv = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$statusEv.counts.incompleteAuditPack -eq 1) "kanit sonrasi incomplete=1 (proje duzeyi)"
    $rowEv = @($statusEv.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($rowEv.flags) -notcontains "incompleteAuditPack") "WBS incompleteAuditPack kalkti"
    Assert-True (@($rowEv.flags) -contains "openAuditPack") "WBS openAuditPack duruyor"

    $issued = @(Invoke-Ops -Method PUT -Path "/audit-packs/$($open.id)" -Body @{
            status      = "issued"
            resourceIds = @("doc-fat-1", "doc-fat-2")
        })[0]
    Assert-True ($issued.status -eq "issued") "issued"
    Assert-True (-not $issued.open) "artik open degil"
    Assert-True (-not $issued.overdue) "artik overdue degil"
    Assert-True ([bool]$issued.issuedAt) "damga issuedAt"
    Assert-True ([bool]$issued.issuedBy) "damga issuedBy"

    $status2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status2.counts.openAuditPack -eq 1) "issued sonrasi open=1"
    Assert-True ([int]$status2.counts.overdueAuditPack -eq 0) "issued sonrasi overdue=0"
    $row2 = @($status2.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row2.flags) -notcontains "openAuditPack") "WBS openAuditPack kalkti"
    Assert-True (@($row2.flags) -notcontains "overdueAuditPack") "WBS overdueAuditPack kalkti"

    $reopen = @(Invoke-Ops -Method PUT -Path "/audit-packs/$($open.id)" -Body @{ status = "draft" })[0]
    Assert-True ($reopen.open) "draft'a donus"
    Assert-True (-not $reopen.issuedAt) "damga silindi"

    $withdrawn = @(Invoke-Ops -Method PUT -Path "/audit-packs/$($assembled.id)" -Body @{
            status = "withdrawn"
            note   = "Musteri erteledi"
        })[0]
    Assert-True ($withdrawn.status -eq "withdrawn") "withdrawn"
    Assert-True ([bool]$withdrawn.issuedAt) "withdrawn damgasi"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/audit-packs")[0]
    Assert-True ([int]$pack.openCount -eq 1) "GET audit-packs openCount=1 (yeniden acilan)"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.auditPacks).Count -eq 2) "GET detail auditPacks=2"

    Invoke-Ops -Method DELETE -Path "/audit-packs/$($assembled.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterDel = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftAssembled = @($afterDel.auditPacks) | Where-Object { $_.id -eq $assembled.id }
    Assert-True ($leftAssembled.Count -eq 0) "musteri paketi silindi"

    $extraWbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Gecici"
        } -ExpectStatus @(201, 200))[0]
    $tempPack = @(Invoke-Ops -Method POST -Path "/projects/$projectId/audit-packs" -Body @{
            name  = "Gecici paket"
            wbsId = $extraWbs.id
        } -ExpectStatus @(201, 200))[0]
    Invoke-Ops -Method DELETE -Path "/wbs/$($extraWbs.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftTemp = @($afterWbs.auditPacks) | Where-Object { $_.id -eq $tempPack.id }
    Assert-True ($leftTemp.Count -eq 0) "WBS silince bagli paket dustu"
    $kept = @($afterWbs.auditPacks) | Where-Object { $_.id -eq $open.id }
    Assert-True ($kept.Count -eq 1) "diger paket duruyor"

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

    Write-Host "F2-8 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-8 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
