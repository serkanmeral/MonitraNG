# Reproduce from-reference 500 on prod
param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$SampleDocx = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
if (-not $SampleDocx) {
    $SampleDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-23-202.docx"
}
$tokenFile = "$env:TEMP\operationcore_dg_token_prod.txt"
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1"
if (-not (Test-Path $tokenFile)) { & $loadToken | Out-Null }
$token = (Get-Content $tokenFile -Raw).Trim()

# CoC category from seed
$categoryId = "3c92d366-5e7b-4d11-a479-a3bb28629f84"
$bytes = [IO.File]::ReadAllBytes($SampleDocx)
$b64 = [Convert]::ToBase64String($bytes)
$bodyObj = @{
    categoryId = $categoryId
    fileName   = [IO.Path]::GetFileName($SampleDocx)
    name       = "Probe CoC Template"
    content    = $b64
    size       = $bytes.Length
}
$json = $bodyObj | ConvertTo-Json -Compress
$utf8 = [Text.UTF8Encoding]::new($false)

Write-Host "POST from-reference ($([math]::Round($bytes.Length/1KB, 1)) KB docx, b64 $([math]::Round($json.Length/1KB, 1)) KB json)" -ForegroundColor Cyan

try {
    $r = Invoke-WebRequest -Uri "$Gateway/documents/api/v1/templates/from-reference" `
        -Method POST `
        -Headers @{ Authorization = "Bearer $token"; "Content-Type" = "application/json; charset=utf-8" } `
        -Body ([Text.Encoding]::UTF8.GetBytes($json)) `
        -SkipHttpErrorCheck
    Write-Host "HTTP $($r.StatusCode)" -ForegroundColor $(if ($r.StatusCode -lt 300) { 'Green' } else { 'Red' })
    Write-Host $r.Content
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
}
