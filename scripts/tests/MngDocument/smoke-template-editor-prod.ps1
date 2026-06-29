# Smoke: E1 blank template + Collabora editor-session (WOPI)
param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt",
    [string]$CategoryId = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"

$token = if (Test-Path $TokenFile) {
    (Get-Content $TokenFile -Raw).Trim()
} else {
    & $loadToken
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Prod token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }

if ([string]::IsNullOrWhiteSpace($CategoryId)) {
    $tree = Invoke-RestMethod -Uri "$Gateway/documents/api/v1/template-categories/tree" -Headers $headers -TimeoutSec 30
    $first = $null
    function Find-FirstCategory($nodes) {
        foreach ($n in $nodes) {
            if ($n.id) { return $n }
            if ($n.children) {
                $found = Find-FirstCategory $n.children
                if ($found) { return $found }
            }
        }
        return $null
    }
    $first = Find-FirstCategory $tree
    if (-not $first) {
        Write-Host "SKIP: Kategori yok; once Belge Tasarimcisi'ndan kategori olusturun." -ForegroundColor Yellow
        exit 0
    }
    $CategoryId = $first.id
    Write-Host "Ilk kategori kullaniliyor: $CategoryId ($($first.name))" -ForegroundColor Gray
}

$blankBody = @{ categoryId = $CategoryId; name = "E1 Smoke $(Get-Date -Format 'yyyyMMdd-HHmmss')" } | ConvertTo-Json
$created = Invoke-RestMethod -Uri "$Gateway/documents/api/v1/templates/blank" -Method POST -Headers $headers -Body $blankBody -ContentType "application/json" -TimeoutSec 60

if (-not $created.id) {
    Write-Host "FAIL: blank template olusturulamadi" -ForegroundColor Red
    exit 1
}

Write-Host "OK blank template: $($created.id) ($($created.name))" -ForegroundColor Green

$sessionUri = "$Gateway/documents/api/v1/templates/$([uri]::EscapeDataString($created.id))/editor-session"
$session = Invoke-RestMethod -Uri $sessionUri -Headers $headers -TimeoutSec 30

if ([string]::IsNullOrWhiteSpace($session.editorUrl)) {
    Write-Host "FAIL: editorUrl bos" -ForegroundColor Red
    exit 1
}

if ($session.editorUrl -notmatch "cool\.html") {
    Write-Host "FAIL: editorUrl Collabora cool.html icermiyor: $($session.editorUrl)" -ForegroundColor Red
    exit 1
}

Write-Host "OK editor-session" -ForegroundColor Green
Write-Host "  editorUrl: $($session.editorUrl.Substring(0, [Math]::Min(120, $session.editorUrl.Length)))..." -ForegroundColor Cyan
Write-Host "  wopiSrc: $($session.wopiSrc)" -ForegroundColor Cyan
Write-Host "E1 editor smoke tamam (templateId=$($created.id))." -ForegroundColor Cyan
