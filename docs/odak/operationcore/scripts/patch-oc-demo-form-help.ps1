# OC Demo Create Form — layout.helpMarkdown senkronu (tek dosyadan)
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\patch-oc-demo-form-help.ps1
#   .\docs\odak\operationcore\scripts\patch-oc-demo-form-help.ps1 -ReloadMetadataCache

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$UseGateway = $true,
    [switch]$ReloadMetadataCache = $false,
    [string]$FormId = "",
    [string]$HelpFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($HelpFile)) {
    $HelpFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/oc_demo_create_form_help.md"
}
if (-not (Test-Path $HelpFile)) { throw "Yardim dosyasi yok: $HelpFile" }

$helpMarkdown = (Get-Content $HelpFile -Raw -Encoding UTF8).Trim()
if (-not $helpMarkdown) { throw "Yardim dosyasi bos: $HelpFile" }

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$demoTag = "OC Demo"
$formName = "$demoTag Create Form"

$loadTokenScript = Join-Path $scriptDir "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
    $irmParams.SkipCertificateCheck = $true
}

function Invoke-Dg {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body = $null
    )
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $headers
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
        $params.ContentType = "application/json"
    }
    return Invoke-RestMethod @params
}

function Get-Items {
    param($Response)
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items", "Items", "results", "Results")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return $items }
            return @($items)
        }
    }
    return @($Response)
}

if ([string]::IsNullOrWhiteSpace($FormId)) {
    $filter = "name:eq:$formName"
    $items = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_forms?limit=5&filter=$([Uri]::EscapeDataString($filter))"))
    if ($items.Count -eq 0) { throw "Form bulunamadi: $formName" }
    $FormId = $items[0].__dataId
    if (-not $FormId) { $FormId = $items[0].dataId }
}

$formRaw = Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/op_forms/$FormId"
$form = $formRaw.data
if (-not $form) { $form = $formRaw }

$layout = @{}
if ($form.layout) {
    if ($form.layout -is [string]) {
        $layout = $form.layout | ConvertFrom-Json -AsHashtable
    }
    elseif ($form.layout -is [hashtable]) {
        $layout = $form.layout
    }
    else {
        $layout = $form.layout | ConvertTo-Json -Depth 20 | ConvertFrom-Json -AsHashtable
    }
}

$layout["helpMarkdown"] = $helpMarkdown

Write-Host "PATCH op_forms/$FormId layout.helpMarkdown ($($helpMarkdown.Length) karakter)..." -ForegroundColor Cyan
Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/op_forms/$FormId" -Body @{
    layout = $layout
} | Out-Null
Write-Host "OK: $formName yardim metni guncellendi." -ForegroundColor Green

if ($ReloadMetadataCache) {
    $wsId = $null
    $wsRaw = $form.workspaceId
    if ($wsRaw -is [string]) { $wsId = $wsRaw.Trim() }
    elseif ($wsRaw) {
        $wsId = $wsRaw.__dataId
        if (-not $wsId) { $wsId = $wsRaw.dataId }
    }
    if ($wsId) {
        Write-Host "MO metadata cache reload (workspace $wsId)..." -ForegroundColor Yellow
        try {
            $moParams = @{ Uri = "$MoBaseUrl/api/v1/workspaces/$wsId/metadata-cache/reload"; Method = "POST"; Headers = $headers; ErrorAction = "Stop" }
            if ($MoBaseUrl.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
                $moParams.SkipCertificateCheck = $true
            }
            Invoke-RestMethod @moParams | Out-Null
            Write-Host "OK: cache reload" -ForegroundColor Green
        }
        catch {
            Write-Host "WARN: cache reload: $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
    }
}

Write-Host "Tamam. Yeni is modalinda Yardim butonunu kontrol edin." -ForegroundColor Cyan
