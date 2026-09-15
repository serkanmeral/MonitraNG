# Smoke: F5-1 paket kokeni, icerik ozeti, imzasiz ucuncu taraf (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$pmoPath = Join-Path $repoRoot "MngOperations/Core/MngOperations.Application/Packs/pmo.json"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F51-$stamp"

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
        [string]$RawBody = $null,
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
    if (-not [string]::IsNullOrWhiteSpace($RawBody)) {
        $params.ContentType = "application/json"
        $params.Body = $RawBody
    }
    elseif ($null -ne $Body) {
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

function Get-DgItems {
    param([string]$Collection, [string]$Filter)
    $uri = "$Gateway/data/api/v1/data/$Collection`?limit=50"
    if ($Filter) { $uri += "&filter=" + [Uri]::EscapeDataString($Filter) }
    $status = 0
    $result = Invoke-RestMethod -Uri $uri -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck -StatusCodeVariable status -TimeoutSec 60
    if ([int]$status -ge 400) { return @() }
    if ($null -eq $result) { return @() }
    if ($result -is [Array]) { return @($result) }
    foreach ($prop in @("data", "Data", "items", "Items")) {
        if ($null -ne $result.$prop) { return @($result.$prop) }
    }
    return @($result)
}

function Remove-PackWorkspace {
    param([string]$WorkspaceId)
    if ([string]::IsNullOrWhiteSpace($WorkspaceId)) { return }
    foreach ($ds in @("op_boards", "op_forms", "op_profiles", "op_work_item_types", "op_state_flows")) {
        foreach ($row in @(Get-DgItems -Collection $ds -Filter "workspaceId:eq:$WorkspaceId")) {
            $id = $row.__dataId
            if (-not $id) { $id = $row.dataId }
            if (-not $id) { continue }
            try {
                Invoke-RestMethod -Uri "$Gateway/data/api/v1/data/$ds/$id" -Method DELETE -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 30 | Out-Null
            }
            catch { }
        }
    }
    try {
        Invoke-RestMethod -Uri "$Gateway/data/api/v1/data/op_workspaces/$WorkspaceId" -Method DELETE -Headers $script:Headers -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 30 | Out-Null
    }
    catch { }
}

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$projectIds = @()
$workspaceIds = @()

Write-Host "F5-1 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $catalog = @(Invoke-Ops -Path "/job-packs")[0]
    $packs = @($catalog)
    Assert-True ($packs.Count -eq 7) "katalog sayisi=$($packs.Count)"

    $hashes = @{}
    foreach ($pack in $packs) {
        $origin = [string]$pack.origin
        $sha = [string]$pack.contentSha256
        Assert-True ($origin -eq "firstParty") "$($pack.code) origin=firstParty"
        Assert-True ([string]$pack.publisher -eq "MonitraNG") "$($pack.code) publisher=MonitraNG"
        Assert-True ($pack.verified -eq $true) "$($pack.code) verified"
        Assert-True ($pack.canApply -eq $true) "$($pack.code) canApply"
        Assert-True ($sha -match "^[0-9a-f]{64}$") "$($pack.code) sha256"
        Assert-True (-not $hashes.ContainsKey($sha)) "$($pack.code) sha256 benzersiz"
        $hashes[$sha] = $pack.code
    }

    $evil = @(Invoke-Ops -Method POST -Path "/job-packs/inspect" -RawBody '{"code":"evil-pack","name":"Evil","origin":"thirdParty","publisher":"Other"}')[0]
    Assert-True ($evil.canApply -eq $false) "ucuncu taraf canApply=false"
    Assert-True ($evil.verified -eq $false) "ucuncu taraf verified=false"
    Assert-True ($evil.fromCatalog -eq $false) "ucuncu taraf fromCatalog=false"
    Assert-True ($evil.origin -eq "thirdParty") "ucuncu taraf origin"
    Assert-True ($evil.reason -eq "PACK_UNTRUSTED") "ucuncu taraf reason=$($evil.reason)"

    $spoof = @(Invoke-Ops -Method POST -Path "/job-packs/inspect" -RawBody '{"code":"pmo","name":"PMO spoof","origin":"firstParty"}')[0]
    Assert-True ($spoof.canApply -eq $false) "firstParty sahte canApply=false"
    Assert-True ($spoof.fromCatalog -eq $false) "firstParty sahte fromCatalog=false"

    Assert-True (Test-Path $pmoPath) "pmo.json var"
    $pmoRaw = Get-Content -Path $pmoPath -Raw -Encoding UTF8
    $matched = @(Invoke-Ops -Method POST -Path "/job-packs/inspect" -RawBody $pmoRaw)[0]
    Assert-True ($matched.fromCatalog -eq $true) "pmo.json fromCatalog=true"
    Assert-True ($matched.canApply -eq $true) "pmo.json canApply=true"
    Assert-True ($matched.verified -eq $true) "pmo.json verified"

    $created = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code     = $code
            name     = "F5-1 trust $stamp"
            status   = "active"
            packCode = "pmo"
        } -ExpectStatus @(201, 200))[0]
    $projectId = [string]$created.id
    $projectIds += $projectId
    $wsId = [string]$created.workspaceId
    Assert-True ($projectId) "packCode ile create id=$projectId"
    Assert-True ($wsId) "guvenilen pmo workspaceId=$wsId"
    $workspaceIds += $wsId

    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null
        }
        $projectIds = @()
        foreach ($id in $workspaceIds) { Remove-PackWorkspace $id }
        $workspaceIds = @()
        Write-Host "  cleanup OK" -ForegroundColor Green
    }
    else {
        Write-Host "KeepArtifacts: projeler birakildi $($projectIds -join ',')" -ForegroundColor Yellow
    }

    Write-Host "F5-1 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F5-1 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        foreach ($id in $projectIds) {
            try { Invoke-Ops -Method DELETE -Path "/projects/$id" -ExpectStatus @(204, 200, 404) | Out-Null } catch { }
        }
        foreach ($id in $workspaceIds) { try { Remove-PackWorkspace $id } catch { } }
    }
    throw
}
