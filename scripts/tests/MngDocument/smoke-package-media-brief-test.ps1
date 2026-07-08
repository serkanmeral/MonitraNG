# PACKAGE-BRIEF medya paketi smoke — dashboard XLSX + müşteri sunumu PPTX + writeback + chart (Yol A)
#
# Kullanım:
#   .\scripts\tests\MngDocument\smoke-package-media-brief-test.ps1 -PackageId "<uuid>"
#
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$PackageId = "2d8aeb0e-6f67-4f3a-a578-21cff682ec17",
    [string]$PackagesDataset = "odak_is_paketleri",
    [string]$Token = $env:DI_TOKEN,
    [switch]$SkipWritebackCheck = $false,
    [switch]$SkipChartCheck = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../..")).Path
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = $Token
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadTokenScript)) {
    $token = & $loadTokenScript
}
if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }
$token = $token.Trim()

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type"  = "application/json"
}
$generateBase = "$BaseUrl/documents/api/v1/generate"
$utf8 = [System.Text.Encoding]::UTF8

function Invoke-Generate {
    param([string]$ProfileCode, [string]$TemplateCode, [string]$Label)
    $body = @{
        profileCode  = $ProfileCode
        templateCode = $TemplateCode
        context      = @{
            type = "odak.siparis.package"
            id   = $PackageId
        }
    }
    $json = $body | ConvertTo-Json -Depth 6 -Compress
    $bytes = $utf8.GetBytes($json)
    Write-Host "POST $Label ($ProfileCode)..." -ForegroundColor Cyan
    $result = Invoke-RestMethod -Uri $generateBase -Method POST -Headers $headers -Body $bytes -ContentType "application/json; charset=utf-8" -TimeoutSec 180
    Write-Host "OK $Label resourceId=$($result.resourceId) file=$($result.fileName)" -ForegroundColor Green
    if ($result.undefinedParameterKeys -and @($result.undefinedParameterKeys).Count -gt 0) {
        Write-Host "WARN undefined: $($result.undefinedParameterKeys -join ', ')" -ForegroundColor Yellow
    }
    if ($result.unresolvedParameterKeys -and @($result.unresolvedParameterKeys).Count -gt 0) {
        Write-Host "WARN unresolved: $($result.unresolvedParameterKeys -join ', ')" -ForegroundColor Yellow
    }
    return $result
}

function Assert-PackageWriteback {
    param(
        [string]$FieldName,
        [string]$ExpectedResourceId
    )
    $uri = "$BaseUrl/data/api/v1/data/$([Uri]::EscapeDataString($PackagesDataset))/$([Uri]::EscapeDataString($PackageId))"
    $pkg = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET
    $actual = [string]$pkg.$FieldName
    if ([string]::IsNullOrWhiteSpace($actual)) {
        throw "Writeback bos: $FieldName"
    }
    if ($actual -ne $ExpectedResourceId) {
        Write-Host "WARN writeback $FieldName=$actual (beklenen $ExpectedResourceId — idempotency yeniden uretim olabilir)" -ForegroundColor Yellow
    } else {
        Write-Host "OK writeback $FieldName=$actual" -ForegroundColor Green
    }
}

function Assert-DashboardChartInXlsx {
    param([string]$ResourceId)
    $uri = "$BaseUrl/documents/api/v1/resources/$([Uri]::EscapeDataString($ResourceId))/versions/1/download"
    $tempXlsx = Join-Path $env:TEMP "smoke-dashboard-$ResourceId.xlsx"
    try {
        Invoke-WebRequest -Uri $uri -Headers @{ Authorization = "Bearer $token" } -OutFile $tempXlsx -TimeoutSec 120
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($tempXlsx)
        try {
            $chart1 = $zip.Entries | Where-Object { $_.FullName -eq "xl/charts/chart1.xml" } | Select-Object -First 1
            if (-not $chart1 -or $chart1.Length -lt 100) {
                throw "Dashboard XLSX icinde chart1.xml bos veya eksik (bar chart)."
            }
            Write-Host "OK dashboard chart: xl/charts/chart1.xml ($($chart1.Length) bytes)" -ForegroundColor Green

            $chart2 = $zip.Entries | Where-Object { $_.FullName -eq "xl/charts/chart2.xml" } | Select-Object -First 1
            if (-not $chart2) { throw "Dashboard XLSX icinde xl/charts/chart2.xml yok (donut chart eksik)." }
            Write-Host "OK dashboard chart: xl/charts/chart2.xml" -ForegroundColor Green

            $sheet3 = $zip.Entries | Where-Object { $_.FullName -eq "xl/worksheets/sheet3.xml" } | Select-Object -First 1
            if ($sheet3) {
                $sr = New-Object System.IO.StreamReader($sheet3.Open())
                $xml = $sr.ReadToEnd()
                $sr.Close()
                if ($xml -match '<c r="C10"[^>]*>\s*<v>') {
                    Write-Host "OK Veri!C10 numeric cell (bar chart)" -ForegroundColor Green
                } else {
                    throw "Veri!C10 sayisal hucre degil (inlineStr); bar chart bos kalir."
                }
                if ($xml -match '<c r="F2"[^>]*>\s*<v>') {
                    Write-Host "OK Veri!F2 numeric cell (donut chart)" -ForegroundColor Green
                } else {
                    throw "Veri!F2 sayisal hucre degil; donut chart bos kalir."
                }
                $chart2Entry = $zip.Entries | Where-Object { $_.FullName -eq "xl/charts/chart2.xml" } | Select-Object -First 1
                if ($chart2Entry) {
                    $sr3 = New-Object System.IO.StreamReader($chart2Entry.Open())
                    $chart2Xml = $sr3.ReadToEnd()
                    $sr3.Close()
                    if ($chart2Xml -match '<c:pt idx="0"><c:v>[0-9]') {
                        Write-Host "OK donut chart numCache populated" -ForegroundColor Green
                    } else {
                        throw "chart2.xml numCache bos; donut dilimleri cizilmez."
                    }
                }
            }
        } finally {
            $zip.Dispose()
        }
    } finally {
        Remove-Item $tempXlsx -ErrorAction SilentlyContinue
    }
}

Write-Host "PACKAGE-BRIEF smoke @ $BaseUrl package=$PackageId" -ForegroundColor Cyan
$dash = Invoke-Generate -ProfileCode "odak.package.dashboard.fromPackage" -TemplateCode "PACKAGE-DASHBOARD-STD" -Label "Dashboard XLSX"
$brief = Invoke-Generate -ProfileCode "odak.package.brief.fromPackage" -TemplateCode "PACKAGE-BRIEF-STD" -Label "Brief PPTX"

if (-not $SkipWritebackCheck) {
    Write-Host "Writeback kontrol..." -ForegroundColor Cyan
    Assert-PackageWriteback -FieldName "packageDashboardDiResourceId" -ExpectedResourceId $dash.resourceId
    Assert-PackageWriteback -FieldName "packageBriefDiResourceId" -ExpectedResourceId $brief.resourceId
}

if (-not $SkipChartCheck) {
    Write-Host "Dashboard chart (Yol A) kontrol..." -ForegroundColor Cyan
    Assert-DashboardChartInXlsx -ResourceId $dash.resourceId
}

function Assert-BriefDesignInPptx {
    param([string]$ResourceId)
    $uri = "$BaseUrl/documents/api/v1/resources/$([Uri]::EscapeDataString($ResourceId))/versions/1/download"
    $tempPptx = Join-Path $env:TEMP "smoke-brief-$ResourceId.pptx"
    try {
        Invoke-WebRequest -Uri $uri -Headers @{ Authorization = "Bearer $token" } -OutFile $tempPptx -TimeoutSec 120
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($tempPptx)
        try {
            $slides = @($zip.Entries | Where-Object { $_.FullName -match '^ppt/slides/slide\d+\.xml$' })
            if ($slides.Count -lt 7) {
                throw "Brief PPTX yalnizca $($slides.Count) slayt — executive sablon 7 slayt olmali (sunucuda eski binary olabilir; patch-package-brief-standard-test.ps1 calistirin)."
            }
            Write-Host "OK brief slayt sayisi: $($slides.Count)" -ForegroundColor Green

            $slide1 = $slides | Where-Object { $_.FullName -eq 'ppt/slides/slide1.xml' } | Select-Object -First 1
            $sr = New-Object System.IO.StreamReader($slide1.Open())
            $slide1Xml = $sr.ReadToEnd()
            $sr.Close()
            if ($slide1Xml -notmatch 'FF1F4E79') {
                throw "Brief kapak slaytinda accent banner yok (eski/plain sablon kullaniliyor)."
            }
            Write-Host "OK brief kapak accent banner" -ForegroundColor Green

            $slide3 = $slides | Where-Object { $_.FullName -eq 'ppt/slides/slide3.xml' } | Select-Object -First 1
            $sr3 = New-Object System.IO.StreamReader($slide3.Open())
            $slide3Xml = $sr3.ReadToEnd()
            $sr3.Close()
            if ($slide3Xml -notmatch 'roundRect') {
                throw "Brief KPI slaytinda kart sekilleri yok."
            }
            Write-Host "OK brief KPI kartlari" -ForegroundColor Green
        } finally {
            $zip.Dispose()
        }
    } finally {
        Remove-Item $tempPptx -ErrorAction SilentlyContinue
    }
}

Assert-BriefDesignInPptx -ResourceId $brief.resourceId

Write-Host "Smoke tamam." -ForegroundColor Green
Write-Host "Dashboard: $($dash.fileName)" -ForegroundColor DarkGray
Write-Host "Brief: $($brief.fileName)" -ForegroundColor DarkGray
