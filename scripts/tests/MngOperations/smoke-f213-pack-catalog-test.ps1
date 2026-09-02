# Smoke: F2-13 ic paket katalogu onizleme / skip|update / sok (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F213-$stamp"

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

function Get-KickOff($projectId) {
    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $row = @($detail.wbs) | Where-Object { $_.name -eq "Kick-off" } | Select-Object -First 1
    Assert-True ($null -ne $row -and $row.id) "Kick-off bulundu"
    return $row
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectId = $null

Write-Host "F2-13 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-13 smoke $stamp"
            status = "active"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "proje id=$projectId"

    $emptyPreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply")[0]
    Assert-True ($emptyPreview.createCount -ge 8) "bos proje createCount=$($emptyPreview.createCount)"
    Assert-True (($emptyPreview.skipCount -as [int]) -eq 0) "bos proje skipCount=0"
    Assert-True (($emptyPreview.updateCount -as [int]) -eq 0) "bos proje updateCount=0"

    Invoke-Ops -Path "/projects/$projectId/packs/no-such-pack/preview" -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "bilinmeyen paket preview 400"

    $applied = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=skip")[0]
    Assert-True ($applied.created -ge 8) "apply skip created=$($applied.created)"
    Assert-True (($applied.updated -as [int]) -eq 0) "apply skip updated=0"

    $afterPreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply&mode=skip")[0]
    Assert-True (($afterPreview.createCount -as [int]) -eq 0) "tekrar preview create=0"
    Assert-True ($afterPreview.skipCount -ge 8) "tekrar preview skip=$($afterPreview.skipCount)"

    $reapply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=skip")[0]
    Assert-True (($reapply.created -as [int]) -eq 0) "reapply created=0"
    Assert-True ($reapply.skipped -ge 8) "reapply skipped=$($reapply.skipped)"

    $kick = Get-KickOff $projectId
    Invoke-Ops -Method PUT -Path "/wbs/$($kick.id)" -Body @{ kind = "milestone" } | Out-Null
    $kick = Get-KickOff $projectId
    Assert-True ($kick.kind -eq "milestone") "Kick-off kind=milestone"

    $updatePreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply&mode=update")[0]
    Assert-True ($updatePreview.updateCount -ge 1) "update preview updateCount=$($updatePreview.updateCount)"
    $kickItem = @($updatePreview.items) | Where-Object { $_.path -match "Kick-off" } | Select-Object -First 1
    Assert-True ($kickItem.action -eq "update") "Kick-off preview action=update"

    $skipPreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=apply&mode=skip")[0]
    Assert-True (($skipPreview.updateCount -as [int]) -eq 0) "skip preview updateCount=0"

    $skipApply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=skip")[0]
    Assert-True (($skipApply.updated -as [int]) -eq 0) "apply skip kind degistirmez"
    $kick = Get-KickOff $projectId
    Assert-True ($kick.kind -eq "milestone") "apply skip sonrasi Kick-off hâlâ milestone"

    $updateApply = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo?mode=update")[0]
    Assert-True ($updateApply.updated -ge 1) "apply update updated=$($updateApply.updated)"
    $kick = Get-KickOff $projectId
    Assert-True ($kick.kind -eq "task") "apply update Kick-off kind=task"

    Invoke-Ops -Method PUT -Path "/wbs/$($kick.id)" -Body @{ percentComplete = 50 } | Out-Null
    $kick = Get-KickOff $projectId
    Assert-True ($kick.percentComplete -ge 50) "Kick-off percent=$($kick.percentComplete)"

    $detachPreview = @(Invoke-Ops -Path "/projects/$projectId/packs/pmo/preview?intent=detach")[0]
    Assert-True ($detachPreview.keepCount -ge 1) "detach preview keepCount=$($detachPreview.keepCount)"
    $keptKick = @($detachPreview.items) | Where-Object { $_.path -match "Kick-off" } | Select-Object -First 1
    Assert-True ($keptKick.action -eq "keep") "ilerlemeli Kick-off keep"

    $detached = @(Invoke-Ops -Method DELETE -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ($detached.kept -ge 1) "detach kept=$($detached.kept)"
    $kick = Get-KickOff $projectId
    Assert-True ($kick.kind -eq "task") "ilerlemeli Kick-off silinmedi"

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
    }
    else {
        Write-Host "KeepArtifacts: proje birakildi $projectId" -ForegroundColor Yellow
    }

    Write-Host "F2-13 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-13 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
