# Smoke: F1-4 proje / WBS / FS / baseline (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F14-$stamp"

function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
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
    return ,$result
}

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$script:LastErrorBody = ""
$projectId = $null

Write-Host "F1-4 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code          = $code
            name          = "F1-4 smoke $stamp"
            description   = "auto"
            status        = "active"
            plannedStart  = "2026-09-01T00:00:00.000Z"
            plannedFinish = "2026-09-30T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($projectId)) "proje olusturuldu id=$projectId"
    Assert-True ($created.code -eq $code) "proje kodu $code"

    $dup = Invoke-Ops -Method POST -Path "/projects" -Body @{
        code = $code
        name = "dup"
    } -ExpectStatus @(409, 400)
    Assert-True ($script:LastStatus -eq 409 -or $script:LastStatus -eq 400) "unique kod reddedildi ($script:LastStatus)"

    $root = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind          = "summary"
            name          = "Kurulum"
            plannedStart  = "2026-09-01T00:00:00.000Z"
            plannedFinish = "2026-09-10T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $rootId = [string]$root.id
    Assert-True ($root.wbsCode -eq "1" -or [string]::IsNullOrWhiteSpace($root.wbsCode) -eq $false) "kok WBS id=$rootId code=$($root.wbsCode)"

    $child = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            parentId      = $rootId
            kind          = "task"
            name          = "Saha"
            plannedStart  = "2026-09-02T00:00:00.000Z"
            plannedFinish = "2026-09-08T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $childId = [string]$child.id
    Assert-True ($child.parentId -eq $rootId) "alt WBS parent=$rootId"

    $ms = @(Invoke-Ops -Method POST -Path "/projects/$projectId/wbs" -Body @{
            kind         = "milestone"
            name         = "FAT"
            plannedStart = "2026-09-15T00:00:00.000Z"
        } -ExpectStatus @(201, 200))[0]
    $msId = [string]$ms.id
    Assert-True ($ms.kind -eq "milestone") "kilometre tasi id=$msId"

    $dep = @(Invoke-Ops -Method POST -Path "/projects/$projectId/dependencies" -Body @{
            predecessorId = $childId
            successorId   = $msId
            type          = "FS"
            lagDays       = 0
        } -ExpectStatus @(201, 200))[0]
    $depId = [string]$dep.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($depId)) "FS bagimlilik id=$depId"

    Invoke-Ops -Method POST -Path "/projects/$projectId/dependencies" -Body @{
        predecessorId = $msId
        successorId   = $childId
        type          = "FS"
    } -ExpectStatus @(400)
    Assert-True ($script:LastStatus -eq 400) "dongu reddedildi"

    $afterBase = @(Invoke-Ops -Method POST -Path "/projects/$projectId/baseline" -Body @{
            note = "smoke baseline"
        })[0]
    Assert-True ($null -ne $afterBase.project.baselineSetAt) "baseline alindi"
    $driftedBefore = [bool]$afterBase.project.baselineDrifted
    Assert-True (-not $driftedBefore) "baseline sonrasi sapma yok"

    $updatedChild = @(Invoke-Ops -Method PUT -Path "/wbs/$childId" -Body @{
            plannedFinish = "2026-09-20T00:00:00.000Z"
        })[0]
    Assert-True ([bool]$updatedChild.baselineDrifted) "plan kaydirinca WBS sapma"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    Assert-True ([bool]$detail.project.baselineDrifted) "proje sapma bayragi"
    Assert-True (@($detail.wbs).Count -ge 3) "WBS sayisi $(@($detail.wbs).Count)"
    Assert-True (@($detail.dependencies).Count -eq 1) "tek FS bagimlilik"

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200)
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404)
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
    }
    else {
        Write-Host "KeepArtifacts: proje birakildi $projectId" -ForegroundColor Yellow
    }

    Write-Host "F1-4 smoke PASSED" -ForegroundColor Green
}
catch {
    Write-Host "F1-4 smoke FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    exit 1
}
