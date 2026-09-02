# Smoke: F2-1 ic paket katalogu (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F21-$stamp"

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

Write-Host "F2-1 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $packs = @(Invoke-Ops -Path "/job-packs")[0]
    $pmo = @($packs) | Where-Object { $_.code -eq "pmo" } | Select-Object -First 1
    Assert-True ($pmo.version -eq "1.0.0") "katalog surum=$($pmo.version)"

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code   = $code
            name   = "F2-1 smoke $stamp"
            status = "draft"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    Assert-True ($projectId) "bos proje id=$projectId"

    $shelf = @(Invoke-Ops -Path "/projects/$projectId/packs")[0]
    Assert-True ((@($shelf.catalog).Count) -ge 2) "raf katalog"
    Assert-True ((@($shelf.installed).Count) -eq 0) "henuz kurulu paket yok"

    Invoke-Ops -Method POST -Path "/projects/$projectId/packs/no-such" -ExpectStatus @(400) | Out-Null
    Assert-True ($script:LastStatus -eq 400) "bilinmeyen paket 400"

    $first = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ([int]$first.created -eq 12) "ilk kur created=$($first.created)"
    Assert-True ([int]$first.skipped -eq 0) "ilk kur skipped=0"

    $again = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ([int]$again.created -eq 0) "tekrar kur created=0"
    Assert-True ([int]$again.skipped -eq 12) "tekrar kur skipped=$($again.skipped)"

    $shelf2 = @(Invoke-Ops -Path "/projects/$projectId/packs")[0]
    $inst = @($shelf2.installed) | Where-Object { $_.packCode -eq "pmo" } | Select-Object -First 1
    Assert-True ($inst.version -eq "1.0.0") "kurulu surum"
    Assert-True (-not $inst.outdated) "outdated degil"

    $q = @(Invoke-Ops -Method POST -Path "/projects/$projectId/packs/quality")[0]
    Assert-True ([int]$q.created -ge 8) "kalite created=$($q.created)"

    $detail = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $names = @($detail.wbs | ForEach-Object { $_.name })
    Assert-True ($names -contains "Kick-off") "pmo wbs duruyor"
    Assert-True (($names -join ",") -match "kontrol") "kalite wbs duruyor"

    $detach = @(Invoke-Ops -Method DELETE -Path "/projects/$projectId/packs/pmo")[0]
    Assert-True ([int]$detach.removed -ge 1) "sok removed=$($detach.removed)"
    $after = @(Invoke-Ops -Path "/projects/$projectId")[0]
    $afterNames = @($after.wbs | ForEach-Object { $_.name })
    Assert-True ($afterNames -notcontains "Kick-off") "kick-off silindi"
    Assert-True (($afterNames -join ",") -match "kontrol") "kalite korundu"

    $shelf3 = @(Invoke-Ops -Path "/projects/$projectId/packs")[0]
    $left = @($shelf3.installed | ForEach-Object { $_.packCode })
    Assert-True ($left -notcontains "pmo") "pmo kaydi yok"
    Assert-True ($left -contains "quality") "kalite kaydi duruyor"

    if (-not $KeepArtifacts) {
        Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200) | Out-Null
        Invoke-Ops -Path "/projects/$projectId" -ExpectStatus @(404) | Out-Null
        Assert-True ($script:LastStatus -eq 404) "proje silindi"
        $projectId = $null
    }
    else {
        Write-Host "KeepArtifacts: proje birakildi $projectId" -ForegroundColor Yellow
    }

    Write-Host "F2-1 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F2-1 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if ($projectId -and -not $KeepArtifacts) {
        try { Invoke-Ops -Method DELETE -Path "/projects/$projectId" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
    }
    throw
}
