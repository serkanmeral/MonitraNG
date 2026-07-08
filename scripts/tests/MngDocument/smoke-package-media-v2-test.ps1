# Medya paketi V2 smoke — V1 şablonlarına dokunmadan PACKAGE-*-STD-V2 ile üretim testi
#
# Kullanım:
#   .\scripts\tests\MngDocument\smoke-package-media-v2-test.ps1 -PackageId "<uuid>"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$PackageId = "2d8aeb0e-6f67-4f3a-a578-21cff682ec17",
    [string]$DashboardTemplateCode = "PACKAGE-DASHBOARD-STD-V2",
    [string]$BriefTemplateCode = "PACKAGE-BRIEF-STD-V2",
    [string]$Token = $env:DI_TOKEN,
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
    Write-Host "POST $Label ($TemplateCode)..." -ForegroundColor Cyan
    $result = Invoke-RestMethod -Uri $generateBase -Method POST -Headers $headers -Body $bytes -ContentType "application/json; charset=utf-8" -TimeoutSec 180
    Write-Host "OK $Label resourceId=$($result.resourceId) file=$($result.fileName)" -ForegroundColor Green
    return $result
}

function Assert-DashboardDonutInXlsx {
    param([string]$ResourceId)
    $uri = "$BaseUrl/documents/api/v1/resources/$([Uri]::EscapeDataString($ResourceId))/versions/1/download"
    $tempXlsx = Join-Path $env:TEMP "smoke-v2-dashboard-$ResourceId.xlsx"
    try {
        Invoke-WebRequest -Uri $uri -Headers $headers -OutFile $tempXlsx -TimeoutSec 120
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($tempXlsx)
        try {
            $chartEntry = $zip.Entries | Where-Object { $_.FullName -eq "xl/charts/chart2.xml" } | Select-Object -First 1
            if (-not $chartEntry) { throw "chart2.xml yok" }
            $reader = New-Object System.IO.StreamReader($chartEntry.Open())
            $xml = $reader.ReadToEnd()
            $reader.Close()
            if ($xml -notmatch "doughnutChart") { throw "doughnutChart bulunamadi" }
            if ($xml -notmatch '<c:pt idx="0"><c:v>Sevk</c:v></c:pt>') { throw "donut seed cache (Sevk) yok" }
            if ($xml -notmatch '<c:pt idx="0"><c:v>\d+</c:v></c:pt>') { throw "donut numCache pt yok" }
            Write-Host "OK dashboard donut cache (V2)" -ForegroundColor Green
        } finally {
            $zip.Dispose()
        }
    } finally {
        if (Test-Path $tempXlsx) { Remove-Item $tempXlsx -Force -ErrorAction SilentlyContinue }
    }
}

function Assert-BriefPptxStructure {
    param([string]$ResourceId)
    $uri = "$BaseUrl/documents/api/v1/resources/$([Uri]::EscapeDataString($ResourceId))/versions/1/download"
    $tempPptx = Join-Path $env:TEMP "smoke-v2-brief-$ResourceId.pptx"
    try {
        Invoke-WebRequest -Uri $uri -Headers $headers -OutFile $tempPptx -TimeoutSec 120
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($tempPptx)
        try {
            $slideCount = @($zip.Entries | Where-Object { $_.FullName -match '^ppt/slides/slide\d+\.xml$' }).Count
            if ($slideCount -lt 7) { throw "PPTX slayt sayisi $slideCount (beklenen >=7)" }

            $slide1 = $zip.Entries | Where-Object { $_.FullName -eq "ppt/slides/slide1.xml" } | Select-Object -First 1
            $r1 = New-Object System.IO.StreamReader($slide1.Open())
            $s1 = $r1.ReadToEnd(); $r1.Close()
            if ($s1 -notmatch 'name="Domain Logo"') { throw "Kapak slaytinda Domain Logo placeholder yok" }

            $slide4 = $zip.Entries | Where-Object { $_.FullName -eq "ppt/slides/slide4.xml" } | Select-Object -First 1
            $r4 = New-Object System.IO.StreamReader($slide4.Open())
            $s4 = $r4.ReadToEnd(); $r4.Close()
            foreach ($bar in @("FulfillBar Sevk", "FulfillBar Kalan", "FulfillBar Stok")) {
                if ($s4 -notmatch "name=`"$bar`"") { throw "Tamamlanma slaytinda $bar yok" }
            }

            Write-Host "OK brief PPTX yapisi (V2: logo placeholder + veri-gudumlu bar isimleri)" -ForegroundColor Green
        } finally {
            $zip.Dispose()
        }
    } finally {
        if (Test-Path $tempPptx) { Remove-Item $tempPptx -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host "=== Medya Paketi V2 Smoke ===" -ForegroundColor Cyan
Write-Host "PackageId=$PackageId Dashboard=$DashboardTemplateCode Brief=$BriefTemplateCode" -ForegroundColor DarkGray

$dash = Invoke-Generate -ProfileCode "odak.package.dashboard.fromPackage" -TemplateCode $DashboardTemplateCode -Label "Dashboard V2"
$brief = Invoke-Generate -ProfileCode "odak.package.brief.fromPackage" -TemplateCode $BriefTemplateCode -Label "Brief V2"

if (-not $SkipChartCheck) {
    Assert-DashboardDonutInXlsx -ResourceId $dash.resourceId
}
Assert-BriefPptxStructure -ResourceId $brief.resourceId

Write-Host "=== V2 Smoke tamam ===" -ForegroundColor Green
