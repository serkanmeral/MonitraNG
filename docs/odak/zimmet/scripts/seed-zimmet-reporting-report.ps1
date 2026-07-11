# Zimmet — Raporlama seed (kategori + rapor). Idempotent UTF-8 upsert.
#
# Kullanım (repo kökü):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\seed-zimmet-reporting-report.ps1 -SeedFile "zimmet-reporting-urunler.json"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [Parameter(Mandatory = $true)]
    [string]$SeedFile
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
. (Join-Path $scriptDir "lib/ZimmetDgCommon.ps1")

$Ctx = Initialize-ZimmetDgSession -BaseUrl $BaseUrl
$seedPath = if ([IO.Path]::IsPathRooted($SeedFile)) { $SeedFile } else { Join-Path $scriptDir "..\seed\$SeedFile" }
$seedFileResolved = (Resolve-Path $seedPath).Path
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$seedText = [IO.File]::ReadAllText($seedFileResolved, $utf8NoBom)
$now = (Get-Date).ToUniversalTime().ToString("o")

$catDs = "@reporting_categories"
$rptDs = "@reporting_reports"

function Get-JsonObjectSlice {
    param([string]$Text, [string]$PropertyName)
    $marker = '"' + $PropertyName + '"'
    $propIdx = $Text.IndexOf($marker)
    if ($propIdx -lt 0) { throw "Seed icinde '$PropertyName' yok" }
    $braceIdx = $Text.IndexOf('{', $propIdx)
    if ($braceIdx -lt 0) { throw "'$PropertyName' objesi bulunamadi" }
    $depth = 0
    $inString = $false
    $escape = $false
    for ($i = $braceIdx; $i -lt $Text.Length; $i++) {
        $ch = $Text[$i]
        if ($escape) { $escape = $false; continue }
        if ($ch -eq '\') { $escape = $true; continue }
        if ($ch -eq '"') { $inString = -not $inString; continue }
        if ($inString) { continue }
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $Text.Substring($braceIdx, $i - $braceIdx + 1)
            }
        }
    }
    throw "'$PropertyName' objesi kapanmadi"
}

function Get-JsonStringProp {
    param([string]$ObjectJson, [string]$Prop)
    $m = [regex]::Match($ObjectJson, '"' + [regex]::Escape($Prop) + '"\s*:\s*"([^"]*)"')
    if (-not $m.Success) { return $null }
    return $m.Groups[1].Value
}

function Add-TimestampsToJsonObject {
    param([string]$ObjectJson, [string]$CreatedAt, [string]$UpdatedAt)
    $trimmed = $ObjectJson.Trim()
    if (-not $trimmed.EndsWith('}')) { throw "JSON obje degil" }
    $inner = $trimmed.Substring(0, $trimmed.Length - 1).TrimEnd()
    if ($inner -match '"createdAt"\s*:') {
        $inner = [regex]::Replace($inner, '"createdAt"\s*:\s*"[^"]*"', ('"createdAt":"' + $CreatedAt + '"'))
    }
    else {
        $inner = $inner + ',"createdAt":"' + $CreatedAt + '"'
    }
    if ($inner -match '"updatedAt"\s*:') {
        $inner = [regex]::Replace($inner, '"updatedAt"\s*:\s*"[^"]*"', ('"updatedAt":"' + $UpdatedAt + '"'))
    }
    else {
        $inner = $inner + ',"updatedAt":"' + $UpdatedAt + '"'
    }
    return $inner + '}'
}

function Invoke-ZimmetDgRawJson {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Ctx,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$Json
    )
    $bytes = $utf8NoBom.GetBytes($Json)
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $Ctx.Headers
        Body        = $bytes
        ContentType = "application/json; charset=utf-8"
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and $Ctx.IrmParams.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    return Invoke-RestMethod @params
}

function Find-ByLogicalId {
    param([string]$Dataset, [string]$LogicalId)
    $filter = "id:eq:$LogicalId"
    $uri = "$($Ctx.BaseUrl)$($Ctx.DataPath)/$Dataset`?limit=5&filter=$([Uri]::EscapeDataString($filter))"
    return @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $uri))
}

$catJson = Get-JsonObjectSlice -Text $seedText -PropertyName "category"
$rptJson = Get-JsonObjectSlice -Text $seedText -PropertyName "report"
$catId = Get-JsonStringProp -ObjectJson $catJson -Prop "id"
$rptId = Get-JsonStringProp -ObjectJson $rptJson -Prop "id"
$rptTitle = Get-JsonStringProp -ObjectJson $rptJson -Prop "title"
$datasetName = Get-JsonStringProp -ObjectJson $rptJson -Prop "datasetName"
if (-not $catId -or -not $rptId) { throw "Seed category.id / report.id okunamadi" }

# --- Category ---
$existingCat = Find-ByLogicalId -Dataset $catDs -LogicalId $catId
$catCreatedAt = $now
if ($existingCat.Count -gt 0 -and $existingCat[0].createdAt) {
    $catCreatedAt = [string]$existingCat[0].createdAt
}
$catPayload = Add-TimestampsToJsonObject -ObjectJson $catJson -CreatedAt $catCreatedAt -UpdatedAt $now

if ($existingCat.Count -gt 0) {
    $dataId = $existingCat[0].__dataId; if (-not $dataId) { $dataId = $existingCat[0].dataId }
    Invoke-ZimmetDgRawJson -Ctx $Ctx -Method PUT -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$catDs/$dataId" -Json $catPayload | Out-Null
    Write-Host "  SYNC: category ($catId)" -ForegroundColor Yellow
}
else {
    $created = Invoke-ZimmetDgRawJson -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$catDs" -Json $catPayload
    $dataId = Get-ZimmetDataId $created
    Write-Host "  OK: category -> $dataId" -ForegroundColor Green
}

# --- Report ---
$existingRpt = Find-ByLogicalId -Dataset $rptDs -LogicalId $rptId
$rptCreatedAt = $now
if ($existingRpt.Count -gt 0 -and $existingRpt[0].createdAt) {
    $rptCreatedAt = [string]$existingRpt[0].createdAt
}
$rptPayload = Add-TimestampsToJsonObject -ObjectJson $rptJson -CreatedAt $rptCreatedAt -UpdatedAt $now

if ($existingRpt.Count -gt 0) {
    $dataId = $existingRpt[0].__dataId; if (-not $dataId) { $dataId = $existingRpt[0].dataId }
    Invoke-ZimmetDgRawJson -Ctx $Ctx -Method PUT -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$rptDs/$dataId" -Json $rptPayload | Out-Null
    Write-Host "  SYNC: report $rptId" -ForegroundColor Yellow
}
else {
    $created = Invoke-ZimmetDgRawJson -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$rptDs" -Json $rptPayload
    $dataId = Get-ZimmetDataId $created
    Write-Host "  OK: report $rptId -> $dataId" -ForegroundColor Green
}

Write-Host ""
Write-Host "Tamam. Browse: /apps/reporting/browse  ->  Zimmet -> $rptTitle" -ForegroundColor Cyan
Write-Host "  categoryId=$catId  reportId=$rptId  dataset=$datasetName  parameters=[]" -ForegroundColor Gray
