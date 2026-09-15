# Her iş paketi türü için birer örnek proje (idempotent).
# POST /projects + packCode → WBS + DI klasörleri + OC workspace/işler.
#
#   pwsh .\docs\odak\project_management\scripts\seed-pack-demo-projects.ps1
#   pwsh .\docs\odak\project_management\scripts\seed-pack-demo-projects.ps1 -WhatIf
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [string]$CodePrefix = "SEED",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

function Get-Token {
    if (Test-Path $loadToken) {
        $fresh = & $loadToken -AutoRefresh
        if ($fresh) { return $fresh.Trim() }
    }
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
        TimeoutSec           = 300
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
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

function Get-Items($response) {
    if ($null -eq $response) { return @() }
    if ($null -ne $response.items) { return @($response.items) }
    if ($null -ne $response.Items) { return @($response.Items) }
    if ($response -is [Array]) { return @($response) }
    return @($response)
}

function Get-Prop($row, [string[]]$names) {
    foreach ($n in $names) {
        $v = $row.$n
        if (-not [string]::IsNullOrWhiteSpace([string]$v)) { return [string]$v }
    }
    return ""
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0

Write-Host "Pack demo projects seed  gateway=$Gateway  prefix=$CodePrefix" -ForegroundColor Cyan

$packsRaw = Invoke-Ops -Path "/job-packs"
$packs = @($packsRaw)
if ($packs.Count -eq 1 -and $null -ne $packs[0].items) { $packs = @($packs[0].items) }
elseif ($packs.Count -eq 1 -and $null -ne $packs[0].Items) { $packs = @($packs[0].Items) }
if ($packs.Count -eq 0) { throw "Job pack katalogu bos." }

$existingRaw = Invoke-Ops -Path "/projects"
$existing = @($existingRaw)
if ($existing.Count -eq 1 -and $null -ne $existing[0].items) { $existing = @($existing[0].items) }
elseif ($existing.Count -eq 1 -and $null -ne $existing[0].Items) { $existing = @($existing[0].Items) }
$byCode = @{}
foreach ($p in $existing) {
    $c = Get-Prop $p @("code", "Code")
    if ($c) { $byCode[$c.ToUpperInvariant()] = $p }
}

$created = 0
$skipped = 0
$results = @()

foreach ($pack in ($packs | Sort-Object { Get-Prop $_ @("code", "Code") })) {
    $packCode = Get-Prop $pack @("code", "Code")
    $packName = Get-Prop $pack @("name", "Name")
    if (-not $packCode) { continue }

    $projectCode = "$CodePrefix-$($packCode.ToUpperInvariant())"
    $projectName = "Ornek — $packName"
    $key = $projectCode.ToUpperInvariant()

    Write-Host ""
    Write-Host "=== $packCode ($packName) -> $projectCode ===" -ForegroundColor Cyan

    if ($byCode.ContainsKey($key)) {
        $row = $byCode[$key]
        $id = Get-Prop $row @("id", "Id")
        Write-Host "  SKIP mevcut id=$id" -ForegroundColor Yellow
        $skipped++
        $results += [pscustomobject]@{
            PackCode    = $packCode
            ProjectCode = $projectCode
            ProjectId   = $id
            Action      = "skip"
        }
        continue
    }

    if ($WhatIf) {
        Write-Host "  WhatIf create name='$projectName'" -ForegroundColor Yellow
        $created++
        $results += [pscustomobject]@{
            PackCode    = $packCode
            ProjectCode = $projectCode
            ProjectId   = ""
            Action      = "whatif"
        }
        continue
    }

    $proj = @(Invoke-Ops -Method POST -Path "/projects" -Body @{
            code        = $projectCode
            name        = $projectName
            description = "Demo seed: $packName ($packCode) is paketi. UI kontrolu icin."
            status      = "active"
            packCode    = $packCode
        } -ExpectStatus @(201, 200))[0]

    $id = Get-Prop $proj @("id", "Id")
    $ws = Get-Prop $proj @("workspaceId", "WorkspaceId")
    if (-not $id) { throw "Proje id donmedi ($projectCode)" }

    $detail = @(Invoke-Ops -Path "/projects/$id")[0]
    $wbsCount = 0
    if ($detail.wbs) { $wbsCount = @($detail.wbs).Count }
    elseif ($detail.Wbs) { $wbsCount = @($detail.Wbs).Count }

    Write-Host "  OK id=$id workspace=$ws wbs=$wbsCount" -ForegroundColor Green
    $created++
    $results += [pscustomobject]@{
        PackCode    = $packCode
        ProjectCode = $projectCode
        ProjectId   = $id
        Action      = "create"
        WorkspaceId = $ws
        WbsCount    = $wbsCount
    }
}

Write-Host ""
Write-Host "Ozet: create=$created skip=$skipped total=$($results.Count)" -ForegroundColor Green
$results | Format-Table -AutoSize
Write-Host "Pack demo projects seed OK" -ForegroundColor Green
