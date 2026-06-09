# @widgets instance olusturma — setup/seed scriptleri tarafindan dot-source edilir.

function Get-RowId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    if ($row.id) { return [string]$row.id }
    if ($row.data) { return Get-RowId $row.data }
    if ($row.result) { return Get-RowId $row.result }
    return $null
}

function Get-LocalizedTitle($titleObj) {
    if ($null -eq $titleObj) { return "Widget" }
    if ($titleObj -is [string]) { return $titleObj }
    if ($titleObj.tr) { return [string]$titleObj.tr }
    if ($titleObj.en) { return [string]$titleObj.en }
    return "Widget"
}

function Get-LegacyTypeFromKind([string]$kind) {
    switch ($kind) {
        'stat' { return 'card' }
        'chart' { return 'chart' }
        'table' { return 'table' }
        'banner' { return 'banner' }
        'gauge' { return 'gauge' }
        'map' { return 'map' }
        default { return 'card' }
    }
}

function Get-CategoryId($categoryField) {
    if ($null -eq $categoryField) { return $null }
    if ($categoryField -is [string]) {
        if ($categoryField -match '^[0-9a-fA-F-]{36}$') { return $categoryField }
        return $null
    }
    return Get-RowId $categoryField
}

$script:LegacyCategorySlugToDomain = @{
    'alarm-kpi'       = 'alarm'
    'alarm-charts'    = 'alarm'
    'alarm-tables'    = 'alarm'
    'siem-kpi'        = 'siem'
    'siem-charts'     = 'siem'
    'oc-kpi'          = 'operation-core'
    'oc-work-queues'  = 'operation-core'
    'oc-sla'          = 'operation-core'
    'di-lists'        = 'document-intelligence'
    'di-quick-access' = 'document-intelligence'
}

function Get-WidgetCategoryMap {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$DataPath = '/data/api/v1/data'
    )
    $map = @{}
    $skip = 0
    $limit = 100
    while ($true) {
        $uri = "$BaseUrl$DataPath/@widget_categories?skip=$skip&limit=$limit&sort=order,name"
        try {
            $list = Invoke-RestMethod -Uri $uri -Headers $Headers -Method GET -TimeoutSec 30
        }
        catch {
            Write-Host "  Uyari: @widget_categories listelenemedi: $($_.Exception.Message)" -ForegroundColor Yellow
            break
        }
        $items = @()
        if ($list -is [array]) { $items = @($list) }
        elseif ($list.items) { $items = @($list.items) }
        elseif ($list.data) { $items = @($list.data) }
        if ($items.Count -eq 0) { break }
        foreach ($item in $items) {
            $name = $item.name
            if ([string]::IsNullOrEmpty($name)) { $name = $item.Name }
            $id = Get-RowId $item
            $desc = $item.description
            if ([string]::IsNullOrEmpty($desc)) { $desc = $item.Description }
            if (-not [string]::IsNullOrEmpty($name) -and -not [string]::IsNullOrEmpty($id)) {
                $map[$name] = $id
                if ($desc -match '^domain:(.+)$') {
                    $map[$Matches[1].ToLower()] = $id
                }
            }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $map
}

function Resolve-ModuleCategoryId {
    param(
        $TemplateRow,
        [hashtable]$CategoryMap
    )
    $categoryId = Get-CategoryId $TemplateRow.category
    $manifest = $TemplateRow.manifest
    if (-not $categoryId -and $manifest) {
        $categoryId = Get-CategoryId $manifest.category
    }
    if (-not $categoryId -and $TemplateRow.domain) {
        $domainKey = [string]$TemplateRow.domain.ToLower()
        if ($CategoryMap.ContainsKey($domainKey)) {
            return $CategoryMap[$domainKey]
        }
    }
    if (-not $categoryId -and $manifest -and $manifest.category) {
        $slug = [string]$manifest.category
        if ($script:LegacyCategorySlugToDomain.ContainsKey($slug)) {
            $mapped = $script:LegacyCategorySlugToDomain[$slug]
            if ($CategoryMap.ContainsKey($mapped)) {
                return $CategoryMap[$mapped]
            }
        }
        if ($CategoryMap.ContainsKey($slug)) {
            return $CategoryMap[$slug]
        }
    }
    return $categoryId
}

function Build-ManifestBinding($binding) {
    $fieldMap = @{}
    if ($binding.fieldMap) {
        foreach ($prop in $binding.fieldMap.PSObject.Properties) {
            $fieldMap[$prop.Name] = $prop.Value
        }
    }
    if ($binding.serviceRef -match ':static/') {
        return @{
            kind       = 'static'
            parameters = @{}
            fieldMap   = $fieldMap
        }
    }
    if ($binding.serviceRef) {
        return @{
            kind       = 'serviceRef'
            serviceRef = [string]$binding.serviceRef
            parameters = @{}
            fieldMap   = $fieldMap
        }
    }
    if ($binding.queryRef) {
        return @{
            kind       = 'queryRef'
            queryRef   = [string]$binding.queryRef
            parameters = @{}
            fieldMap   = $fieldMap
        }
    }
    return @{ kind = 'static'; parameters = @{}; fieldMap = $fieldMap }
}

function Build-LegacyDataSource($binding, [hashtable]$Parameters = @{}) {
    $manifestBinding = Build-ManifestBinding $binding
    if ($manifestBinding.kind -eq 'queryRef') {
        $queryRef = [string]$binding.queryRef
        if ($queryRef -match '^@([^/]+)/queries/(.+)$') {
            $predefinedParams = @{}
            foreach ($key in $Parameters.Keys) {
                $predefinedParams[$key] = $Parameters[$key]
            }
            return @{
                type       = 'data'
                dataset    = $Matches[1]
                getMethod  = 'predefined'
                predefined = @{
                    queryName  = $Matches[2]
                    parameters = $predefinedParams
                }
            }
        }
        throw "Gecersiz queryRef: $queryRef"
    }
    if ($manifestBinding.kind -eq 'serviceRef') {
        $mapping = @{
            items = if ($binding.fieldMap.rows) { [string]$binding.fieldMap.rows } else { 'items' }
            total = if ($binding.fieldMap.total) { [string]$binding.fieldMap.total } else { 'total' }
            value = if ($binding.fieldMap.value) { [string]$binding.fieldMap.value } else { 'value' }
        }
        return @{
            type      = 'data'
            dataset   = '__manifest_service__'
            getMethod = 'default'
            default   = @{}
            mapping   = $mapping
        }
    }
    return @{
        type      = 'data'
        dataset   = '__manifest_static__'
        getMethod = 'default'
        default   = @{ limit = 0 }
    }
}

function Get-TemplateRow {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$TemplateId,
        [string]$DataPath = '/data/api/v1/data'
    )
    $filter = [uri]::EscapeDataString("templateId:eq:$TemplateId")
    $uri = "$BaseUrl$DataPath/@widget_templates?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $uri -Headers $Headers -Method GET -TimeoutSec 30
    if ($list -is [array] -and $list.Count -gt 0) { return $list[0] }
    if ($list.items -and $list.items.Count -gt 0) { return $list.items[0] }
    return $null
}

function Get-WidgetByName {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$Name,
        [string]$DataPath = '/data/api/v1/data'
    )
    $filter = [uri]::EscapeDataString("name:eq:$Name")
    $uri = "$BaseUrl$DataPath/@widgets?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $uri -Headers $Headers -Method GET -TimeoutSec 30
    if ($list -is [array] -and $list.Count -gt 0) { return $list[0] }
    if ($list.items -and $list.items.Count -gt 0) { return $list.items[0] }
    return $null
}

function Build-WidgetBody {
    param(
        [string]$WidgetName,
        $TemplateRow,
        [hashtable]$CategoryMap,
        [hashtable]$Parameters = @{},
        [string]$TitleOverride = '',
        [int]$Order = 0
    )

    $manifest = $TemplateRow.manifest
    if (-not $manifest) { throw "Template manifest yok: $($TemplateRow.templateId)" }

    $presetId = $manifest.presentation.defaultPreset
    if (-not $presetId -and $manifest.presentation.preset) {
        $presetId = $manifest.presentation.preset
    }
    if (-not $presetId) { $presetId = 'stat-simple' }

    $kind = [string]$manifest.presentation.kind
    $legacyType = Get-LegacyTypeFromKind $kind
    $categoryId = Resolve-ModuleCategoryId -TemplateRow $TemplateRow -CategoryMap $CategoryMap
    if (-not $categoryId) {
        throw "Modul kategorisi cozulemedi: $($TemplateRow.templateId) (domain=$($TemplateRow.domain))"
    }

    $definition = ($manifest | ConvertTo-Json -Depth 40 | ConvertFrom-Json)
    $definition | Add-Member -NotePropertyName name -NotePropertyValue $WidgetName -Force
    $definition | Add-Member -NotePropertyName isActive -NotePropertyValue $true -Force
    $definition | Add-Member -NotePropertyName parameters -NotePropertyValue $Parameters -Force

    $config = @{}
    if ($manifest.presentation.config) {
        $cfgJson = $manifest.presentation.config | ConvertTo-Json -Depth 20
        $cfgObj = $cfgJson | ConvertFrom-Json
        foreach ($prop in $cfgObj.PSObject.Properties) {
            $config[$prop.Name] = $prop.Value
        }
    }
    $config['manifestBinding'] = Build-ManifestBinding $manifest.dataBinding
    $config['templateId'] = [string]$manifest.templateId
    $config['templateVersion'] = [string]$manifest.templateVersion
    $config['manifestVersion'] = [string]$manifest.manifestVersion
    $config['presentationPreset'] = $presetId
    $config['presentationKind'] = $kind
    if ($kind -eq 'stat') {
        $config['valueField'] = 'value'
    }
    $config['manifest'] = $definition

    $title = Get-LocalizedTitle $manifest.title
    if (-not [string]::IsNullOrWhiteSpace($TitleOverride)) {
        $title = $TitleOverride
    }

    return @{
        name        = $WidgetName
        title       = $title
        description = if ($TemplateRow.description) { [string]$TemplateRow.description } else { $null }
        category    = $categoryId
        type        = $legacyType
        dataSource  = Build-LegacyDataSource $manifest.dataBinding $Parameters
        config      = $config
        isActive    = $true
        order       = $Order
    }
}

function Ensure-WidgetInstance {
    param(
        [string]$BaseUrl,
        [hashtable]$Headers,
        [string]$WidgetName,
        [string]$TemplateId,
        [hashtable]$CategoryMap,
        [hashtable]$Parameters = @{},
        [string]$TitleOverride = '',
        [int]$Order = 0,
        [switch]$WhatIf,
        [switch]$Recreate,
        [string]$DataPath = '/data/api/v1/data'
    )

    $existing = Get-WidgetByName -BaseUrl $BaseUrl -Headers $Headers -Name $WidgetName -DataPath $DataPath
    if ($existing -and -not $Recreate) {
        $id = Get-RowId $existing
        Write-Host "  mevcut name=$WidgetName id=$id" -ForegroundColor Gray
        return $id
    }

    $templateRow = Get-TemplateRow -BaseUrl $BaseUrl -Headers $Headers -TemplateId $TemplateId -DataPath $DataPath
    if (-not $templateRow) {
        throw "Sablon bulunamadi: $TemplateId (once setup-widget-templates-datasets.ps1)"
    }

    $body = Build-WidgetBody -WidgetName $WidgetName -TemplateRow $templateRow -CategoryMap $CategoryMap `
        -Parameters $Parameters -TitleOverride $TitleOverride -Order $Order
    $json = ($body | ConvertTo-Json -Depth 40 -Compress)

    if ($WhatIf) {
        Write-Host "  WhatIf POST @widgets name=$WidgetName template=$TemplateId" -ForegroundColor DarkYellow
        return "whatif-$WidgetName"
    }

    if ($existing -and $Recreate) {
        $existingId = Get-RowId $existing
        Invoke-RestMethod -Uri "$BaseUrl$DataPath/@widgets/$existingId" -Headers $Headers -Method DELETE -TimeoutSec 60 | Out-Null
        Write-Host "  eski kayit silindi name=$WidgetName" -ForegroundColor DarkGray
    }

    $created = Invoke-RestMethod -Uri "$BaseUrl$DataPath/@widgets" -Headers $Headers -Method POST -Body $json -TimeoutSec 60
    $id = Get-RowId $created
    if (-not $id) {
        $again = Get-WidgetByName -BaseUrl $BaseUrl -Headers $Headers -Name $WidgetName -DataPath $DataPath
        $id = Get-RowId $again
    }
    if (-not $id) {
        throw "Widget olusturuldu ama id alinamadi: $WidgetName"
    }
    Write-Host "  OK name=$WidgetName id=$id ($TemplateId)" -ForegroundColor Green
    return $id
}

function Resolve-SeedParameterPlaceholders {
    param(
        $ParametersNode,
        [hashtable]$Context
    )
    $resolved = @{}
    if ($null -eq $ParametersNode) { return $resolved }
    foreach ($prop in $ParametersNode.PSObject.Properties) {
        $raw = [string]$prop.Value
        if ($raw -match '^\$\{(.+)\}$') {
            $key = $Matches[1]
            if (-not $Context.ContainsKey($key)) {
                throw "Seed placeholder cozulemedi: `$${key}"
            }
            $resolved[$prop.Name] = $Context[$key]
        }
        else {
            $resolved[$prop.Name] = $prop.Value
        }
    }
    return $resolved
}
