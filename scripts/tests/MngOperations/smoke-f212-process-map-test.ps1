# Smoke: F2-12 surec haritasi kutuphanesi (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F212-$stamp"

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

Write-Host "F2-12 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-12 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $wbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "FAT sureci"
        } -ExpectStatus @(201, 200))[0]
    $wbsId = [string]$wbs.id

    $other = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = "$code-X"
            name   = "F2-12 foreign $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $otherId = [string]$other.id
    $foreign = @(Invoke-Ops -Method POST -Path "/projects/$otherId/wbs" -Body @{
            kind = "task"
            name = "Yabanci"
        } -ExpectStatus @(201, 200))[0]
    $foreignWbsId = [string]$foreign.id

    Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            kind = "procedure"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "adsiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name  = "FAT akisi"
            wbsId = $foreignWbsId
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "yabanci WBS 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name   = "FAT akisi"
            status = "current"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "current belgesiz 400"

    Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name   = "FAT akisi"
            status = "superseded"
        } -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "superseded notesuz 400"

    $draft = @(Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name  = "FAT akisi"
            kind  = "procedure"
            wbsId = $wbsId
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($draft.open) "olusturma open"
    Assert-True ($draft.incomplete) "belge yok incomplete"
    Assert-True (-not $draft.current) "henuz current degil"
    Assert-True (-not $draft.currentAt) "open damgasiz"

    Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name = "FAT akisi"
        } -ExpectStatus @(409) | Out-Null
    Assert-True ($script:LastStatus -eq 409) "ayni ad 409"

    $projectLevel = @(Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name       = "Organizasyon semasi"
            kind       = "org"
            resourceId = "doc-org-1"
            status     = "current"
        } -ExpectStatus @(201, 200))[0]
    Assert-True ($projectLevel.current) "proje duzeyi current"
    Assert-True (-not $projectLevel.open) "current open degil"
    Assert-True (-not $projectLevel.wbsId) "proje duzeyi WBS bos"
    Assert-True ([bool]$projectLevel.currentAt) "current damgasi"

    $status = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status.counts.openProcessMap -eq 1) "status openProcessMap=1"
    Assert-True ([int]$status.counts.incompleteProcessMap -eq 1) "status incompleteProcessMap=1"
    Assert-True ([int]$status.counts.currentProcessMap -eq 1) "status currentProcessMap=1"
    $row = @($status.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row.flags) -contains "openProcessMap") "WBS openProcessMap bayragi"
    Assert-True (@($row.flags) -contains "incompleteProcessMap") "WBS incompleteProcessMap bayragi"

    $withDoc = @(Invoke-Ops -Method PUT -Path "/process-maps/$($draft.id)" -Body @{
            resourceId = "doc-fat-flow"
        })[0]
    Assert-True ($withDoc.resourceId -eq "doc-fat-flow") "belge baglandi"
    Assert-True (-not $withDoc.incomplete) "belge eklenince incomplete kalkti"
    Assert-True ($withDoc.open) "taslak hâlâ open"

    $statusEv = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$statusEv.counts.incompleteProcessMap -eq 0) "belge sonrasi incomplete=0"
    $rowEv = @($statusEv.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($rowEv.flags) -notcontains "incompleteProcessMap") "WBS incompleteProcessMap kalkti"
    Assert-True (@($rowEv.flags) -contains "openProcessMap") "WBS openProcessMap duruyor"

    $current = @(Invoke-Ops -Method PUT -Path "/process-maps/$($draft.id)" -Body @{
            status     = "current"
            resourceId = "doc-fat-flow"
        })[0]
    Assert-True ($current.status -eq "current") "current"
    Assert-True ($current.current) "current flag"
    Assert-True (-not $current.open) "artik open degil"
    Assert-True ([bool]$current.currentAt) "damga currentAt"
    Assert-True ([bool]$current.currentBy) "damga currentBy"

    $status2 = @(Invoke-Ops -Path "/projects/$projectId/status")[0]
    Assert-True ([int]$status2.counts.openProcessMap -eq 0) "current sonrasi open=0"
    Assert-True ([int]$status2.counts.currentProcessMap -eq 2) "current=2"
    $row2 = @($status2.items) | Where-Object { $_.wbsId -eq $wbsId } | Select-Object -First 1
    Assert-True (@($row2.flags) -notcontains "openProcessMap") "WBS openProcessMap kalkti"

    $reopen = @(Invoke-Ops -Method PUT -Path "/process-maps/$($draft.id)" -Body @{ status = "draft" })[0]
    Assert-True ($reopen.open) "draft'a donus"
    Assert-True (-not $reopen.currentAt) "current damgasi silindi"

    $superseded = @(Invoke-Ops -Method PUT -Path "/process-maps/$($projectLevel.id)" -Body @{
            status = "superseded"
            note   = "Yeni org semasi gelecek"
        })[0]
    Assert-True ($superseded.status -eq "superseded") "superseded"
    Assert-True ([bool]$superseded.supersededAt) "superseded damgasi"

    $pack = @(Invoke-Ops -Path "/projects/$projectId/process-maps")[0]
    Assert-True ([int]$pack.openCount -eq 1) "GET process-maps openCount=1"
    Assert-True ([int]$pack.currentCount -eq 0) "GET currentCount=0"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True (@($detail.processMaps).Count -eq 2) "GET detail processMaps=2"

    Invoke-Ops -Method DELETE -Path "/process-maps/$($projectLevel.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterDel = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $left = @($afterDel.processMaps) | Where-Object { $_.id -eq $projectLevel.id }
    Assert-True ($left.Count -eq 0) "proje duzeyi harita silindi"

    $extraWbs = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind = "task"
            name = "Gecici"
        } -ExpectStatus @(201, 200))[0]
    $tempMap = @(Invoke-Ops -Method POST -Path "/projects/$projectId/process-maps" -Body @{
            name  = "Gecici harita"
            wbsId = $extraWbs.id
        } -ExpectStatus @(201, 200))[0]
    Invoke-Ops -Method DELETE -Path "/wbs/$($extraWbs.id)" -ExpectStatus @(204, 200) | Out-Null
    $afterWbs = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $leftTemp = @($afterWbs.processMaps) | Where-Object { $_.id -eq $tempMap.id }
    Assert-True ($leftTemp.Count -eq 0) "WBS silince bagli harita dustu"
    $kept = @($afterWbs.processMaps) | Where-Object { $_.id -eq $draft.id }
    Assert-True ($kept.Count -eq 1) "diger harita duruyor"

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

    Write-Host "F2-12 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-12 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
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
