# D-BR2 kapak sayfasi smoke — ODK-COVER-STD + CoC DOCX uretiminde prepend dogrulama
#
#   .\scripts\tests\MngDocument\smoke-cover-page-coc-test.ps1
#   .\scripts\tests\MngDocument\smoke-cover-page-coc-test.ps1 -LineId "<uuid>"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$LineId = "",
    [string]$ProfileCode = "odak.coc.fromLine",
    [string]$TemplateCode = "COC-STANDARD",
    [string]$CoverPageCode = "ODK-COVER-STD",
    [string]$Token = $env:DI_TOKEN
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
$coverBase = "$BaseUrl/documents/api/v1/cover-pages"
$utf8 = [System.Text.Encoding]::UTF8

Write-Host "=== Kapak sayfasi smoke -> $BaseUrl ===" -ForegroundColor Cyan

$covers = Invoke-RestMethod -Uri $coverBase -Method GET -Headers $headers -TimeoutSec 60
$cover = @($covers.items) | Where-Object { $_.code -eq $CoverPageCode } | Select-Object -First 1
if (-not $cover) { throw "Kapak bulunamadi: $CoverPageCode (once seed-cover-pages-odak.ps1 calistirin)" }
Write-Host "OK kapak katalog: $($cover.code) id=$($cover.id)" -ForegroundColor Green

if ([string]::IsNullOrWhiteSpace($LineId)) {
    throw "LineId zorunlu. Ornek: -LineId <odak_siparis_kalemleri dataId>"
}

$body = @{
    profileCode      = $ProfileCode
    templateCode     = $TemplateCode
    includeCoverPage = $true
    coverPageId      = [string]$cover.id
    context          = @{
        type = "odak.siparis.line"
        id   = $LineId.Trim()
    }
}
$json = $body | ConvertTo-Json -Depth 6 -Compress
Write-Host "POST generate CoC + kapak (line=$LineId)..." -ForegroundColor Cyan
$result = Invoke-RestMethod -Uri $generateBase -Method POST -Headers $headers -Body ($utf8.GetBytes($json)) -ContentType "application/json; charset=utf-8" -TimeoutSec 180

if ([string]::IsNullOrWhiteSpace($result.coverPageId)) {
    throw "coverPageId bos — kapak merge uygulanmamis"
}
if ($result.coverPageCode -ne $CoverPageCode) {
    throw "coverPageCode beklenen=$CoverPageCode gelen=$($result.coverPageCode)"
}
Write-Host "OK uretim coverPage=$($result.coverPageCode) resourceId=$($result.resourceId)" -ForegroundColor Green

function Assert-CoverPrependedInDocx {
    param([string]$ResourceId)
    $uri = "$BaseUrl/documents/api/v1/resources/$([Uri]::EscapeDataString($ResourceId))/versions/1/download"
    $tempDocx = Join-Path $env:TEMP "smoke-cover-$ResourceId.docx"
    try {
        Invoke-WebRequest -Uri $uri -Headers $headers -OutFile $tempDocx -TimeoutSec 120
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($tempDocx)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq "word/document.xml" } | Select-Object -First 1
            if (-not $entry) { throw "word/document.xml yok" }
            $reader = New-Object System.IO.StreamReader($entry.Open())
            $xml = $reader.ReadToEnd()
            $reader.Close()
            if ($xml -notmatch 'w:type w:val="nextPage"') { throw "kapak section break (nextPage) yok" }
            Write-Host "OK DOCX kapak section break" -ForegroundColor Green
        } finally {
            $zip.Dispose()
        }
    } finally {
        if (Test-Path $tempDocx) { Remove-Item $tempDocx -Force -ErrorAction SilentlyContinue }
    }
}

Assert-CoverPrependedInDocx -ResourceId $result.resourceId
Write-Host "Smoke tamam." -ForegroundColor Cyan
