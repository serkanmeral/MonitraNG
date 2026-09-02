# Teslimat Omurgasi F1-0 — DG category + dataset ensure (create or merge-sync).
# Merge-sync adds missing fields/indexes from canonical JSON; does not strip live extras
# or send empty queries (full replace can 500).

function Get-TeslimatRepoRoot {
    param([string]$FromScriptRoot)
    return (Resolve-Path (Join-Path $FromScriptRoot "../../../..")).Path
}

function Get-TeslimatToken {
    param(
        [string]$Token,
        [string]$BaseUrl,
        [string]$RepoRoot
    )
    if (-not [string]::IsNullOrWhiteSpace($Token)) { return $Token.Trim() }
    if (-not [string]::IsNullOrWhiteSpace($env:DI_TOKEN)) { return $env:DI_TOKEN.Trim() }

    $isProd = $BaseUrl -match "192\.168\.20\.8"
    $loader = if ($isProd) {
        Join-Path $RepoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"
    }
    else {
        Join-Path $RepoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $loader) {
        $loaded = & $loader
        if (-not [string]::IsNullOrWhiteSpace($loaded)) { return $loaded.Trim() }
    }
    return $null
}

function New-TeslimatDgHeaders {
    param([string]$Token)
    return @{
        Authorization = "Bearer $Token"
        "Content-Type" = "application/json"
    }
}

function Invoke-TeslimatDg {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [hashtable]$Headers,
        [string]$BodyJson = $null,
        [switch]$IgnoreNotFound
    )
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $Headers
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($BodyJson)) {
        $utf8 = [System.Text.Encoding]::UTF8
        $params.Body = $utf8.GetBytes($BodyJson)
        $params.ContentType = "application/json; charset=utf-8"
    }
    try {
        return Invoke-RestMethod @params
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        if ($IgnoreNotFound -and $code -eq 404) { return $null }
        throw
    }
}

function ConvertTo-TeslimatCleanField {
    param($Field)
    $o = [ordered]@{
        fieldType = $Field.fieldType
        name      = [string]$Field.name
        title     = [string]$Field.title
        mandatory = [bool]$Field.mandatory
        unique    = [bool]$Field.unique
        isArray   = [bool]$Field.isArray
    }
    if ($null -ne $Field.defaultValue) { $o.defaultValue = $Field.defaultValue }
    if ($Field.relationDataset) { $o.relationDataset = $Field.relationDataset }
    if ($Field.incrementalOptions) { $o.incrementalOptions = $Field.incrementalOptions }
    if ($Field.datetimeOptions) { $o.datetimeOptions = $Field.datetimeOptions }
    if ($Field.validation) { $o.validation = $Field.validation }
    if ($Field.options) { $o.options = $Field.options }
    return [pscustomobject]$o
}

function Get-TeslimatDatasetPayload {
    param($Response)
    if (-not $Response) { return $null }
    if ($Response.fields) { return $Response }
    if ($Response.data -and $Response.data.fields) { return $Response.data }
    return $Response
}

function ConvertTo-TeslimatDatasetCreateBody {
    param($Schema, [string]$CategoryId)
    return @{
        Name        = $Schema.name
        Description = $Schema.description
        Category    = $CategoryId
        ForceSchema = $Schema.forceSchema
        Logging     = $Schema.logging
        PublishMode = $Schema.publish_mode
        Fields      = $Schema.fields
        Validations = $Schema.validations
        Queries     = $Schema.queries
        IndexList   = $Schema.indexList
    }
}

function Import-TeslimatSchemaMap {
    param([string]$RepoRoot, [string[]]$RelativeFiles)
    $byName = @{}
    foreach ($rel in $RelativeFiles) {
        $path = Join-Path $RepoRoot $rel
        if (-not (Test-Path $path)) { throw "Schema file missing: $path" }
        $raw = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
        $list = @($raw)
        foreach ($schema in $list) {
            if (-not $schema.name) { continue }
            $byName[[string]$schema.name] = $schema
        }
    }
    return $byName
}

function Ensure-TeslimatDatasetCategory {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$CategoryFile,
        [switch]$WhatIf
    )
    $cat = Get-Content $CategoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $name = [string]$cat.categoryName
    $body = @{
        categoryName        = $cat.categoryName
        categoryDescription = $cat.categoryDescription
        isSystemCategory    = $cat.isSystemCategory
    } | ConvertTo-Json -Compress

    $categoriesPath = "/data/api/v1/dataset-categories"
    if ($WhatIf) {
        Write-Host "  WhatIf category $name" -ForegroundColor Yellow
        return $cat.'__dataId'
    }

    try {
        $null = Invoke-TeslimatDg -Method POST -Uri "$BaseUrl$categoriesPath" -Headers $Headers -BodyJson $body
    }
    catch {
        $msg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        Write-Host "  Category already present; resolving id" -ForegroundColor Gray
    }

    $listUri = '{0}{1}?pageSize=100&search={2}' -f $BaseUrl, $categoriesPath, [Uri]::EscapeDataString($name)
    $list = Invoke-TeslimatDg -Method GET -Uri $listUri -Headers $Headers
    $items = $list.items
    if (-not $items) { $items = $list.data }
    $found = $null
    if ($items) {
        $found = @($items) | Where-Object { $_.categoryName -eq $name } | Select-Object -First 1
    }
    if ($found) {
        $id = $found.dataId
        if (-not $id) { $id = $found.__dataId }
        Write-Host "  Category $name OK ($id)" -ForegroundColor Green
        return $id
    }
    Write-Host "  Category $name fallback JSON id" -ForegroundColor Yellow
    return $cat.'__dataId'
}

function Ensure-TeslimatDataset {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        $Schema,
        [string]$CategoryId,
        [switch]$WhatIf
    )
    $name = [string]$Schema.name
    $datasetsPath = "/data/api/v1/datasets"
    $getUri = "$BaseUrl$datasetsPath/$name"
    $wantedFields = @($Schema.fields)
    $wantedIndexes = @()
    if ($Schema.indexList) { $wantedIndexes = @($Schema.indexList) }

    $live = $null
    try {
        $live = Get-TeslimatDatasetPayload (Invoke-TeslimatDg -Method GET -Uri $getUri -Headers $Headers -IgnoreNotFound)
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        if ($code -ne 404) { throw }
    }

    if (-not $live) {
        $create = ConvertTo-TeslimatDatasetCreateBody -Schema $Schema -CategoryId $CategoryId | ConvertTo-Json -Depth 30 -Compress
        if ($WhatIf) {
            Write-Host "  WhatIf CREATE $name ($($wantedFields.Count) fields)" -ForegroundColor Yellow
            return "create"
        }
        $null = Invoke-TeslimatDg -Method POST -Uri "$BaseUrl$datasetsPath" -Headers $Headers -BodyJson $create
        Write-Host "  CREATE $name ($($wantedFields.Count) fields)" -ForegroundColor Green
        return "create"
    }

    $liveFieldNames = @($live.fields | ForEach-Object { [string]$_.name })
    $addedFields = @()
    $mergedFields = @($live.fields)
    foreach ($f in $wantedFields) {
        if ($liveFieldNames -contains [string]$f.name) { continue }
        $mergedFields += $f
        $addedFields += [string]$f.name
    }

    $liveIndexNames = @()
    $mergedIndexes = @()
    if ($live.indexList) {
        foreach ($idx in $live.indexList) {
            $mergedIndexes += $idx
            if ($idx.name) { $liveIndexNames += [string]$idx.name }
        }
    }
    $addedIndexes = @()
    foreach ($idx in $wantedIndexes) {
        $idxName = [string]$idx.name
        if ([string]::IsNullOrWhiteSpace($idxName)) { continue }
        if ($liveIndexNames -contains $idxName) { continue }
        $mergedIndexes += $idx
        $addedIndexes += $idxName
    }

    if ($addedFields.Count -eq 0 -and $addedIndexes.Count -eq 0) {
        Write-Host "  SYNC $name (already current, $($liveFieldNames.Count) fields)" -ForegroundColor Green
        return "skip"
    }

    $body = @{}
    if ($addedFields.Count -gt 0) {
        $body.fields = @($mergedFields | ForEach-Object { ConvertTo-TeslimatCleanField $_ })
        Write-Host "  +fields $($addedFields -join ', ')" -ForegroundColor Yellow
    }
    if ($addedIndexes.Count -gt 0) {
        $body.IndexList = $mergedIndexes
        Write-Host "  +indexes $($addedIndexes -join ', ')" -ForegroundColor Yellow
    }
    $json = $body | ConvertTo-Json -Depth 30 -Compress
    if ($WhatIf) {
        Write-Host "  WhatIf PUT $name" -ForegroundColor Yellow
        return "update"
    }
    $null = Invoke-TeslimatDg -Method PUT -Uri $getUri -Headers $Headers -BodyJson $json
    Write-Host "  SYNC $name" -ForegroundColor Green
    return "update"
}

function Test-TeslimatDataset {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$Name,
        [string[]]$RequiredFields
    )
    $uri = "$BaseUrl/data/api/v1/datasets/$Name"
    $payload = $null
    try {
        $payload = Get-TeslimatDatasetPayload (Invoke-TeslimatDg -Method GET -Uri $uri -Headers $Headers)
    }
    catch {
        $msg = $_.Exception.Message
        if ($_.ErrorDetails.Message) { $msg = $_.ErrorDetails.Message }
        return [pscustomobject]@{
            Name    = $Name
            Ok      = $false
            Error   = ("unreachable: " + $msg)
            Fields  = @()
            Missing = @($RequiredFields)
        }
    }
    $names = @($payload.fields | ForEach-Object { [string]$_.name })
    $missing = @()
    foreach ($f in $RequiredFields) {
        if ($names -notcontains $f) { $missing += $f }
    }
    return [pscustomobject]@{
        Name    = $Name
        Ok      = ($missing.Count -eq 0)
        Error   = if ($missing.Count -eq 0) { $null } else { "missing-fields" }
        Fields  = $names
        Missing = $missing
    }
}
