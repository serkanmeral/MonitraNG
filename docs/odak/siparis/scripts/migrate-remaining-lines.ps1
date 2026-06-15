# SQL dump'tan DG'ye henuz aktarilmamis kalemleri dogrudan POST eder.
# Usage: .\migrate-remaining-lines.ps1

param(
    [string]$SqlDumpPath = "",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$dumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($null -ne $Body) {
        $p.Body = $Body | ConvertTo-Json -Depth 20 -Compress
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function To-IntOrNull { param($Value); if ($null -eq $Value -or $Value -eq "") { return $null }; if ([string]$Value -notmatch '^\d+$') { return $null }; return [int]$Value }
function To-DoubleOrNull { param($Value); if ($null -eq $Value -or $Value -eq "") { return $null }; return [double]$Value }
function To-IsoDate { param($Value); if ([string]::IsNullOrWhiteSpace($Value)) { return $null }; try { return ([datetime]$Value).ToUniversalTime().ToString("o") } catch { return $null } }
function Sanitize-JsonText {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    $text = [string]$Value
    if ([string]::IsNullOrEmpty($text)) { return $text }
    $latin = [System.Text.Encoding]::GetEncoding("ISO-8859-1")
    $utf8 = [System.Text.Encoding]::UTF8
    try {
        $bytes = $latin.GetBytes($text)
        $fixed = $utf8.GetString($bytes)
        if (-not [string]::IsNullOrWhiteSpace($fixed)) { $text = $fixed }
    }
    catch { }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $text.ToCharArray()) {
        $code = [int]$ch
        if ($code -lt 32 -and $code -notin @(9, 10, 13)) { continue }
        if ([char]::IsSurrogate($ch)) { continue }
        [void]$sb.Append($ch)
    }
    return $sb.ToString()
}

function Limit-String { param([object]$Value, [int]$MaxLength); if ($null -eq $Value) { return $null }; $t = [string]$Value; if ($t.Length -le $MaxLength) { return $t }; return $t.Substring(0, $MaxLength) }
function Map-Unit {
    param([string]$LegacyUnit)
    if ([string]::IsNullOrWhiteSpace($LegacyUnit)) { return "adet" }
    switch ($LegacyUnit.Trim().ToLowerInvariant()) {
        { $_ -in @("adet", "ad", "pcs", "ea") } { return "adet" }
        { $_ -in @("takim", "takım", "set") } { return "takim" }
        { $_ -in @("kg", "kilogram") } { return "kg" }
        { $_ -in @("m", "metre") } { return "m" }
        { $_ -in @("m2", "m²") } { return "m2" }
        default { return "adet" }
    }
}
function Map-Currency {
    param([string]$LegacyCurrency)
    if ([string]::IsNullOrWhiteSpace($LegacyCurrency)) { return $null }
    switch ($LegacyCurrency.Trim().ToUpperInvariant()) {
        "TL" { return "TRY" }
        "TRY" { return "TRY" }
        "USD" { return "USD" }
        "EUR" { return "EUR" }
        "GBP" { return "GBP" }
        default { return $null }
    }
}
function Get-DgErrorMessage {
    param($ErrorRecord)
    if ($ErrorRecord.ErrorDetails -and $ErrorRecord.ErrorDetails.Message) { return [string]$ErrorRecord.ErrorDetails.Message }
    return [string]$ErrorRecord.Exception.Message
}

Write-Host "`n=== migrate-remaining-lines ===" -ForegroundColor Cyan
$pkgRows = Read-SqlInsertRows -Path $dumpPath -TableName "packages"
$itemRows = Read-SqlInsertRows -Path $dumpPath -TableName "packageitems"
$pkgNoById = @{}
foreach ($p in $pkgRows) { $pkgNoById[[string]$p[0]] = [string]$p[1] }

$pkgMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
$lineMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"
$parentLineMap = Load-ParentLineNoMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath

$ok = 0; $skip = 0; $fail = 0; $n = 0
foreach ($item in $itemRows) {
    $n++
    if ($n % 500 -eq 0) { Write-Host "  ... $n / $($itemRows.Count)" -ForegroundColor Gray }

    $legacyLineId = [string]$item[0]
    $legacyPkgId = [string]$item[1]
    $lineNo = To-IntOrNull $item[3]
    if ($legacyLineId -notmatch '^\d+$' -or -not $lineNo -or $lineNo -lt 1) { $skip++; continue }
    if ($lineMap.ContainsKey($legacyLineId)) { $skip++; continue }
    if (-not $pkgMap.ContainsKey($legacyPkgId)) { $skip++; continue }

    $parentId = $pkgMap[$legacyPkgId]
    $parentLineKey = "$parentId|$lineNo"
    if ($parentLineMap.ContainsKey($parentLineKey)) { $skip++; continue }

    $packageNo = $pkgNoById[$legacyPkgId]
    $description = Limit-String (Sanitize-JsonText ([string]$item[6])) 2000
    if ([string]::IsNullOrWhiteSpace($description)) { $description = "Legacy kalem $lineNo" }
    $mappedCurrency = Map-Currency ([string]$item[13])

    $body = @{
        parentPackageId   = $parentId
        parentWorkItemId  = $parentId
        lineNo            = $lineNo
        customerProjectNo = Limit-String (Sanitize-JsonText $item[2]) 64
        customerPoNo      = Limit-String (Sanitize-JsonText $item[4]) 64
        customerPoItemNo  = To-IntOrNull $item[5]
        description       = $description
        poItemRevNo       = Limit-String (Sanitize-JsonText $item[7]) 32
        customerJobNo     = Limit-String (Sanitize-JsonText $item[8]) 64
        quantity          = if ($null -ne $item[9] -and $item[9] -ne "") { [double]$item[9] } else { 0 }
        unit              = Map-Unit ([string]$item[10])
        unitCost          = To-DoubleOrNull $item[11]
        totalCost         = To-DoubleOrNull $item[12]
        qualityReqs       = Limit-String (Sanitize-JsonText $item[14]) 1000
        isFai             = [bool]([int]$item[15] -eq 1)
        isFaiComplete     = [bool]([int]$item[16] -eq 1)
        shipmentDate      = To-IsoDate $item[17]
        shipmentAddress   = Limit-String (Sanitize-JsonText $item[18]) 500
        legacyLineId      = $legacyLineId
        legacyPackageId   = $legacyPkgId
        shippedQuantity   = 0
    }
    if ($mappedCurrency) { $body.currency = $mappedCurrency }

    if ($DryRun) { $ok++; continue }

    try {
        Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri" -Body $body | Out-Null
        $lineMap[$legacyLineId] = "1"
        $parentLineMap[$parentLineKey] = $true
        $ok++
        if ($ok % 50 -eq 0) { Write-Host "  + $ok kalem ($packageNo #$lineNo)" -ForegroundColor Green }
    }
    catch {
        Write-Host "  HATA $packageNo #$lineNo (legacyLineId=$legacyLineId): $(Get-DgErrorMessage $_)" -ForegroundColor Red
        $fail++
    }
}

Write-Host "`nBitti: OK=$ok SKIP=$skip HATA=$fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
