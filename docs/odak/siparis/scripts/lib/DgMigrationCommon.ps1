# DG migration helpers — filter API unreliable; client-side legacy id map

function Sanitize-LegacyText {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    $text = [string]$Value
    if ([string]::IsNullOrEmpty($text)) { return $text }
    # Fix mojibake: UTF-8 bytes previously interpreted as ISO-8859-1 (common with Turkish "ı", "ş", etc.)
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

function Limit-LegacyText {
    param([object]$Value, [int]$MaxLength)
    if ($null -eq $Value) { return $null }
    $text = Sanitize-LegacyText $Value
    if ($text.Length -le $MaxLength) { return $text }
    return $text.Substring(0, $MaxLength)
}

# Backward-compatible alias used by migrate-remaining-lines.ps1
function Sanitize-JsonText {
    param([object]$Value)
    return Sanitize-LegacyText $Value
}

function Initialize-DgMigrationHeaders {
    param([string]$TokenScriptPath)
    $token = & $TokenScriptPath
    if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }
    return @{
        TokenScript = $TokenScriptPath
        Headers     = @{
            "Authorization" = "Bearer $token"
            "Content-Type"  = "application/json"
        }
    }
}

function Update-DgMigrationToken {
    param([hashtable]$AuthContext)
    $token = & $AuthContext.TokenScript
    if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }
    $AuthContext.Headers.Authorization = "Bearer $token"
}

function Invoke-DgMigrationApi {
    param(
        [hashtable]$AuthContext,
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null,
        [switch]$RetryOnUnauthorized
    )
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        $p = @{ Uri = $Uri; Method = $Method; Headers = $AuthContext.Headers; ErrorAction = "Stop" }
        if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
            $p.SkipCertificateCheck = $true
        }
        if ($null -ne $Body) {
            $p.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
            $p.ContentType = "application/json"
        }
        try {
            return Invoke-RestMethod @p
        }
        catch {
            $detail = [string]$_.Exception.Message
            if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $detail = [string]$_.ErrorDetails.Message }
            if ($RetryOnUnauthorized -and $attempt -eq 0 -and ($detail -match '401|Unauthorized')) {
                Write-Host "  Token yenileniyor (401)..." -ForegroundColor Yellow
                Update-DgMigrationToken -AuthContext $AuthContext
                continue
            }
            throw
        }
    }
}

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
