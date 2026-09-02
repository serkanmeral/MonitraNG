# Smoke: F2-7 yukumluluk kaydi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F27-$stamp"
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

Write-Host "F2-7 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-7 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Yedekleme"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-7 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            clauseRef = "4.2"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "metinsiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title = "Yedekleme proseduru"
            wbsId = $foreignWbsId
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title  = "Yedekleme proseduru"
            status = "waived"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "feragat notesuz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title  = "Yedekleme proseduru"
            status = "satisfied"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "karsilandi kanitsiz 400"

    $open = @(Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title     = "Yedekleme proseduru yazilacak"
            clauseRef = "4.2"
            wbsId     = $wbsId
            dueDate   = $pastDue
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($open.open) "olusturma open"
    Assert-True ($open.overdue) "gecmis dueDate overdue"
    Assert-True ($open.unbound) "is yok unbound"
    Assert-True ($open.missingEvidence) "kanit yok"
    Assert-True (-not $open.closedAt) "open damgasiz"

    Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title     = "Yedekleme proseduru yazilacak"
            clauseRef = "4.2"
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni madde+metin 409"

    $otherClause = @(Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title     = "Yedekleme proseduru yazilacak"
            clauseRef = "4.3"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($otherClause.open) "farkli madde no ayri kayit"

    $projectLevel = @(Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title = "Kalite el kitabi yururlukte"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($projectLevel.open) "proje duzeyi open"
    Assert-True (-not $projectLevel.wbsId) "proje duzeyi WBS bos"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status.counts.openObligation -eq 3) "status openObligation=3"
    Assert-True ([int]$status.counts.overdueObligation -eq 1) "status overdueObligation=1"
    Assert-True ([int]$status.counts.unboundObligation -eq 3) "status unboundObligation=3"
    $row = @($status.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "openObligation") "WBS openObligation bayragi"
    Assert-True (@($row.flags) -contains "overdueObligation") "WBS overdueObligation bayragi"
    Assert-True (@($row.flags) -contains "unboundObligation") "WBS unboundObligation bayragi"

    $bound = @(Invoke-Ops -Method PUT -Path "/obligations/$($open.id)" -Body @{ workItemId = "wi-backup-1" })[0]
    Assert-True (-not $bound.unbound) "is baglaninca unbound kalkti"

    $statusBound = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$statusBound.counts.unboundObligation -eq 2) "is baglaninca unbound=2"
    $rowBound = @($statusBound.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($rowBound.flags) -notcontains "unboundObligation") "WBS unboundObligation kalkti"

    $done = @(Invoke-Ops -Method PUT -Path "/obligations/$($open.id)" -Body @{
            status             = "satisfied"
            evidenceResourceId = "doc-backup-ev"
        })[0]
    Assert-True ($done.status -eq "satisfied") "karsilandi"
    Assert-True (-not $done.open) "artik open degil"
    Assert-True (-not $done.overdue) "artik overdue degil"
    Assert-True ([bool]$done.closedAt) "damga closedAt"
    Assert-True ([bool]$done.closedBy) "damga closedBy"

    $status2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status2.counts.openObligation -eq 2) "karsilandi sonrasi open=2"
    Assert-True ([int]$status2.counts.overdueObligation -eq 0) "karsilandi sonrasi overdue=0"
    $row2 = @($status2.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row2.flags) -notcontains "openObligation") "WBS openObligation kalkti"
    Assert-True (@($row2.flags) -notcontains "overdueObligation") "WBS overdueObligation kalkti"

    $reopen = @(Invoke-Ops -Method PUT -Path "/obligations/$($open.id)" -Body @{ status = "open" })[0]
    Assert-True ($reopen.open) "open'e donus"
    Assert-True (-not $reopen.closedAt) "damga silindi"

    $waived = @(Invoke-Ops -Method PUT -Path "/obligations/$($projectLevel.id)" -Body @{
            status = "waived"
            note   = "Kapsam disi"
        })[0]
    Assert-True ($waived.status -eq "waived") "feragat"
    Assert-True ([bool]$waived.closedAt) "feragat damgasi"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/obligations")[0]
    Assert-True ([int]$pack.openCount -eq 2) "GET obligations openCount=2 (4.3 + yeniden acilan)"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.obligations).Count -eq 3) "GET detail obligations=3"

    Invoke-Ops -Method DELETE -Path "/obligations/$($otherClause.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterDel = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftOther = @($afterDel.obligations) | Where-Object { $_.id -eq $otherClause.id }
    Assert-True ($leftOther.Count -eq 0) "4.3 silindi"

    $extraWbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Gecici"
        } -ExpectStatus @(201, 200))[0]
    $tempObl = @(Invoke-Ops -Method POST -Path "/projects/$projectId/obligations" -Body @{
            title = "Gecici madde"
            wbsId = $extraWbs.id
        } -ExpectStatus @(201, 200))[0]
    Invoke-Ops -Method DELETE -Path "/wbs/$($extraWbs.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftTemp = @($afterWbs.obligations) | Where-Object { $_.id -eq $tempObl.id }
    Assert-True ($leftTemp.Count -eq 0) "WBS silince bagli obligation dustu"
    $kept = @($afterWbs.obligations) | Where-Object { $_.id -eq $open.id }
    Assert-True ($kept.Count -eq 1) "diger obligation duruyor"

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

    Write-Host "F2-7 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-7 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
