# Prod: test ile ayni DI sablon + kategori seed (tek seferlik migrasyon)
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $repoRoot

$base = "http://192.168.20.8:5040"
$env:DI_TOKEN = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1")
if ([string]::IsNullOrWhiteSpace($env:DI_TOKEN)) { throw "Prod token alinamadi" }

$diScripts = Join-Path $repoRoot "docs/odak/document_intelligence/scripts"

Write-Host "=== CoC seed ===" -ForegroundColor Cyan
& (Join-Path $diScripts "seed-designer-template-coc-standard.ps1") -BaseUrl $base -Replace -Token $env:DI_TOKEN

Write-Host "`n=== Line Activity seed ===" -ForegroundColor Cyan
& (Join-Path $diScripts "seed-designer-template-line-activity-standard.ps1") -BaseUrl $base -Replace -Token $env:DI_TOKEN

Write-Host "`n=== CoC publish ===" -ForegroundColor Cyan
& (Join-Path $diScripts "patch-coc-standard-profile-test.ps1") -BaseUrl $base -Token $env:DI_TOKEN

Write-Host "`n=== Line Activity publish ===" -ForegroundColor Cyan
& (Join-Path $diScripts "patch-line-activity-standard-test.ps1") -BaseUrl $base -Token $env:DI_TOKEN

Write-Host "`n=== Dogrulama ===" -ForegroundColor Cyan
$h = @{ Authorization = "Bearer $($env:DI_TOKEN.Trim())" }
$list = Invoke-RestMethod -Uri "$base/documents/api/v1/templates" -Headers $h -TimeoutSec 60
foreach ($t in @($list.items)) {
    Write-Host ("  {0} ({1}) -> {2}" -f $t.name, ($t.code ?? "-"), $t.status) -ForegroundColor Green
}
