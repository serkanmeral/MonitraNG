# Smoke: D-META + D-CREATE + D-FILE-PREV (prod / configurable gateway)
# Kabul: upload origin=upload, upload editor blocked, upload PDF preview,
#        native origin=native + documentNo, native editor OK, native PDF blocked,
#        documentNo domain uniqueness (409).
param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$getToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$codeSuffix = "SMK$stamp"
$uploadName = "_smoke_upload_$stamp.docx"
$nativeName = "_smoke_native_$stamp"
$documentNo = "SMK-$codeSuffix"
$seedDocumentNo = "SMK-SEED-$codeSuffix"

function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    & $getToken | Out-Null
    return (Get-Content $TokenFile -Raw).Trim()
}

function Invoke-Di {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [switch]$RawBytes
    )
    $uri = "$Gateway/documents/api/v1/resources$Path"
    $params = @{
        Uri     = $uri
        Method  = $Method
        Headers = $script:Headers
        TimeoutSec = 120
    }
    if ($Body -ne $null) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    if ($RawBytes) {
        return Invoke-WebRequest @params
    }
    try {
        return Invoke-RestMethod @params
    }
    catch {
        $status = $null
        try { $status = [int]$_.Exception.Response.StatusCode } catch { }
        $detail = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        return [PSCustomObject]@{ Ok = $false; Status = $status; Detail = $detail }
    }
}

function Assert-Status {
    param($Result, [int[]]$Expected, [string]$Label)
    if ($Result -is [System.Net.Http.HttpResponseMessage]) { return }
    if ($Result.PSObject.Properties.Name -contains "Ok" -and $Result.Ok -eq $false) {
        $exp = ($Expected -join ",")
        throw "$Label -> HTTP $($Result.Status) (beklenen: $exp)`n$($Result.Detail)"
    }
}

function Assert-HttpError {
    param($Result, [int]$ExpectedStatus, [string]$CodeFragment, [string]$Label)
    if ($Result -isnot [PSCustomObject] -or $Result.Ok -ne $false) {
        throw "$Label -> hata bekleniyordu (HTTP $ExpectedStatus), basarili dondu"
    }
    if ($Result.Status -ne $ExpectedStatus) {
        throw "$Label -> HTTP $($Result.Status) (beklenen $ExpectedStatus)`n$($Result.Detail)"
    }
    if ($CodeFragment -and $Result.Detail -notmatch [regex]::Escape($CodeFragment)) {
        throw "$Label -> beklenen kod '$CodeFragment' bulunamadi`n$($Result.Detail)"
    }
    Write-Host "  OK $Label (HTTP $ExpectedStatus)" -ForegroundColor Green
}

$token = Get-Token
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi." }

$script:Headers = @{ Authorization = "Bearer $token" }
$createdIds = @()
$failed = 0

Write-Host ""
Write-Host "DI smoke: D-META / D-CREATE / D-FILE-PREV ($Gateway)" -ForegroundColor Cyan
Write-Host ""

try {
    Write-Host "1) Gotenberg / rendering status" -ForegroundColor Yellow
    $rendering = Invoke-RestMethod -Uri "$Gateway/documents/api/v1/rendering/status" -Headers $script:Headers -TimeoutSec 30
    if (-not $rendering.gotenbergReachable) { throw "Gotenberg erisilemiyor." }
    Write-Host "  OK gotenbergReachable=true" -ForegroundColor Green

    Write-Host "2) Minimal DOCX seed (native create + v1 download)" -ForegroundColor Yellow
    $seedBody = @{
        parentId   = $null
        name       = "_smoke_seed_$stamp"
        documentNo = $seedDocumentNo
    }
    $seed = Invoke-Di -Method POST -Path "/documents" -Body $seedBody
    Assert-Status $seed 201 "seed native"
    $createdIds += $seed.id
    $seedDownload = Invoke-WebRequest -Uri "$Gateway/documents/api/v1/resources/$($seed.id)/versions/1/download" -Headers $script:Headers -TimeoutSec 120
    if ($seedDownload.StatusCode -ne 200) { throw "seed download HTTP $($seedDownload.StatusCode)" }
    $docxBytes = $seedDownload.Content
    $docxB64 = [Convert]::ToBase64String($docxBytes)
    Write-Host "  OK seed docx bytes=$($docxBytes.Length)" -ForegroundColor Green

    Write-Host "3) Upload DOCX (origin=upload)" -ForegroundColor Yellow
    $uploadBody = @{
        parentId         = $null
        name             = $uploadName
        originalFileName = $uploadName
        mimeType         = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        extension        = ".docx"
        size             = $docxBytes.Length
        content          = $docxB64
        description      = "smoke upload docx"
    }
    $upload = Invoke-Di -Method POST -Path "/file" -Body $uploadBody
    Assert-Status $upload 201 "upload create"
    if ($upload.origin -ne "upload") { throw "upload origin beklenen 'upload', gelen '$($upload.origin)'" }
    $createdIds += $upload.id
    Write-Host "  OK upload id=$($upload.id) origin=$($upload.origin)" -ForegroundColor Green

    Write-Host "4) Upload -> editor-session engeli (D-META gate)" -ForegroundColor Yellow
    $editorUpload = Invoke-Di -Path "/$($upload.id)/editor-session"
    Assert-HttpError $editorUpload 400 "UPLOAD_NOT_EDITABLE" "upload editor blocked"

    Write-Host "5) Upload -> preview/pdf (D-FILE-PREV)" -ForegroundColor Yellow
    $pdfResp = Invoke-Di -Path "/$($upload.id)/preview/pdf" -RawBytes
    if ($pdfResp.StatusCode -ne 200) { throw "preview/pdf HTTP $($pdfResp.StatusCode)" }
    $pdfBytes = $pdfResp.Content
    if ($pdfBytes.Length -lt 4 -or [Text.Encoding]::ASCII.GetString($pdfBytes[0..3]) -ne "%PDF") {
        throw "preview/pdf gecerli PDF donmedi (len=$($pdfBytes.Length))"
    }
    Write-Host "  OK preview/pdf bytes=$($pdfBytes.Length)" -ForegroundColor Green

    Write-Host "6) Native DOCX create (D-CREATE)" -ForegroundColor Yellow
    $nativeBody = @{
        parentId    = $null
        name        = $nativeName
        documentNo  = $documentNo
        description = "smoke native docx"
    }
    $native = Invoke-Di -Method POST -Path "/documents" -Body $nativeBody
    Assert-Status $native 201 "native create"
    if ($native.origin -ne "native") { throw "native origin beklenen 'native', gelen '$($native.origin)'" }
    if ($native.documentNo -ne $documentNo) { throw "documentNo uyusmadi" }
    $createdIds += $native.id
    Write-Host "  OK native id=$($native.id) origin=$($native.origin) documentNo=$($native.documentNo)" -ForegroundColor Green

    Write-Host "7) Native -> editor-session acilabilir" -ForegroundColor Yellow
    $editorNative = Invoke-Di -Path "/$($native.id)/editor-session"
    Assert-Status $editorNative 200 "native editor-session"
    if ([string]::IsNullOrWhiteSpace($editorNative.editorUrl)) { throw "editor-session editorUrl bos" }
    Write-Host "  OK editor-session editorUrl set" -ForegroundColor Green

    Write-Host "8) Native -> preview/pdf engeli" -ForegroundColor Yellow
    $nativePdf = Invoke-Di -Path "/$($native.id)/preview/pdf"
    Assert-HttpError $nativePdf 400 "PREVIEW_NOT_AVAILABLE" "native pdf blocked"

    Write-Host "9) documentNo benzersizligi (409)" -ForegroundColor Yellow
    $dupBody = @{
        parentId   = $null
        name       = "${nativeName}_dup"
        documentNo = $documentNo
    }
    $dup = Invoke-Di -Method POST -Path "/documents" -Body $dupBody
    Assert-HttpError $dup 409 "DOCUMENT_NO_EXISTS" "duplicate documentNo"

    Write-Host ""
    Write-Host "Tum smoke kontrolleri gecti." -ForegroundColor Cyan
}
catch {
    Write-Host ""
    Write-Host "SMOKE FAIL: $($_.Exception.Message)" -ForegroundColor Red
    $failed = 1
}
finally {
    if (-not $KeepArtifacts -and $createdIds.Count -gt 0) {
        Write-Host ""
        Write-Host "Temizlik: $($createdIds.Count) kaynak siliniyor..." -ForegroundColor Yellow
        foreach ($id in $createdIds) {
            try {
                Invoke-Di -Method DELETE -Path "/$id" | Out-Null
                Write-Host "  deleted $id" -ForegroundColor DarkGray
            }
            catch {
                Write-Host "  silinemedi $id : $($_.Exception.Message)" -ForegroundColor DarkYellow
            }
        }
    }
    elseif ($KeepArtifacts) {
        Write-Host ""
        Write-Host "KeepArtifacts: $($createdIds -join ', ')" -ForegroundColor Yellow
    }
}

exit $failed
