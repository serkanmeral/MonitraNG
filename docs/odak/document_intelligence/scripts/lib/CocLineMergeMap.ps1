# Siparis kalemi + paket + musteri (+ urun) -> CoC merge degerleri.
# Eksik alanlar merge edilmez; DOCX'te {{paramKey}} kalir.

function Get-DgRelationId {
    param([object]$Field)
    if ($null -eq $Field) { return $null }
    if ($Field -is [string]) {
        $t = $Field.Trim()
        if ([string]::IsNullOrWhiteSpace($t)) { return $null }
        return $t
    }
    if ($Field.PSObject.Properties['__dataId']) { return [string]$Field.__dataId }
    if ($Field.PSObject.Properties['dataId']) { return [string]$Field.dataId }
    $s = [string]$Field
    if ([string]::IsNullOrWhiteSpace($s)) { return $null }
    return $s
}

function Format-CocIssueDate {
    param([Nullable[datetime]]$Value)
    if ($Value) { return $Value.Value.ToString('dd.MM.yyyy') }
    return (Get-Date).ToString('dd.MM.yyyy')
}

function Format-CocPartQuantity {
    param([object]$Quantity, [object]$Unit)
    if ($null -eq $Quantity) { return $null }
    $q = [string]$Quantity
    if ([string]::IsNullOrWhiteSpace($q)) { return $null }
    $u = [string]$Unit
    if ([string]::IsNullOrWhiteSpace($u)) { return $q }
    return "$q $u".ToUpperInvariant()
}

function Build-CocMergeValuesFromLine {
    param(
        [object]$Line,
        [object]$Package,
        [object]$Customer,
        [object]$Product,
        [string]$DocNo,
        [hashtable]$Defaults = @{}
    )

    $values = @{}
    if ($DocNo) { $values['docNo'] = $DocNo }
    if ($Package.packageNo) { $values['workPackageNo'] = [string]$Package.packageNo.Trim() }
    if ($Customer.unvan) { $values['customerName'] = [string]$Customer.unvan.Trim() }
    if ($Line.customerPoNo) { $values['orderNo'] = [string]$Line.customerPoNo.Trim() }
    if ($Line.description) { $values['partDescription'] = [string]$Line.description.Trim() }

    $qty = Format-CocPartQuantity -Quantity $Line.quantity -Unit $Line.unit
    if ($qty) { $values['partQuantity'] = $qty }

    if ($Line.poItemRevNo) {
        $values['drawingRevision'] = [string]$Line.poItemRevNo.Trim()
    }
    elseif ($Product.revizyon) {
        $values['drawingRevision'] = [string]$Product.revizyon.Trim()
    }

    if ($Product.partNumber) { $values['drawingNo'] = [string]$Product.partNumber.Trim() }

    $issueDate = Format-CocIssueDate -Value $null
    $values['issueDate'] = $issueDate
    $values['documentName'] = if ($Defaults.documentName) { [string]$Defaults.documentName } else { 'Uygunluk Belgesi' }
    $values['generatedAt'] = $issueDate

    if ($Defaults.signatoryName) { $values['signatoryName'] = [string]$Defaults.signatoryName }
    if ($Defaults.signatoryTitle) { $values['signatoryTitle'] = [string]$Defaults.signatoryTitle }

    return $values
}
