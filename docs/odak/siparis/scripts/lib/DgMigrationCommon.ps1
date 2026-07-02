# DG migration helpers — filter API unreliable; client-side legacy id map

function Sanitize-LegacyText {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    $text = [string]$Value
    if ([string]::IsNullOrEmpty($text)) { return $text }
    # Fix mojibake only when text looks like UTF-8 bytes misread as ISO-8859-1 (e.g. "Ã§", "Ä±", "ÅŸ")
    if ($text -match 'Ã.|Ä.|Å.|â€|ï¿½') {
        $latin = [System.Text.Encoding]::GetEncoding("ISO-8859-1")
        $utf8 = [System.Text.Encoding]::UTF8
        try {
            $bytes = $latin.GetBytes($text)
            $fixed = $utf8.GetString($bytes)
            if (-not [string]::IsNullOrWhiteSpace($fixed)) { $text = $fixed }
        }
        catch { }
    }
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

function Test-SuspiciousLegacyText {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    if ($Text -match '[?�]|Ã.|Ä.|Å.|â€|ï¿½') { return $true }
    return $false
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
    param(
        [hashtable]$AuthContext,
        [switch]$ForceRefresh
    )
    if ($ForceRefresh) {
        $token = & $AuthContext.TokenScript -AutoRefresh
        if ([string]::IsNullOrEmpty($token)) {
            Write-Host "  ForceRefresh basarisiz; dosyadan okunuyor..." -ForegroundColor Yellow
            $token = & $AuthContext.TokenScript -AutoRefresh:$false
        }
    }
    else {
        $token = & $AuthContext.TokenScript -AutoRefresh:$false
        if ([string]::IsNullOrEmpty($token)) {
            $token = & $AuthContext.TokenScript -AutoRefresh
        }
    }
    if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }
    $AuthContext.Headers.Authorization = "Bearer $token"
}

function ConvertTo-Utf8JsonBody {
    param(
        [object]$Object,
        [int]$Depth = 20
    )
    if ($Object -is [string]) {
        return [string]$Object
    }
    return ($Object | ConvertTo-Json -Depth $Depth -Compress)
}

function ConvertTo-Utf8JsonBytes {
    param(
        [object]$Object,
        [int]$Depth = 20
    )
    $json = ConvertTo-Utf8JsonBody -Object $Object -Depth $Depth
    return [System.Text.Encoding]::UTF8.GetBytes($json)
}

# Invoke-RestMethod string Body uses system default encoding on Windows and corrupts Turkish chars.
function Set-RestMethodUtf8JsonBody {
    param(
        [hashtable]$Parameters,
        [object]$Body,
        [int]$Depth = 20
    )
    if ($Body -is [byte[]]) {
        $Parameters.Body = $Body
    }
    elseif ($Body -is [string]) {
        $Parameters.Body = [System.Text.Encoding]::UTF8.GetBytes([string]$Body)
    }
    else {
        $Parameters.Body = ConvertTo-Utf8JsonBytes -Object $Body -Depth $Depth
    }
    $Parameters.ContentType = "application/json; charset=utf-8"
}

function Invoke-DgRestMethod {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [int]$JsonDepth = 20,
        [switch]$SkipCertificateCheck
    )
    $handler = [System.Net.Http.HttpClientHandler]::new()
    if ($SkipCertificateCheck -or $Uri.StartsWith("https://")) {
        $handler.ServerCertificateCustomValidationCallback = { param($sender, $cert, $chain, $errors) $true }
    }
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $httpMethod = [System.Net.Http.HttpMethod]::new($Method.ToUpperInvariant())
        $request = [System.Net.Http.HttpRequestMessage]::new($httpMethod, $Uri)
        foreach ($key in $Headers.Keys) {
            $val = [string]$Headers[$key]
            if ($key -eq "Authorization") {
                if ($val -match "^Bearer\s+(.+)$") {
                    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $matches[1])
                }
            }
            elseif ($key -ne "Content-Type") {
                [void]$request.Headers.TryAddWithoutValidation($key, $val)
            }
        }
        if ($null -ne $Body) {
            $json = if ($Body -is [string]) { [string]$Body } else { ConvertTo-Utf8JsonBody -Object $Body -Depth $JsonDepth }
            $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, "application/json")
        }
        $response = $client.SendAsync($request).ConfigureAwait($false).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().ConfigureAwait($false).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "HTTP $([int]$response.StatusCode): $content"
        }
        if ([string]::IsNullOrWhiteSpace($content)) { return $null }
        return ($content | ConvertFrom-Json)
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Write-Utf8JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Object,
        [int]$Depth = 8
    )
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $json = $Object | ConvertTo-Json -Depth $Depth
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
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
        $skipCert = $Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")
        try {
            return Invoke-DgRestMethod -Method $Method -Uri $Uri -Headers $AuthContext.Headers -Body $Body -SkipCertificateCheck:$skipCert
        }
        catch {
            $detail = [string]$_.Exception.Message
            if ($RetryOnUnauthorized -and $attempt -eq 0 -and ($detail -match '401|Unauthorized')) {
                Write-Host "  Token yenileniyor (401)..." -ForegroundColor Yellow
                Update-DgMigrationToken -AuthContext $AuthContext -ForceRefresh
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
    Write-Host "  Map yukleniyor: $Dataset ..." -ForegroundColor Gray
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
        Write-Host "    $Dataset skip=$skip +$($items.Count) (toplam $($map.Count))" -ForegroundColor DarkGray
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
