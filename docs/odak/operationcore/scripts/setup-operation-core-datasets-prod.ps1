# Operation Core datasets — Production (192.168.20.8)
#   .\get-operationcore-token-prod.ps1
#   .\setup-operation-core-datasets-prod.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true
)

$setupScript = Join-Path $PSScriptRoot "setup-operation-core-datasets.ps1"
$prodTokenLoader = Join-Path $PSScriptRoot "load-operationcore-token-prod.ps1"

& $setupScript `
    -BaseUrl $BaseUrl `
    -UseGateway:$UseGateway `
    -LoadTokenScript $prodTokenLoader

if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) { exit $LASTEXITCODE }
