# Legacy Kalite SQL dump — firma snapshot JSON export (MySQL gerekmez)
#
# Usage:
#   .\export-legacy-firm-snapshot-from-sql.ps1 -LegacyFirmId 143
#   .\export-legacy-firm-snapshot-from-sql.ps1 -LegacyFirmId 143 -SupplierNamePattern "METIN Y"

param(
    [Parameter(Mandatory = $true)]
    [string]$LegacyFirmId,

    [string]$SupplierNamePattern = "",
    [string]$SqlDumpPath = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")

$SqlDumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
$mappingFile = Join-Path $scriptDir "..\datasets\migration-firm-mapping.json"

if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-firm-$LegacyFirmId-snapshot.json"
}

function New-RowObject {
    param([string[]]$Columns, [array]$Values)
    $obj = [ordered]@{}
    for ($i = 0; $i -lt $Columns.Count; $i++) {
        $key = $Columns[$i]
        $val = if ($i -lt $Values.Count) { $Values[$i] } else { $null }
        $obj[$key] = $val
    }
    return [pscustomobject]$obj
}

function Test-SupplierNameMatch {
    param([string]$Name, [string]$Pattern)
    if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
    if ([string]::IsNullOrWhiteSpace($Pattern)) { return $false }
    $opts = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor `
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    return [regex]::IsMatch($Name, $Pattern, $opts)
}

function Get-SqlTableBodyRaw {
    param([string]$Path, [string]$TableName)
    $prefix = "INSERT INTO ``$TableName`` VALUES "
    $lines = [System.Collections.Generic.List[string]]::new()
    $reader = [System.IO.StreamReader]::new($Path, [System.Text.Encoding]::UTF8, $true)
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.StartsWith($prefix)) {
                [void]$lines.Add($line.Substring($prefix.Length))
            }
        }
    }
    finally {
        $reader.Close()
    }
    if ($lines.Count -eq 0) { return $null }
    return ($lines -join "")
}

function Import-RegexPosRows {
    param([string]$Body, [string]$LegacyFirmId, [string]$NamePattern)
    $rows = [System.Collections.Generic.List[object]]::new()
    if (-not $Body) { return $rows }
    $rx = [regex]"\('(?<ponum>PO\d+)','?(?<suppid>\d+)'?,?'(?<suppname>[^']*)','?(?<contact>[^']*)',(?<status>-?\d+),'?(?<note>[^']*)'\)"
    foreach ($m in $rx.Matches($Body)) {
        $suppName = $m.Groups["suppname"].Value
        $suppId = $m.Groups["suppid"].Value
        if ($suppId -ne $LegacyFirmId -and -not (Test-SupplierNameMatch -Name $suppName -Pattern $NamePattern)) {
            continue
        }
        $rows.Add([pscustomobject]@{
                ponum    = $m.Groups["ponum"].Value
                suppid   = $suppId
                suppname = $suppName
                contact  = $m.Groups["contact"].Value
                status   = $m.Groups["status"].Value
                note     = $m.Groups["note"].Value
            })
    }
    return $rows
}

function Import-RegexItemRows {
    param([string]$Body, [string]$NamePattern)
    $rows = [System.Collections.Generic.List[object]]::new()
    if (-not $Body) { return $rows }
    $rx = [regex]"\('(?<stock>[^']+)','(?<category>[^']+)','(?<warehouse>[^']+)','(?<supplier>[^']*)','?(?<supplier_no>[^']*)',?'(?<description>[^']*)',(?<qty>[^,]+),'?(?<unit>[^']*)',(?<unit_cost>[^,]*),'?(?<note>[^']*)'\)"
    foreach ($m in $rx.Matches($Body)) {
        $supplier = $m.Groups["supplier"].Value
        if (-not (Test-SupplierNameMatch -Name $supplier -Pattern $NamePattern)) { continue }
        $rows.Add([pscustomobject]@{
                stock_code  = $m.Groups["stock"].Value
                category    = $m.Groups["category"].Value
                warehouse   = $m.Groups["warehouse"].Value
                supplier    = $supplier
                supplier_no = $m.Groups["supplier_no"].Value
                description = $m.Groups["description"].Value
                qty         = $m.Groups["qty"].Value
                unit        = $m.Groups["unit"].Value
                unit_cost   = $m.Groups["unit_cost"].Value
                note        = $m.Groups["note"].Value
            })
    }
    return $rows
}

function Import-RegexPoDetailRows {
    param([string]$Body, [System.Collections.Generic.HashSet[string]]$PoNumbers)
    $rows = [System.Collections.Generic.List[object]]::new()
    if (-not $Body -or $PoNumbers.Count -eq 0) { return $rows }
    $rx = [regex]"\((?<id>\d+),'(?<ponum>PO\d+)','(?<reference>[^']*)',(?<line>\d+),'?(?<types>[^']*)',?'(?<descript>[^']*)',(?<cost>[^,]+),(?<qty>[^,]+),'?(?<unit>[^']*)','?(?<jobno>[^']*)','?(?<note>[^']*)','?(?<due>[^']*)','?(?<exp>[^']*)','?(?<req>[^']*)'\)"
    foreach ($m in $rx.Matches($Body)) {
        $ponum = $m.Groups["ponum"].Value
        if (-not $PoNumbers.Contains($ponum)) { continue }
        $rows.Add([pscustomobject]@{
                id        = $m.Groups["id"].Value
                ponum     = $ponum
                reference = $m.Groups["reference"].Value
                line      = $m.Groups["line"].Value
                types     = $m.Groups["types"].Value
                descript  = $m.Groups["descript"].Value
                cost      = $m.Groups["cost"].Value
                qty       = $m.Groups["qty"].Value
                unit      = $m.Groups["unit"].Value
                jobno     = $m.Groups["jobno"].Value
                note      = $m.Groups["note"].Value
                due_date  = $m.Groups["due"].Value
                exp_date  = $m.Groups["exp"].Value
                req_date  = $m.Groups["req"].Value
            })
    }
    return $rows
}

function Import-RegexNcrRows {
    param([string]$Body, [string]$NamePattern, [string[]]$PackageIds)
    $rows = [System.Collections.Generic.List[object]]::new()
    if (-not $Body) { return $rows }
    $rx = [regex]"\((?<id>\d+),(?<form_year>\d+),(?<form_no>\d+),'(?<nc_no>[^']*)',(?<package_id>[^,]*),'?(?<product_id>[^']*)',?'(?<nc_date>[^']*)',?'?(?<closure_date>[^']*)',?'?(?<control_type>[^']*)',?'(?<explanation>[^']*)',(?<part_count>[^,]*),'?(?<job_no>[^']*)',?'?(?<product_code>[^']*)',?'?(?<descriptor>[^']*)'"
    foreach ($m in $rx.Matches($Body)) {
        $descriptor = $m.Groups["descriptor"].Value
        $explanation = $m.Groups["explanation"].Value
        $packageId = ($m.Groups["package_id"].Value -replace "^NULL$", "").Trim()
        $matchText = "$descriptor $explanation"
        $matchPackage = $PackageIds -contains $packageId
        $matchName = Test-SupplierNameMatch -Name $matchText -Pattern $NamePattern
        if (-not ($matchPackage -or $matchName)) { continue }
        $rows.Add([pscustomobject]@{
                id           = $m.Groups["id"].Value
                nc_no        = $m.Groups["nc_no"].Value
                package_id   = if ([string]::IsNullOrWhiteSpace($packageId)) { $null } else { $packageId }
                nc_date      = $m.Groups["nc_date"].Value
                control_type = $m.Groups["control_type"].Value
                explanation  = $explanation
                part_count   = $m.Groups["part_count"].Value
                job_no       = $m.Groups["job_no"].Value
                product_code = $m.Groups["product_code"].Value
                descriptor   = $descriptor
            })
    }
    return $rows
}

Write-Host "`n=== export-legacy-firm-snapshot-from-sql ===" -ForegroundColor Cyan
Write-Host "Firm id: $LegacyFirmId" -ForegroundColor Cyan
Write-Host "SQL dump: $SqlDumpPath" -ForegroundColor Gray
Write-Host "Output: $OutputFile`n" -ForegroundColor Cyan

$firmRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "firms"
$firmRow = $firmRows | Where-Object { [string]$_[0] -eq $LegacyFirmId } | Select-Object -First 1
if (-not $firmRow) {
    throw "firms tablosunda legacyFirmId=$LegacyFirmId bulunamadi."
}

$firmColumns = @(
    "id", "code", "is_supplier", "is_customer", "short_name", "name",
    "country", "city", "phone", "fax", "address", "invoice_address",
    "email", "district", "tax_no", "created", "created_by", "modified", "modified_by"
)
$firm = New-RowObject -Columns $firmColumns -Values $firmRow

if ([string]::IsNullOrWhiteSpace($SupplierNamePattern)) {
    $SupplierNamePattern = 'metin\s+yoney|metin\s+y[öo]ney'
}
Write-Host "Supplier name pattern: $SupplierNamePattern" -ForegroundColor Gray

$packageRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "packages"
$packageColumns = @(
    "id", "package_no", "customer_id", "name", "polink", "porlink", "po_version",
    "responsible", "design_responsible", "manufacture_responsible", "contact_id",
    "part_count", "stock_count", "shipped_count", "status", "begin_date", "delivery_date",
    "address", "payment_detail", "notes", "created", "created_by", "modified", "modified_by",
    "extra1", "extra2", "extra3"
)
$packages = @(
    foreach ($row in $packageRows) {
        if ([string]$row[2] -eq $LegacyFirmId) {
            New-RowObject -Columns $packageColumns -Values $row
        }
    }
)

$packageIds = @($packages | ForEach-Object { [string]$_.id })
$lineRows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "packageitems"
$lineColumns = @(
    "id", "package_id", "customer_project_no", "number", "customer_po_no", "customer_po_item_no",
    "description", "po_item_rev_no", "customer_job_no", "count", "unit", "unit_cost", "total_cost",
    "currency", "quality_reqs", "isfai", "faicomp", "shipment_date", "shipment_address",
    "created", "created_by", "modified", "modified_by"
)
$packageLines = @(
    foreach ($row in $lineRows) {
        if ($packageIds -contains [string]$row[1]) {
            New-RowObject -Columns $lineColumns -Values $row
        }
    }
)

$posBody = Get-SqlTableBodyRaw -Path $SqlDumpPath -TableName "pos"
$posList = Import-RegexPosRows -Body $posBody -LegacyFirmId $LegacyFirmId -NamePattern $SupplierNamePattern

$poNumbers = @($posList | ForEach-Object { [string]$_.ponum } | Sort-Object -Unique)
$poSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($p in $poNumbers) { [void]$poSet.Add($p) }

$podBody = Get-SqlTableBodyRaw -Path $SqlDumpPath -TableName "podetails"
$poDetails = Import-RegexPoDetailRows -Body $podBody -PoNumbers $poSet

$itemsBody = Get-SqlTableBodyRaw -Path $SqlDumpPath -TableName "items"
$stockItems = Import-RegexItemRows -Body $itemsBody -NamePattern $SupplierNamePattern

$contactsBody = Get-SqlTableBodyRaw -Path $SqlDumpPath -TableName "contacts"
$contacts = [System.Collections.Generic.List[object]]::new()
if ($contactsBody) {
    $contactColumns = @(
        "id", "firm_id", "name", "surname", "position", "tel", "fax",
        "email", "address", "details", "created", "created_by", "modified", "modified_by"
    )
    $contactRx = [regex]"\((?<id>\d+),(?<firm_id>\d+),'?(?<name>[^']*)','?(?<surname>[^']*)','?(?<position>[^']*)','?(?<tel>[^']*)','?(?<fax>[^']*)','?(?<email>[^']*)','?(?<address>[^']*)','?(?<details>[^']*)'"
    foreach ($m in $contactRx.Matches($contactsBody)) {
        if ($m.Groups["firm_id"].Value -ne $LegacyFirmId) { continue }
        $contacts.Add([pscustomobject]@{
                id       = $m.Groups["id"].Value
                firm_id  = $m.Groups["firm_id"].Value
                name     = $m.Groups["name"].Value
                surname  = $m.Groups["surname"].Value
                position = $m.Groups["position"].Value
                tel      = $m.Groups["tel"].Value
                fax      = $m.Groups["fax"].Value
                email    = $m.Groups["email"].Value
                address  = $m.Groups["address"].Value
                details  = $m.Groups["details"].Value
            })
    }
}

$ncsBody = Get-SqlTableBodyRaw -Path $SqlDumpPath -TableName "ncs"
$ncrList = [System.Collections.Generic.List[object]]::new()
foreach ($item in @(Import-RegexNcrRows -Body $ncsBody -NamePattern $SupplierNamePattern -PackageIds $packageIds)) {
    [void]$ncrList.Add($item)
}

if ($ncrList.Count -eq 0) {
    $ncrExportPath = Join-Path $scriptDir "..\datasets\legacy-ncs-cpas.json"
    if (Test-Path $ncrExportPath) {
        Write-Host "  NCR fallback: legacy-ncs-cpas.json" -ForegroundColor Gray
        $ncrRaw = Get-Content $ncrExportPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($nc in @($ncrRaw.ncs)) {
            $matchText = "$($nc.descriptor) $($nc.explanation)"
            $matchPackage = $packageIds -contains [string]$nc.package_id
            $matchName = Test-SupplierNameMatch -Name $matchText -Pattern $SupplierNamePattern
            if ($matchPackage -or $matchName) {
                [void]$ncrList.Add([pscustomobject]@{
                        id           = $nc.id
                        nc_no        = $nc.nc_no
                        package_id   = $nc.package_id
                        nc_date      = $nc.nc_date
                        control_type = $nc.control_type
                        explanation  = $nc.explanation
                        part_count   = $nc.part_count
                        job_no       = $nc.job_no
                        product_code = $nc.product_code
                        descriptor   = $nc.descriptor
                        nc_status    = $nc.nc_status
                        responsible  = $nc.responsible
                        return_count = $nc.return_count
                        closure_date = $nc.closure_date
                    })
            }
        }
    }
}

$dgMapping = $null
if (Test-Path $mappingFile) {
    $mapRaw = Get-Content $mappingFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $dgId = $mapRaw.firms.$LegacyFirmId
    if ($dgId) {
        $num = [int]$LegacyFirmId
        $dgMapping = [pscustomobject]@{
            legacyFirmId           = $LegacyFirmId
            odak_musteriler_dataId = $dgId
            musteriKod             = "MUS-{0:D3}" -f $num
            mappingSource          = "migration-firm-mapping.json"
            migratedAt             = $mapRaw.migratedAt
        }
    }
}

$export = [ordered]@{
    exportedAt           = (Get-Date).ToUniversalTime().ToString("o")
    legacyFirmId         = $LegacyFirmId
    displayName          = [string]$firm.name
    shortName            = [string]$firm.short_name
    supplierNamePattern  = $SupplierNamePattern
    source               = [ordered]@{
        sqlDump        = $SqlDumpPath
        exportScript   = "docs/odak/siparis/scripts/export-legacy-firm-snapshot-from-sql.ps1"
        legacyModule   = "Kalite — firms + pos/podetails (tedarik) + items + ncs"
        notInScope     = @(
            "odak_is_paketleri: legacy packages tablosunda customer_id=$LegacyFirmId kaydi yok",
            "itemh (stok hareketleri): hacim nedeniyle bu snapshot'a dahil edilmedi",
            "pos.suppid alani firms.id ile birebir eslesmeyebilir; suppname ile eslestirildi"
        )
    }
    summary              = [ordered]@{
        packagesAsCustomer = $packages.Count
        packageLines       = $packageLines.Count
        purchaseOrders     = $posList.Count
        purchaseOrderLines = $poDetails.Count
        stockItems         = $stockItems.Count
        ncrs               = $ncrList.Count
        contacts           = $contacts.Count
    }
    monitraNgMigration   = $dgMapping
    firm                 = $firm
    packagesAsCustomer   = @($packages)
    packageLines         = @($packageLines)
    purchaseOrders       = @($posList)
    purchaseOrderDetails = @($poDetails)
    stockItems           = @($stockItems)
    ncrs                 = @($ncrList)
    contacts             = @($contacts)
}

$export | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "OK: $($firm.name)" -ForegroundColor Green
Write-Host "  packages=$($packages.Count) pos=$($posList.Count) podetails=$($poDetails.Count) items=$($stockItems.Count) ncr=$($ncrList.Count) contacts=$($contacts.Count)" -ForegroundColor Green
Write-Host "  -> $OutputFile" -ForegroundColor Green
