# Prod antet katalogunu test ortamina tam kopyalar (metadata + tasarim DOCX byte-for-byte).
#
#   .\docs\odak\document_intelligence\scripts\copy-letterheads-prod-to-test.ps1
#   .\docs\odak\document_intelligence\scripts\copy-letterheads-prod-to-test.ps1 -WhatIf
#
# UYARI: Test ortamindaki tum dm_letterheads kayitlari silinir ve prod kayitlari yeniden olusturulur.
# Yeni id'ler uretilir; sablon defaultLetterheadId baglantilari prod varsayilan antete guncellenir.

param(
    [string]$ProdBaseUrl = "http://192.168.20.8:5040",
    [string]$TestBaseUrl = "http://192.168.20.20:5040",
    [string]$ProdToken = $env:DI_TOKEN_PROD,
    [string]$TestToken = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$loadProd = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
$loadTest = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"

if ([string]::IsNullOrWhiteSpace($ProdToken) -and (Test-Path $loadProd)) {
    $ProdToken = & $loadProd
}
if ([string]::IsNullOrWhiteSpace($TestToken) -and (Test-Path $loadTest)) {
    $TestToken = & $loadTest
}
if ([string]::IsNullOrWhiteSpace($ProdToken) -or [string]::IsNullOrWhiteSpace($TestToken)) {
    throw "Prod/test token gerekli."
}
$ProdToken = $ProdToken.Trim()
$TestToken = $TestToken.Trim()

function New-Headers([string]$Token) {
    return @{
        Authorization = "Bearer $Token"
        "Content-Type" = "application/json"
    }
}

function Invoke-DiApi {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )
    $uri = "$BaseUrl$Path"
    $params = @{ Uri = $uri; Method = $Method; Headers = $Headers; TimeoutSec = 180 }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 20 -Compress)
    }
    return Invoke-RestMethod @params
}

function Get-DesignBytes {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$StoragePath
    )
    if ([string]::IsNullOrWhiteSpace($StoragePath)) { return $null }
    $uri = "$BaseUrl/data/api/v1/files/download?filePath=$([uri]::EscapeDataString($StoragePath))"
    $resp = Invoke-WebRequest -Uri $uri -Headers $Headers -Method GET
    return [byte[]]$resp.Content
}

function Get-FooterStats([byte[]]$Bytes) {
    if (-not $Bytes -or $Bytes.Length -eq 0) { return "empty" }
    $tmp = Join-Path $env:TEMP ("lh-stat-" + [guid]::NewGuid().ToString("N"))
    $file = Join-Path $tmp "design.docx"
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    [IO.File]::WriteAllBytes($file, $Bytes)
    Expand-Archive -Path $file -DestinationPath $tmp -Force
    $footers = Get-ChildItem -Path (Join-Path $tmp "word") -Filter "footer*.xml" -ErrorAction SilentlyContinue |
        Sort-Object Name |
        ForEach-Object { "$($_.Name)=$($_.Length)" }
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
    return ($footers -join ", ")
}

function Set-LetterheadDesign {
    param(
        [string]$TestBaseUrl,
        [hashtable]$TestHeaders,
        [string]$LetterheadId,
        [byte[]]$DesignBytes,
        [string]$FileName,
        [string]$LetterheadJson,
        [string]$SettingsJson,
        [object]$RowMeta
    )
    $b64 = [Convert]::ToBase64String($DesignBytes)
    $uploadBody = @{
        Content = $b64
        DatasetName = "dm_letterheads"
        FieldName = "designFile"
        RecordId = $LetterheadId
        UseCompression = $false
        UseEncryption = $false
    }
    $upload = Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $TestHeaders -Method POST `
        -Path "/data/api/v1/files/upload" -Body $uploadBody

    $filePath = $upload.data.filePath
    if ([string]::IsNullOrWhiteSpace($filePath)) { $filePath = $upload.Data.FilePath }
    if ([string]::IsNullOrWhiteSpace($filePath)) { throw "Upload filePath bos." }

    $storedName = $upload.data.file_name
    if ([string]::IsNullOrWhiteSpace($storedName)) { $storedName = $upload.Data.file_name }
    if ([string]::IsNullOrWhiteSpace($storedName)) { $storedName = $FileName }

    $patch = @{
        name = $RowMeta.name
        code = $RowMeta.code
        description = $RowMeta.description
        isDefault = [bool]$RowMeta.isDefault
        isActive = [bool]$RowMeta.isActive
        letterheadJson = $LetterheadJson
        settingsJson = $SettingsJson
        designStoragePath = $filePath
        designFileName = $storedName
        updatedBy = "copy-letterheads-prod-to-test"
        updatedAt = (Get-Date).ToUniversalTime().ToString("o")
    }
    Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $TestHeaders -Method PUT `
        -Path "/data/api/v1/data/dm_letterheads/$LetterheadId" -Body $patch | Out-Null
    return $filePath
}

Write-Host "=== Prod -> Test antet kopyasi ===" -ForegroundColor Cyan
Write-Host "Prod: $ProdBaseUrl" -ForegroundColor Gray
Write-Host "Test: $TestBaseUrl" -ForegroundColor Gray

$prodH = New-Headers $ProdToken
$testH = New-Headers $TestToken

Write-Host "`n1) Prod antetler export..." -ForegroundColor Cyan
$prodList = Invoke-DiApi -BaseUrl $ProdBaseUrl -Headers $prodH -Method GET -Path "/documents/api/v1/letterheads"
$exports = @()
foreach ($item in $prodList.items) {
    $row = Invoke-DiApi -BaseUrl $ProdBaseUrl -Headers $prodH -Method GET `
        -Path "/data/api/v1/data/dm_letterheads/$($item.id)"
    $dto = Invoke-DiApi -BaseUrl $ProdBaseUrl -Headers $prodH -Method GET `
        -Path "/documents/api/v1/letterheads/$($item.id)"
    $bytes = Get-DesignBytes -BaseUrl $ProdBaseUrl -Headers $prodH -StoragePath $row.designStoragePath
    if (-not $bytes -or $bytes.Length -eq 0) {
        Write-Host "  WARN $($item.code): tasarim dosyasi yok" -ForegroundColor Yellow
    } else {
        $stats = Get-FooterStats $bytes
        Write-Host "  $($item.code): $($bytes.Length) byte | footers: $stats" -ForegroundColor Green
    }
    $exports += [PSCustomObject]@{
        Dto = $dto
        Row = $row
        DesignBytes = $bytes
        FileName = if ($row.designFileName) { $row.designFileName } else { "$($item.code)-design.docx" }
    }
}

Write-Host "`n2) Test antetler siliniyor..." -ForegroundColor Cyan
$testList = Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $testH -Method GET -Path "/documents/api/v1/letterheads"
foreach ($old in $testList.items) {
    Write-Host "  DELETE $($old.code) ($($old.id))" -ForegroundColor Yellow
    if (-not $WhatIf) {
        Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $testH -Method DELETE `
            -Path "/documents/api/v1/letterheads/$($old.id)" | Out-Null
    }
}

Write-Host "`n3) Prod antetler testte olusturuluyor..." -ForegroundColor Cyan
$codeToNewId = @{}
foreach ($exp in ($exports | Sort-Object { $_.Dto.isDefault } -Descending)) {
    $dto = $exp.Dto
    $body = @{
        name = $dto.name
        code = $dto.code
        description = $dto.description
        isDefault = [bool]$dto.isDefault
        isActive = [bool]$dto.isActive
        letterhead = $dto.letterhead
        settings = $dto.settings
    }
    if ($WhatIf) {
        Write-Host "  WHATIF CREATE $($dto.code)" -ForegroundColor DarkGray
        continue
    }
    $created = Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $testH -Method POST `
        -Path "/documents/api/v1/letterheads" -Body $body
    $codeToNewId[$dto.code] = $created.id
    Write-Host "  OK $($dto.code) -> id=$($created.id)" -ForegroundColor Green

    if ($exp.DesignBytes -and $exp.DesignBytes.Length -gt 0) {
        $path = Set-LetterheadDesign `
            -TestBaseUrl $TestBaseUrl `
            -TestHeaders $testH `
            -LetterheadId $created.id `
            -DesignBytes $exp.DesignBytes `
            -FileName $exp.FileName `
            -LetterheadJson $exp.Row.letterheadJson `
            -SettingsJson $exp.Row.settingsJson `
            -RowMeta @{
                name = $dto.name
                code = $dto.code
                description = $dto.description
                isDefault = $dto.isDefault
                isActive = $dto.isActive
            }
        $verifyBytes = Get-DesignBytes -BaseUrl $TestBaseUrl -Headers $testH -StoragePath $path
        $prodHash = (Get-FileHash -InputStream ([IO.MemoryStream]::new($exp.DesignBytes)) -Algorithm SHA256).Hash
        $testHash = if ($verifyBytes) { (Get-FileHash -InputStream ([IO.MemoryStream]::new($verifyBytes)) -Algorithm SHA256).Hash } else { "none" }
        $match = ($prodHash -eq $testHash)
        $color = if ($match) { "Green" } else { "Red" }
        Write-Host "    design $($exp.DesignBytes.Length) -> $($verifyBytes.Length) byte SHA256 match=$match" -ForegroundColor $color
        if (-not $match) {
            Write-Host "    prod footers: $(Get-FooterStats $exp.DesignBytes)" -ForegroundColor Yellow
            Write-Host "    test footers: $(Get-FooterStats $verifyBytes)" -ForegroundColor Yellow
        }
    }
}

if ($WhatIf) { exit 0 }

Write-Host "`n4) Sablon varsayilan antet baglantilari..." -ForegroundColor Cyan
$defaultCode = ($exports | Where-Object { $_.Dto.isDefault } | Select-Object -First 1).Dto.code
if (-not $defaultCode) { $defaultCode = "ODK-STD" }
$defaultId = $codeToNewId[$defaultCode]
if ($defaultId) {
    $templates = Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $testH -Method GET -Path "/documents/api/v1/templates"
    foreach ($tpl in $templates.items) {
        if ($tpl.status -eq "published") {
            Write-Host "  SKIP published $($tpl.code)" -ForegroundColor DarkGray
            continue
        }
        try {
            Invoke-DiApi -BaseUrl $TestBaseUrl -Headers $testH -Method PUT `
                -Path "/documents/api/v1/templates/$($tpl.id)/page-structure" `
                -Body @{ defaultLetterheadId = $defaultId } | Out-Null
            Write-Host "  OK template $($tpl.code) -> $defaultCode" -ForegroundColor Green
        } catch {
            Write-Host "  WARN template $($tpl.code): $_" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n=== Tamam ===" -ForegroundColor Green
Write-Host "Test antet kodlari: $($codeToNewId.Keys -join ', ')" -ForegroundColor Green
if ($defaultId) { Write-Host "Varsayilan antet: $defaultCode ($defaultId)" -ForegroundColor Green }
