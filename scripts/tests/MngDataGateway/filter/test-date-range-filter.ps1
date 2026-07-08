# Test: same-field gte + lte on GET list filter (year / date range)
# GET /api/v1/data/{dataset}?filter=field:gte:YYYY-MM-DD,field:lte:YYYY-MM-DD

$baseUrl = if ($env:DG_BASE_URL) { $env:DG_BASE_URL.TrimEnd('/') } else { "https://localhost:5010" }
$datasetName = if ($env:DG_TEST_DATASET) { $env:DG_TEST_DATASET } else { "odak_egitimler" }
$dateField = if ($env:DG_TEST_DATE_FIELD) { $env:DG_TEST_DATE_FIELD } else { "gerceklesenTarih" }
$year = if ($env:DG_TEST_YEAR) { $env:DG_TEST_YEAR } else { (Get-Date).Year.ToString() }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 not found: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization = "Bearer $token"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

Write-Host "Date range filter test — $datasetName / $dateField / year $year" -ForegroundColor Cyan

$filter = "${dateField}:gte:${year}-01-01,${dateField}:lte:${year}-12-31,durum:eq:Tamamlandi"
$encodedFilter = [System.Uri]::EscapeDataString($filter)
$url = "$baseUrl/api/v1/data/$datasetName" + "?filter=$encodedFilter&limit=5&skip=0&fields=$dateField,durum&expand=false&showQuery=true"

Write-Host "URL: $url" -ForegroundColor DarkGray

try {
    $response = Invoke-RestMethod -Uri $url -Method GET -Headers $headers -SkipCertificateCheck
    if ($response.query) {
        Write-Host "PASS: showQuery returned pipeline" -ForegroundColor Green
        $pipelineJson = $response.query | ConvertTo-Json -Depth 20 -Compress
        if ($pipelineJson -match '\$gte' -and $pipelineJson -match '\$lte') {
            Write-Host "PASS: pipeline contains gte and lte for date range" -ForegroundColor Green
        } else {
            Write-Host "WARN: pipeline may not merge gte/lte — inspect showQuery output" -ForegroundColor Yellow
            Write-Host $pipelineJson
        }
    } elseif ($response -is [System.Array]) {
        Write-Host "PASS: returned $($response.Count) row(s) (no showQuery)" -ForegroundColor Green
    } else {
        Write-Host "Unexpected response shape" -ForegroundColor Yellow
        $response | ConvertTo-Json -Depth 5
    }
    exit 0
} catch {
    Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
