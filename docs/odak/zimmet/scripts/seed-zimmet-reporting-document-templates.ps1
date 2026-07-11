# Zimmet DI belge şablonları (personel dökümü + teslim/iade tutanakları).
#
# Kullanım (repo kökü):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\seed-zimmet-reporting-document-templates.ps1
#   .\docs\odak\zimmet\scripts\seed-zimmet-reporting-document-templates.ps1 -Replace

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$Replace = $false
)

$ErrorActionPreference = "Stop"
$token = $env:DI_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    $oc = Join-Path $env:TEMP "operationcore_dg_token.txt"
    if (Test-Path $oc) { $token = (Get-Content $oc -Raw).Trim() }
}
$reportingSeed = Join-Path $PSScriptRoot "..\..\reporting_services\scripts\seed-reporting-document-templates.ps1"
& $reportingSeed -BaseUrl $BaseUrl -Token $token -SeedFile "docs/odak/zimmet/seed/zimmet-reporting-document-templates.json" -Replace:$Replace
