# Abt alani — stok, depo, demirbas, zimmet kurulumu.
# Odak (192.168.20.20) ve proline adresine gitmez.
# Demo ikinci calismada ayni baslikli isi yeniden acmaz.
#
# Repo kokunden:
#   .\docs\ankarabt\stok\scripts\setup-abt-stok.ps1
#   .\docs\ankarabt\stok\scripts\setup-abt-stok.ps1 -SkipDemo

param(
    [string]$BaseUrl = "http://192.168.1.249:5040",
    [string]$DomainName = "abt",
    [string]$Username = "abt_admin",
    [string]$Password = "Admin123!",
    [switch]$SkipDemo = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$zimmetScripts = Join-Path $repoRoot "docs/odak/zimmet/scripts"
$seedFile = Join-Path $repoRoot "docs/ankarabt/stok/seed/abt_master_seed.json"
$masterIds = Join-Path $repoRoot "docs/ankarabt/stok/seed/abt_master_ids.json"
$ocIds = Join-Path $repoRoot "docs/ankarabt/stok/seed/abt_oc_ids.json"
$tokenFile = Join-Path $env:TEMP "operationcore_dg_token.txt"
$getToken = Join-Path $repoRoot "scripts/tests/MngDataGateway/auth/get-token.ps1"
$moBase = "$BaseUrl/operations"

function Update-AbtToken {
    & $getToken `
        -KeeperBaseUrl $BaseUrl `
        -KeeperPath "/keeper/api/auth/token" `
        -DomainName $DomainName `
        -Username $Username `
        -Password $Password `
        -TokenFile $tokenFile | Out-Null
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Abt token alinamadi (exit $LASTEXITCODE)" }
}

Write-Host "`n=== Abt stok kurulumu ===" -ForegroundColor Magenta
Write-Host "Gateway: $BaseUrl  Domain: $DomainName" -ForegroundColor Magenta

Update-AbtToken
& (Join-Path $zimmetScripts "setup-zimmet-datasets-and-forms.ps1") -BaseUrl $BaseUrl
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Dataset/form kurulumu basarisiz" }

Update-AbtToken
& (Join-Path $zimmetScripts "seed-zimmet-master-data.ps1") -BaseUrl $BaseUrl -SeedFile $seedFile -OutputFile $masterIds
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Master seed basarisiz" }

Update-AbtToken
& (Join-Path $zimmetScripts "seed-operation-core-zimmet.ps1") `
    -BaseUrl $BaseUrl `
    -MoBaseUrl $moBase `
    -MasterIdsFile $masterIds `
    -OutputFile $ocIds `
    -ReloadMetadataCache
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Operation Core seed basarisiz" }

Update-AbtToken
foreach ($report in @(
    "seed-zimmet-reporting-demirbaslar.ps1",
    "seed-zimmet-reporting-personel.ps1",
    "seed-zimmet-reporting-garanti.ps1"
)) {
    & (Join-Path $zimmetScripts $report) -BaseUrl $BaseUrl
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Rapor seed basarisiz: $report" }
}
& (Join-Path $zimmetScripts "seed-zimmet-reporting-document-templates.ps1") -BaseUrl $BaseUrl
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Belge sablon seed basarisiz" }

Update-AbtToken
& (Join-Path $zimmetScripts "patch-zimmet-side-menu.ps1") -BaseUrl $BaseUrl
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Yan menu basarisiz" }

if (-not $SkipDemo) {
    Update-AbtToken
    & (Join-Path $scriptDir "seed-abt-stok-demo.ps1") -BaseUrl $BaseUrl -MasterIdsFile $masterIds -OcIdsFile $ocIds
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Demo seed basarisiz" }
}

Write-Host "`nAbt stok kurulumu tamam." -ForegroundColor Green
