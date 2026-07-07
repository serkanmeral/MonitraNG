# Collabora demo dosyalari uretir ve istege bagli test sunucusuna yukler.
# Kullanim (repo kokunden):
#   .\scripts\tests\MngDocument\demo\publish-collabora-demos.ps1
#   .\scripts\tests\MngDocument\demo\publish-collabora-demos.ps1 -Upload -Gateway http://192.168.20.20:5040
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$Upload,
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$exporterDir = Join-Path $PSScriptRoot "CollaboraDemoExporter"
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "output"
}

Write-Host "== Collabora demo dosyalari ==" -ForegroundColor Cyan
Push-Location $exporterDir
try {
    dotnet run -c Release -- $OutputDir
    if ($LASTEXITCODE -ne 0) { throw "Exporter failed." }
}
finally {
    Pop-Location
}

$xlsxPath = Join-Path $OutputDir "MonitraNG-Demo-Elektronik-Tablo.xlsx"
$pptxPath = Join-Path $OutputDir "MonitraNG-Demo-Sunum.pptx"
Write-Host ""
Write-Host "Dosyalar hazir:" -ForegroundColor Green
Write-Host "  $xlsxPath"
Write-Host "  $pptxPath"
Write-Host ""
Write-Host "Collabora'da acmak icin:" -ForegroundColor Yellow
Write-Host "  1) DI'da dosya yukle VEYA asagidaki -Upload ile native kaynak olustur"
Write-Host "  2) Kaynagi ac -> Collabora editör"

if (-not $Upload) { exit 0 }

$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
    throw "Token alinamadi."
}

function Get-NodeId([object]$node) {
    if ($null -eq $node) { return $null }
    if ($node -is [System.Array]) { $node = $node[0] }
    return [string]$node.id
}

function Invoke-Docs {
    param([string]$Method = "GET", [string]$Path, [object]$Body = $null)
    $uri = "$Gateway/documents/api/v1$Path"
    $params = @{
        Uri        = $uri
        Method     = $Method
        Headers    = $script:Headers
        TimeoutSec = 180
    }
    if ($Body -ne $null) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }
    return Invoke-RestMethod @params -SkipCertificateCheck
}

function Find-DemoFolder {
    $roots = @(Invoke-Docs -Path "/resources/tree/roots")
    $docsRoot = $roots | Where-Object { $_.name -match 'D.k.man' } | Select-Object -First 1
    if ($docsRoot) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($(Get-NodeId $docsRoot)))")
        $kalite = $children | Where-Object { $_.name -match 'Kalite' } | Select-Object -First 1
        if ($kalite) { return $kalite }
        $first = @($children | Select-Object -First 1)[0]
        if ($first) { return $first }
    }
    return @($roots | Select-Object -First 1)[0]
}

function Publish-NativeFile {
    param(
        [string]$Path,
        [string]$DisplayName,
        [string]$DocumentNo,
        [string]$Mime,
        [string]$Ext
    )
    $bytes = [IO.File]::ReadAllBytes($Path)
    $body = @{
        parentId         = $script:ParentId
        name             = $DisplayName
        originalFileName = [IO.Path]::GetFileName($Path)
        description      = "Collabora musteri demo dosyasi"
        mimeType         = $Mime
        extension        = $Ext
        size             = $bytes.LongLength
        content          = [Convert]::ToBase64String($bytes)
        origin           = "native"
        documentNo       = $DocumentNo
    }
    $created = Invoke-Docs -Method POST -Path "/resources/file" -Body $body
    Write-Host "  Yuklendi: $($created.name) id=$($created.id)" -ForegroundColor Green
    return $created
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$folder = Find-DemoFolder
$script:ParentId = Get-NodeId $folder
$stamp = Get-Date -Format "yyyyMMdd"

Write-Host "Upload -> $($folder.name) ($($script:ParentId))" -ForegroundColor Cyan
Publish-NativeFile -Path $xlsxPath -DisplayName "Demo Elektronik Tablo" -DocumentNo "DEMO-XLS-$stamp" `
    -Mime "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" -Ext ".xlsx"
Publish-NativeFile -Path $pptxPath -DisplayName "Demo Sunum" -DocumentNo "DEMO-PPT-$stamp" `
    -Mime "application/vnd.openxmlformats-officedocument.presentationml.presentation" -Ext ".pptx"
Write-Host "Upload tamam." -ForegroundColor Green
