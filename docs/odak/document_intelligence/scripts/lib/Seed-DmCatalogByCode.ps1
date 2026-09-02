function Get-DmCatalogListItems {
    param([object]$Response)
    if ($Response -is [System.Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    return @($Response)
}

function Invoke-DmCatalogSeed {
    param(
        [string]$BaseUrl,
        [string]$Token,
        [string]$Dataset,
        [string]$SeedFile,
        [string]$Label,
        [switch]$WhatIf
    )

    $token = $Token
    if ([string]::IsNullOrEmpty($token)) {
        $loadTokenScript = Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token.ps1"
        if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
    }
    if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }
    $token = $token.Trim()

    $headers = @{
        Authorization = "Bearer $token"
    }
    $dataBase = "$BaseUrl/data/api/v1/data/$Dataset"
    $utf8 = [System.Text.Encoding]::UTF8

    function Invoke-DgData {
        param([string]$Method, [string]$Uri, [hashtable]$Body = $null)
        if ($Body) {
            $json = $Body | ConvertTo-Json -Depth 12 -Compress
            $bytes = $utf8.GetBytes($json)
            return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method `
                -Body $bytes -ContentType "application/json; charset=utf-8"
        }
        return Invoke-RestMethod -Uri $Uri -Headers $headers -Method $Method
    }

    function Find-ByCode {
        param([string]$Code)
        $filter = [Uri]::EscapeDataString("code:eq:$Code")
        $uri = "${dataBase}?filter=$filter&limit=1"
        $res = Invoke-DgData -Method GET -Uri $uri
        return Get-DmCatalogListItems -Response $res | Select-Object -First 1
    }

    if (-not (Test-Path $SeedFile)) { throw "Seed dosyasi yok: $SeedFile" }
    $seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json

    Write-Host "$Label seed -> $BaseUrl ($Dataset)" -ForegroundColor Cyan

    foreach ($rec in @($seed.records)) {
        $code = [string]$rec.code
        $body = @{}
        foreach ($prop in $rec.PSObject.Properties) {
            if ($prop.Name -eq "code") { continue }
            $body[$prop.Name] = $prop.Value
        }
        $body.code = $code

        $existing = Find-ByCode -Code $code
        if ($WhatIf) {
            $action = if ($existing) { "PUT" } else { "POST" }
            Write-Host "  WhatIf $action code=$code" -ForegroundColor Yellow
            continue
        }

        if ($existing) {
            $id = if ($existing.dataId) { [string]$existing.dataId } elseif ($existing.__dataId) { [string]$existing.__dataId } else { "" }
            if ($id) {
                Invoke-DgData -Method PUT -Uri "$dataBase/$id" -Body $body | Out-Null
                Write-Host "  OK update code=$code id=$id" -ForegroundColor Green
            }
            else {
                $created = Invoke-DgData -Method POST -Uri $dataBase -Body $body
                $newId = if ($created.dataId) { [string]$created.dataId } elseif ($created.__dataId) { [string]$created.__dataId } else { [string]$created.id }
                Write-Host "  OK create code=$code id=$newId" -ForegroundColor Green
            }
        }
        else {
            $created = Invoke-DgData -Method POST -Uri $dataBase -Body $body
            $newId = if ($created.dataId) { [string]$created.dataId } elseif ($created.__dataId) { [string]$created.__dataId } else { [string]$created.id }
            Write-Host "  OK create code=$code id=$newId" -ForegroundColor Green
        }
    }

    Write-Host "Tamamlandi." -ForegroundColor Cyan
}
