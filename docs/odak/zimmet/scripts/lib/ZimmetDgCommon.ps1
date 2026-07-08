# Zimmet — DG ortak yardimcilar
# Dot-source: . (Join-Path $PSScriptRoot "lib/ZimmetDgCommon.ps1")

function Initialize-ZimmetDgSession {
    param(
        [string]$BaseUrl = "http://192.168.20.20:5040",
        [switch]$UseGateway = $true,
        [string]$RepoRoot = ""
    )
    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        # lib/ -> scripts/ -> zimmet/ -> odak/ -> docs/ -> repo root
        $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../../..")).Path
    }
    $ocTokenScript = Join-Path $RepoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    if (-not (Test-Path $ocTokenScript)) { throw "Token script yok: $ocTokenScript" }
    $token = & $ocTokenScript
    if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi. Once get-operationcore-token.ps1 calistirin." }

    $dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
    $datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
    $categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

    $headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type"  = "application/json"
    }
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    $irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
    if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
        $irmParams.SkipCertificateCheck = $true
    }

    return [pscustomobject]@{
        BaseUrl        = $BaseUrl
        RepoRoot       = $RepoRoot
        Token          = $token
        Headers        = $headers
        IrmParams      = $irmParams
        DataPath       = $dataPath
        DatasetsPath   = $datasetsPath
        CategoriesPath = $categoriesPath
    }
}

function Invoke-ZimmetDg {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Ctx,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body = $null
    )
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $Ctx.Headers
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and $Ctx.IrmParams.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 30 -Compress }
        $params.ContentType = "application/json"
    }
    return Invoke-RestMethod @params
}

function Get-ZimmetItems {
    param($Response)
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items", "Items", "results", "Results")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return $items }
            return @($items)
        }
    }
    return @($Response)
}

function Get-ZimmetDataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Convert-ZimmetFieldOptions {
    param($Options)
    if (-not $Options) { return $null }
    $lookup = $Options.lookup
    if (-not $lookup) { return $null }
    if ($lookup.staticItems) {
        $items = @($lookup.staticItems | ForEach-Object {
            @{ value = [string]$_.value; label = [string]$_.label }
        })
        return @{ lookup = @{ source = [string]$lookup.source; staticItems = $items } }
    }
    if ($lookup.source) {
        return @{ lookup = @{ source = [string]$lookup.source } }
    }
    return $null
}

function Ensure-ZimmetDatasetCategory {
    param(
        [pscustomobject]$Ctx,
        [string]$CategoryName = "BusinessDatasets",
        [string]$CategoryFile = ""
    )
    $listUri = '{0}{1}?pageSize=200' -f $Ctx.BaseUrl, $Ctx.CategoriesPath
    $found = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $listUri)) |
        Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if ($found) {
        $id = $found.dataId; if (-not $id) { $id = $found.__dataId }
        Write-Host "  Category mevcut: $CategoryName ($id)" -ForegroundColor Yellow
        return $id
    }
    if ([string]::IsNullOrWhiteSpace($CategoryFile)) {
        $CategoryFile = Join-Path $Ctx.RepoRoot "docs/odak/is_surecleri/datasets/odak_business_dataset_category.json"
    }
    $cat = Get-Content $CategoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $body = @{
        categoryName        = $cat.categoryName
        categoryDescription = $cat.categoryDescription
        isSystemCategory    = $false
    }
    try { Invoke-ZimmetDg -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.CategoriesPath)" -Body $body | Out-Null } catch { }
    $found2 = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $listUri)) |
        Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if (-not $found2) { throw "Category olusturulamadi: $CategoryName" }
    $id = $found2.dataId; if (-not $id) { $id = $found2.__dataId }
    Write-Host "  OK: Category $CategoryName -> $id" -ForegroundColor Green
    return $id
}

function Sync-ZimmetDatasetSchema {
    param(
        [pscustomobject]$Ctx,
        [string]$CategoryId,
        [string]$DatasetFile
    )
    $schema = Get-Content $DatasetFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $name = $schema.name
    $getUri = '{0}{1}/{2}' -f $Ctx.BaseUrl, $Ctx.DatasetsPath, [Uri]::EscapeDataString($name)
    $exists = $false
    try {
        $null = Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $getUri
        $exists = $true
    }
    catch { }

    $fields = @($schema.fields | ForEach-Object {
        $f = @{
            fieldType = $_.fieldType
            name      = $_.name
            title     = $_.title
            mandatory = $_.mandatory
            unique    = $_.unique
            isArray   = $_.isArray
        }
        if ($_.relationDataset) { $f.relationDataset = $_.relationDataset }
        if ($_.defaultValue -ne $null) { $f.defaultValue = $_.defaultValue }
        if ($_.validation) { $f.validation = $_.validation }
        if ($_.incrementalOptions) { $f.incrementalOptions = $_.incrementalOptions }
        $fieldOptions = Convert-ZimmetFieldOptions -Options $_.options
        if ($fieldOptions) { $f.options = $fieldOptions }
        $f
    })

    $indexList = @()
    if ($schema.indexList) { $indexList = @($schema.indexList) }

    if ($exists) {
        $body = @{
            Description = $schema.description
            ForceSchema = $schema.forceSchema
            Logging     = $schema.logging
            PublishMode = $schema.publish_mode
            Fields      = $fields
            IndexList   = $indexList
        }
        Invoke-ZimmetDg -Ctx $Ctx -Method PUT -Uri $getUri -Body $body | Out-Null
        Write-Host "  SYNC: dataset $name ($($fields.Count) alan)" -ForegroundColor Green
    }
    else {
        $body = @{
            Name        = $name
            Description = $schema.description
            Category    = $CategoryId
            ForceSchema = $schema.forceSchema
            Logging     = $schema.logging
            PublishMode = $schema.publish_mode
            Fields      = $fields
            IndexList   = $indexList
        }
        Invoke-ZimmetDg -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.DatasetsPath)" -Body $body | Out-Null
        Write-Host "  OK: dataset $name olusturuldu" -ForegroundColor Green
    }
}

function Ensure-ZimmetAutomatedForm {
    param(
        [pscustomobject]$Ctx,
        [string]$FormFile
    )
    $formDef = Get-Content $FormFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $formCode = $formDef.formCode
    $filter = "formCode:eq:$formCode"
    $uri = "$($Ctx.BaseUrl)$($Ctx.DataPath)/@automated_forms?limit=1&filter=$([Uri]::EscapeDataString($filter))"
    $existing = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $uri))
    $body = @{
        formName       = $formDef.formName
        formCode       = $formDef.formCode
        description    = $formDef.description
        datasetName    = $formDef.datasetName
        isActive       = $formDef.isActive
        sideMenuConfig = $formDef.sideMenuConfig
        listConfig     = $formDef.listConfig
        formConfig     = $formDef.formConfig
    }
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Invoke-ZimmetDg -Ctx $Ctx -Method PUT -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/@automated_forms/$id" -Body $body | Out-Null
        Write-Host "  SYNC: form $formCode ($id)" -ForegroundColor Yellow
        return $id
    }
    $created = Invoke-ZimmetDg -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/@automated_forms" -Body $body
    $id = Get-ZimmetDataId $created
    Write-Host "  OK: form $formCode -> $id" -ForegroundColor Green
    return $id
}

function Find-ZimmetOrCreate {
    param(
        [pscustomobject]$Ctx,
        [string]$Collection,
        [string]$Filter,
        [object]$Body,
        [string]$Label
    )
    $uri = "$($Ctx.BaseUrl)$($Ctx.DataPath)/$Collection`?limit=5&filter=$([Uri]::EscapeDataString($Filter))"
    $existing = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $uri))
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Write-Host "  SKIP: $Label ($id)" -ForegroundColor Yellow
        return $id
    }
    try {
        $created = Invoke-ZimmetDg -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$Collection" -Body $Body
        $id = Get-ZimmetDataId $created
        Write-Host "  OK: $Label -> $id" -ForegroundColor Green
        return $id
    }
    catch {
        $retry = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $uri))
        if ($retry.Count -gt 0) {
            $id = $retry[0].__dataId; if (-not $id) { $id = $retry[0].dataId }
            Write-Host "  SKIP: $Label (duplicate -> $id)" -ForegroundColor Yellow
            return $id
        }
        throw
    }
}

function Sync-ZimmetRecord {
    param(
        [pscustomobject]$Ctx,
        [string]$Collection,
        [string]$Filter,
        [object]$Body,
        [string]$Label
    )
    $uri = "$($Ctx.BaseUrl)$($Ctx.DataPath)/$Collection`?limit=1&filter=$([Uri]::EscapeDataString($Filter))"
    $existing = @(Get-ZimmetItems (Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $uri))
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Invoke-ZimmetDg -Ctx $Ctx -Method PUT -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$Collection/$id" -Body $Body | Out-Null
        Write-Host "  SYNC: $Label ($id)" -ForegroundColor Yellow
        return $id
    }
    $created = Invoke-ZimmetDg -Ctx $Ctx -Method POST -Uri "$($Ctx.BaseUrl)$($Ctx.DataPath)/$Collection" -Body $Body
    $id = Get-ZimmetDataId $created
    Write-Host "  OK: $Label -> $id" -ForegroundColor Green
    return $id
}

function Get-KeeperActiveUsers {
    param(
        [pscustomobject]$Ctx,
        [int]$PageSize = 30
    )
    $keeperUrl = "$($Ctx.BaseUrl)/keeper/api/user?pageSize=$PageSize"
    $resp = Invoke-ZimmetDg -Ctx $Ctx -Method GET -Uri $keeperUrl
    $users = @()
    if ($resp.users) { $users = @($resp.users) }
    elseif ($resp.data) { $users = @($resp.data) }
    elseif ($resp -is [Array]) { $users = $resp }
    return @($users | Where-Object {
        $uid = $_.userId; if (-not $uid) { $uid = $_.id }
        -not [string]::IsNullOrWhiteSpace([string]$uid)
    } | Select-Object -First $PageSize)
}

function New-ZimmetLookupOptions {
    param(
        [string]$DatasetName,
        [string]$LabelField = "ad",
        [string[]]$SearchFields = @("ad", "kod"),
        [string]$Filter = "aktif:eq:true",
        [hashtable]$DependsOn = $null
    )
    $lookup = @{
        source       = "dataset"
        presentation = "autocomplete"
        valueField   = "__dataId"
        labelField   = $LabelField
        searchFields = $SearchFields
        pageSize     = 50
    }
    if ($Filter) { $lookup.filter = $Filter }
    if ($DependsOn) { $lookup.dependsOn = $DependsOn }
    return @{ lookup = $lookup }
}

function New-ZimmetStaticSelectOptions {
    param([array]$Items)
    return @{
        lookup = @{
            source       = "static"
            presentation = "dropdown"
            staticItems  = $Items
        }
    }
}

function New-ZimmetTransition {
    param(
        [string]$Key,
        [string]$From,
        [string]$To,
        [string]$Label,
        [int]$Order,
        [string[]]$Required = @()
    )
    $h = [ordered]@{
        transitionKey = $Key
        fromStateId   = $From
        toStateId     = $To
        label         = $Label
        order         = $Order
    }
    if ($Required.Count -gt 0) { $h.requiredFields = @($Required) }
    return [hashtable]$h
}
