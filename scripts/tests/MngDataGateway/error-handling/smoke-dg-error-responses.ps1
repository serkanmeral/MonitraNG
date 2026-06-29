# DG error response smoke tests (Odak test server)
#
# Usage:
#   $token = .\docs\odak\operationcore\scripts\load-operationcore-token.ps1
#   .\scripts\tests\MngDataGateway\error-handling\smoke-dg-error-responses.ps1 -Token $token
#
param(
    [string]$BaseUrl = "http://192.168.20.20:5040/data/api",
    [string]$Token = "",
    [string]$Dataset = "odak_musteriler"
)

$ErrorActionPreference = "Stop"
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }

if ([string]::IsNullOrWhiteSpace($Token)) {
    $loadToken = Join-Path $RepoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    if (-not (Test-Path $loadToken)) {
        $loadToken = Join-Path (Resolve-Path (Join-Path $scriptPath "../../../..")).Path "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $loadToken) {
        $Token = & $loadToken
    }
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host "Token gerekli. -Token veya load-operationcore-token.ps1 calistirin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization = "Bearer $Token"
    "Content-Type" = "application/json"
}

function Invoke-DgExpectStatus {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [int[]]$ExpectedStatus
    )

    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $headers
            SkipHttpErrorCheck = $true
        }
        if ($null -ne $Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        # PowerShell 5 fallback — should not happen on PS7+
        throw
    }

    Write-Host "HTTP $status" -ForegroundColor $(if ($ExpectedStatus -contains $status) { "Green" } else { "Red" })
    if ($content) {
        try {
            $json = $content | ConvertFrom-Json
            $code = $json.error.code
            $msg = $json.error.message
            Write-Host "  code: $code"
            Write-Host "  message: $msg"
            if ($json.error.details) {
                Write-Host "  details: $($json.error.details | ConvertTo-Json -Compress -Depth 5)"
            }
        }
        catch {
            Write-Host "  body: $content"
        }
    }

    if ($ExpectedStatus -notcontains $status) {
        Write-Host "BEKLENMEYEN STATUS (beklenen: $($ExpectedStatus -join ','))" -ForegroundColor Red
        return $false
    }
    return $true
}

$passed = 0
$failed = 0
$results = @()

function Test-Case {
    param([scriptblock]$Block, [string]$Name)
    if (& $Block) { $script:passed++ } else { $script:failed++; $script:results += $Name }
}

# A — dataset not found → 404
Test-Case {
    Invoke-DgExpectStatus -Name "Dataset not found" `
        -Method GET `
        -Url "$BaseUrl/v1/data/__nonexistent_dataset_xyz__?limit=1" `
        -ExpectedStatus @(404)
} -Name "Dataset not found"

# B — validation (invalid kod pattern) → 400
Test-Case {
    Invoke-DgExpectStatus -Name "Validation error (invalid kod pattern)" `
        -Method POST `
        -Url "$BaseUrl/v1/data/$Dataset" `
        -Body @{
            kod = "INVALID"
            unvan = "Smoke Test"
            isMusteri = $true
        } `
        -ExpectedStatus @(400)
} -Name "Validation error"

# C — duplicate probe: same unique kod twice
$suffix = (Get-Random -Minimum 100 -Maximum 999)
$probeKod = "MUS-$suffix"
$probeBody = @{
    kod = $probeKod
    unvan = "DG Smoke $suffix"
    isMusteri = $true
}

Test-Case {
    $first = Invoke-DgExpectStatus -Name "Duplicate probe — first insert" `
        -Method POST `
        -Url "$BaseUrl/v1/data/$Dataset" `
        -Body $probeBody `
        -ExpectedStatus @(200)

    if (-not $first) { return $false }

    Invoke-DgExpectStatus -Name "Duplicate probe — second insert (expect 409 or 400+unique)" `
        -Method POST `
        -Url "$BaseUrl/v1/data/$Dataset" `
        -Body @{
            kod = $probeKod
            unvan = "DG Smoke Duplicate $suffix"
            isMusteri = $true
        } `
        -ExpectedStatus @(400, 409)
} -Name "Duplicate key"

Write-Host "`n--- Ozet ---" -ForegroundColor Cyan
Write-Host "Gecen: $passed  Basarisiz: $failed"
if ($failed -gt 0) {
    Write-Host "Basarisiz: $($results -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "Tum smoke testleri gecti." -ForegroundColor Green
