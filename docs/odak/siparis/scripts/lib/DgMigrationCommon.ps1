# DG migration helpers — filter API unreliable; client-side legacy id map

function Get-DgListPage {
    param(
        [hashtable]$InvokeDg,
        [string]$BaseUrl,
        [string]$DataPath,
        [string]$Dataset,
        [int]$Skip = 0,
        [int]$Limit = 500
    )
    $uri = '{0}{1}/{2}?skip={3}&limit={4}' -f $BaseUrl, $DataPath, $Dataset, $Skip, $Limit
    $raw = & $InvokeDg -Method GET -Uri $uri
    $items = @()
    if ($raw -is [Array]) { $items = @($raw) }
    elseif ($raw.items) { $items = @($raw.items) }
    elseif ($raw.data) { $items = @($raw.data) }
    elseif ($raw.__dataId -or $raw.dataId) { $items = @($raw) }
    return $items
}

function Get-DgTotalCount {
    param(
        [hashtable]$Headers,
        [string]$BaseUrl,
        [string]$DataPath,
        [string]$Dataset
    )
    $uri = '{0}{1}/{2}?limit=1&skip=0' -f $BaseUrl, $DataPath, $Dataset
    $resp = Invoke-WebRequest -Uri $uri -Headers $Headers -UseBasicParsing -ErrorAction Stop
    $header = $resp.Headers["X-Total-Count"]
    if ($header) {
        if ($header -is [Array]) { return [int]$header[0] }
        return [int]$header
    }
    $body = $resp.Content | ConvertFrom-Json
    if ($body.total -ne $null) { return [int]$body.total }
    if ($body -is [Array]) { return $body.Count }
    if ($body.__dataId) { return 1 }
    return 0
}

function Load-LegacyIdMap {
    param(
        [scriptblock]$InvokeDg,
        [string]$BaseUrl,
        [string]$DataPath,
        [string]$Dataset,
        [string]$LegacyField
    )
    $map = @{}
    $skip = 0
    $limit = 500
    while ($true) {
        $uri = '{0}{1}/{2}?skip={3}&limit={4}' -f $BaseUrl, $DataPath, $Dataset, $skip, $limit
        $raw = & $InvokeDg -Method GET -Uri $uri
        $items = @()
        if ($raw -is [Array]) { $items = @($raw) }
        elseif ($raw.items) { $items = @($raw.items) }
        elseif ($raw.data) { $items = @($raw.data) }
        elseif ($raw.__dataId -or $raw.dataId) { $items = @($raw) }
        if (-not $items.Count) { break }
        foreach ($item in $items) {
            $legacy = [string]$item.$LegacyField
            if (-not $legacy) { continue }
            $id = $item.__dataId; if (-not $id) { $id = $item.dataId }
            if ($id) { $map[$legacy] = [string]$id }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $map
}

function Get-RelationId {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    if ($Value.__dataId) { return [string]$Value.__dataId }
    if ($Value.dataId) { return [string]$Value.dataId }
    return [string]$Value
}

function Load-ParentLineNoMap {
    param(
        [scriptblock]$InvokeDg,
        [string]$BaseUrl,
        [string]$DataPath
    )
    $map = @{}
    $skip = 0
    $limit = 500
    while ($true) {
        $uri = '{0}{1}/odak_siparis_kalemleri?skip={2}&limit={3}' -f $BaseUrl, $DataPath, $skip, $limit
        $raw = & $InvokeDg -Method GET -Uri $uri
        $items = @()
        if ($raw -is [Array]) { $items = @($raw) }
        elseif ($raw.items) { $items = @($raw.items) }
        elseif ($raw.data) { $items = @($raw.data) }
        if (-not $items.Count) { break }
        foreach ($item in $items) {
            $parentId = Get-RelationId $item.parentPackageId
            if (-not $parentId) { $parentId = Get-RelationId $item.parentWorkItemId }
            $lineNo = [string]$item.lineNo
            if ($parentId -and $lineNo) { $map["$parentId|$lineNo"] = $true }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $map
}
