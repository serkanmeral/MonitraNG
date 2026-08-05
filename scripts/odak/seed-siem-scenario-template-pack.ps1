param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$PackageId = "siem-product-v2",
    [string]$ImportKey = $env:MNG_ALARM_SCENARIO_IMPORT_KEY,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ImportKey)) {
    throw "ImportKey veya MNG_ALARM_SCENARIO_IMPORT_KEY zorunludur."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manifestPath = Join-Path $repoRoot "tests/fixtures/siem/scenario_templates/packages/$PackageId/manifest.json"
if (-not (Test-Path $manifestPath)) { throw "Manifest bulunamadi: $manifestPath" }

$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
if ($manifest.packageId -ne $PackageId) {
    throw "Manifest packageId=$($manifest.packageId), beklenen=$PackageId"
}

if ($DryRun) {
    Write-Host "DRY-RUN package=$($manifest.packageId) version=$($manifest.packageVersion) templates=$(@($manifest.templates).Count)" -ForegroundColor Yellow
    exit 0
}

$tokenScript = Join-Path $repoRoot "scripts/tests/MngDataGateway/auth/get-token.ps1"
$null = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$tokenFile = Join-Path $env:TEMP "serkan_token.txt"
if (-not (Test-Path $tokenFile)) { throw "Token dosyasi yok: $tokenFile" }
$token = (Get-Content -Path $tokenFile -Raw).Trim()

$headers = @{
    Authorization = "Bearer $token"
    "X-Domain-Name" = $Domain
    "X-Scenario-Package-Key" = $ImportKey
    "Content-Type" = "application/json"
}
$uri = "$Gateway/alarm/api/v1/scenarios/packages/import"
$result = Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body ($manifest | ConvertTo-Json -Depth 30)

Write-Host "Scenario template paketi tamamlandi: created=$($result.created) skipped=$($result.skipped)" -ForegroundColor Green
exit 0
