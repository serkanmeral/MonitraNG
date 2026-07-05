# Regenerate letterhead design DOCX from current catalog settings (skeleton: header + empty footer table).
#
#   .\docs\odak\document_intelligence\scripts\regenerate-letterhead-design.ps1 -Code ODK_TST_1
#   .\docs\odak\document_intelligence\scripts\regenerate-letterhead-design.ps1 -Id 84c4eaac-e0fc-4a12-9f51-1ccc4d224798

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [string]$Code = "",
    [string]$Id = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$loadToken = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
$token = $Token
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadToken)) {
    $token = & $loadToken
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok." -ForegroundColor Red
    exit 1
}
$token = $token.Trim()
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
$lhBase = "$BaseUrl/documents/api/v1/letterheads"
$dgBase = "$BaseUrl/data/api/v1/data/dm_letterheads"

if ([string]::IsNullOrWhiteSpace($Id)) {
    if ([string]::IsNullOrWhiteSpace($Code)) { throw "Code veya Id gerekli." }
    $list = Invoke-RestMethod -Uri $lhBase -Headers $headers -Method GET
    $match = $list.items | Where-Object { $_.code -eq $Code } | Select-Object -First 1
    if (-not $match) { throw "Antet bulunamadi: $Code" }
    $Id = $match.id
}

Write-Host "Regenerate design -> $Id" -ForegroundColor Cyan
$row = Invoke-RestMethod -Uri "$dgBase/$Id" -Headers $headers -Method GET
$clearPayload = @{
    name = $row.name
    code = $row.code
    description = $row.description
    isDefault = $row.isDefault
    isActive = $row.isActive
    letterheadJson = $row.letterheadJson
    settingsJson = $row.settingsJson
    designStoragePath = $null
    designFileName = $null
    designFile = $null
    updatedBy = "regenerate-script"
    updatedAt = (Get-Date).ToUniversalTime().ToString("o")
}
Invoke-RestMethod -Uri "$dgBase/$Id" -Headers $headers -Method PUT -Body ($clearPayload | ConvertTo-Json -Depth 10 -Compress) | Out-Null
Write-Host "Design file cleared." -ForegroundColor Green

$session = Invoke-RestMethod -Uri "$lhBase/$Id/design-session" -Headers $headers -Method GET
Write-Host "designFooterSource=$($session.designFooterSource)" -ForegroundColor Green
Write-Host "OK skeleton created via design-session." -ForegroundColor Green
