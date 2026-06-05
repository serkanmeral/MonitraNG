# Operation Core token — Production (192.168.20.8)
$DomainName    = "odak"
$Username      = "odak_admin"
$Password      = "Admin123!"
$KeeperBaseUrl = "http://192.168.20.8:5040"
$KeeperPath    = "/keeper/api/auth/token"
$TokenFile     = "$env:TEMP\operationcore_dg_token_prod.txt"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$getTokenScript = Join-Path $repoRoot "scripts/tests/MngDataGateway/auth/get-token.ps1"
if (-not (Test-Path $getTokenScript)) {
    Write-Host "get-token.ps1 bulunamadi: $getTokenScript" -ForegroundColor Red
    exit 1
}

& $getTokenScript `
    -KeeperBaseUrl $KeeperBaseUrl `
    -KeeperPath $KeeperPath `
    -DomainName $DomainName `
    -Username $Username `
    -Password $Password `
    -TokenFile $TokenFile

if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) { exit $LASTEXITCODE }
