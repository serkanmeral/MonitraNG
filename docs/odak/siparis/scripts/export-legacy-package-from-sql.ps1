# Legacy Kalite SQL dump — tek is paketi + kalemler JSON export (MySQL gerekmez)
#
# Usage:
#   .\export-legacy-package-from-sql.ps1 -PackageNo "2020-039"
#   .\export-legacy-package-from-sql.ps1 -PackageNo "2018-004" -SqlDumpPath "C:\...\01-kalite.sql"
#
# Not: 01-kalite.sql dump'inda packageitems kismi sinirli olabilir; kalemsiz paketler yine export edilir.

param(
    [Parameter(Mandatory = $true)]
    [string]$PackageNo,

    [string]$SqlDumpPath = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

if ([string]::IsNullOrEmpty($SqlDumpPath)) {
    $SqlDumpPath = Join-Path $env:USERPROFILE "kalite-legacy-docker\db\init\01-kalite.sql"
}
if ([string]::IsNullOrEmpty($OutputFile)) {
    $safeName = ($PackageNo -replace '[^\w\-]', '_')
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-package-$safeName.json"
}

if (-not (Test-Path $SqlDumpPath)) {
    throw "SQL dump bulunamadi: $SqlDumpPath"
}

function Split-SqlTuples {
    param([string]$Body)
    $tuples = [System.Collections.Generic.List[string]]::new()
    $depth = 0
    $start = -1
    for ($i = 0; $i -lt $Body.Length; $i++) {
        $c = $Body[$i]
        if ($c -eq '(') {
            if ($depth -eq 0) { $start = $i }
            $depth++
        }
        elseif ($c -eq ')') {
            $depth--
            if ($depth -eq 0 -and $start -ge 0) {
                $tuples.Add($Body.Substring($start, $i - $start + 1))
                $start = -1
            }
        }
    }
    return $tuples
}

function Split-SqlFields {
    param([string]$Inner)
    return [regex]::Split($Inner, ",(?=(?:[^']*'[^']*')*[^']*$)")
}

function Parse-SqlValue {
    param([string]$Raw)
    if ($null -eq $Raw) { return $null }
    $s = $Raw.Trim()
    if ($s -eq 'NULL') { return $null }
    if ($s.StartsWith("'") -and $s.EndsWith("'")) {
        return $s.Substring(1, $s.Length - 2).Replace("''", "'")
    }
    return $s
}

function Get-InsertBody {
    param([string]$Path, [string]$TableName)
    $pattern = "INSERT INTO ``$TableName`` VALUES "
    $line = (Select-String -Path $Path -Pattern ([regex]::Escape("INSERT INTO ``$TableName`` VALUES")) | Select-Object -First 1).Line
    if (-not $line) { return $null }
    return ($line -replace "^INSERT INTO ``$TableName`` VALUES ", "")
}

Write-Host "Export from SQL: $PackageNo" -ForegroundColor Cyan
Write-Host "  Dump: $SqlDumpPath" -ForegroundColor Gray

$pkgBody = Get-InsertBody -Path $SqlDumpPath -TableName "packages"
if (-not $pkgBody) { throw "packages INSERT bulunamadi" }

$firmBody = Get-InsertBody -Path $SqlDumpPath -TableName "firms"
$firmById = @{}
if ($firmBody) {
    foreach ($t in (Split-SqlTuples $firmBody)) {
        $p = Split-SqlFields ($t.Trim('()'))
        $fid = Parse-SqlValue $p[0]
        $fname = Parse-SqlValue $p[4]
        if ($fid) { $firmById[$fid] = $fname }
    }
}

$pkgRow = $null
foreach ($t in (Split-SqlTuples $pkgBody)) {
    $p = Split-SqlFields ($t.Trim('()'))
    $no = Parse-SqlValue $p[1]
    if ($no -eq $PackageNo) {
        $custId = Parse-SqlValue $p[3]
        $firmName = if ($custId -and $firmById.ContainsKey($custId)) { $firmById[$custId] } else { $null }
        $pkgRow = [ordered]@{
            id                     = Parse-SqlValue $p[0]
            package_no             = $no
            name                   = Parse-SqlValue $p[4]
            customer_id            = $custId
            status                 = Parse-SqlValue $p[19]
            begin_date             = Parse-SqlValue $p[20]
            delivery_date          = Parse-SqlValue $p[21]
            address                = Parse-SqlValue $p[10]
            notes                  = Parse-SqlValue $p[11]
            part_count             = Parse-SqlValue $p[16]
            po_version             = Parse-SqlValue $p[8]
            firm_name              = $firmName
        }
        break
    }
}

if (-not $pkgRow) { throw "Paket bulunamadi: $PackageNo" }

$items = @()
$piBody = Get-InsertBody -Path $SqlDumpPath -TableName "packageitems"
if ($piBody) {
    $legacyPkgId = [string]$pkgRow.id
    foreach ($t in (Split-SqlTuples $piBody)) {
        $p = Split-SqlFields ($t.Trim('()'))
        $pkgId = Parse-SqlValue $p[1]
        if ([string]$pkgId -ne $legacyPkgId) { continue }
        $items += [ordered]@{
            id                  = Parse-SqlValue $p[0]
            package_id          = $pkgId
            customer_project_no = Parse-SqlValue $p[2]
            number              = Parse-SqlValue $p[3]
            customer_po_no      = Parse-SqlValue $p[4]
            customer_po_item_no = Parse-SqlValue $p[5]
            description         = (Parse-SqlValue $p[6])?.Trim()
            po_item_rev_no      = Parse-SqlValue $p[7]
            customer_job_no     = Parse-SqlValue $p[8]
            count               = Parse-SqlValue $p[9]
            unit                = Parse-SqlValue $p[10]
            unit_cost           = Parse-SqlValue $p[11]
            total_cost          = Parse-SqlValue $p[12]
            currency            = Parse-SqlValue $p[13]
            quality_reqs        = Parse-SqlValue $p[14]
            isfai               = Parse-SqlValue $p[15]
            shipment_date       = Parse-SqlValue $p[17]
            shipment_address    = Parse-SqlValue $p[18]
        }
    }
}

$export = @{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    packageNo  = $PackageNo
    package    = $pkgRow
    items      = $items
    source     = @{
        sqlDump = (Resolve-Path $SqlDumpPath).Path
        note    = "export-legacy-package-from-sql.ps1"
    }
}

$export | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "OK: $($items.Count) kalem -> $OutputFile" -ForegroundColor Green
