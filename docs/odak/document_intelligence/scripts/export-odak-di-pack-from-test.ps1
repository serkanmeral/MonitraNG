# Export Odak TEST Document Intelligence assets (templates + letterheads + cover pages).
# Pack only — no local import / Create.
#
#   .\export-odak-di-pack-from-test.ps1
#   .\export-odak-di-pack-from-test.ps1 -OutputRoot "C:\Users\monitra\Dev\exports\odak-di-pack-20260711"

param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$WopiHost = "http://192.168.20.20:5095",
    [string]$Domain = "odak",
    [string]$Token = "",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd")
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $preferred = "C:\Users\monitra\Dev\exports\odak-di-pack-$stamp"
    $fallback = Join-Path $repoRoot "docs\odak\exports\odak-di-pack-$stamp"
    $OutputRoot = if (Test-Path (Split-Path $preferred -Parent)) { $preferred } else { $fallback }
}

$docsBase = "$Gateway/documents/api/v1"
$loadToken = Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"
$tokenFile = Join-Path $env:TEMP "operationcore_dg_token.txt"

$token = $Token
if ([string]::IsNullOrWhiteSpace($token) -and (Test-Path $tokenFile)) {
    $token = (Get-Content $tokenFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($token) -and (Test-Path $loadToken)) {
    $token = (& $loadToken)
    if ($token) { $token = $token.Trim() }
}
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token yok. get-operationcore-token.ps1 calistirin." }

$headers = @{
    Authorization     = "Bearer $token"
    "X-Domain-Name"   = $Domain
    Accept            = "application/json"
}

$failures = [System.Collections.Generic.List[object]]::new()
$counts = @{
    categories           = 0
    templates            = 0
    templatesWithBinary  = 0
    letterheads          = 0
    letterheadsWithBinary = 0
    coverPages           = 0
    coverPagesWithBinary = 0
}

function Add-Failure([string]$Kind, [string]$Id, [string]$Code, [string]$Error) {
    $script:failures.Add([ordered]@{
        kind  = $Kind
        id    = $Id
        code  = $Code
        error = $Error
    }) | Out-Null
    Write-Host "  FAIL [$Kind] $Code ($Id): $Error" -ForegroundColor Yellow
}

function Get-Prop($Obj, [string]$Name) {
    if ($null -eq $Obj) { return $null }
    $p = $Obj.PSObject.Properties[$Name]
    if ($p) { return $p.Value }
    $alt = $Obj.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($alt) { return $alt.Value }
    return $null
}

function ConvertTo-SafeFolderName([string]$Value, [string]$Fallback) {
    $raw = if ([string]::IsNullOrWhiteSpace($Value)) { $Fallback } else { $Value.Trim() }
    $safe = ($raw -replace '[<>:"/\\|?*]', '_').Trim()
    if ([string]::IsNullOrWhiteSpace($safe)) { $safe = $Fallback }
    return $safe
}

function Resolve-SourceExtension([string]$FileName, [string]$DefaultExt = ".docx") {
    if ([string]::IsNullOrWhiteSpace($FileName)) { return $DefaultExt }
    $ext = [IO.Path]::GetExtension($FileName)
    if ([string]::IsNullOrWhiteSpace($ext)) { return $DefaultExt }
    return $ext.ToLowerInvariant()
}

function Invoke-DiJson {
    param([string]$Uri, [string]$Method = "GET")
    return Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers -TimeoutSec 180
}

function Get-WopiBytes {
    param(
        [string]$FileId,
        [string]$AccessToken,
        [string]$WopiSrc = ""
    )
    $url = $null
    if (-not [string]::IsNullOrWhiteSpace($WopiSrc)) {
        # wopiSrc often looks like .../wopi/files/{id}?access_token=...
        if ($WopiSrc -match '/contents') {
            $url = $WopiSrc
        }
        elseif ($WopiSrc -match '\?') {
            $base = ($WopiSrc -split '\?')[0]
            $qs = ($WopiSrc -split '\?', 2)[1]
            $url = "$base/contents?$qs"
        }
    }
    if ([string]::IsNullOrWhiteSpace($url)) {
        $url = "$WopiHost/wopi/files/$([uri]::EscapeDataString($FileId))/contents?access_token=$([uri]::EscapeDataString($AccessToken))"
    }
    $resp = Invoke-WebRequest -Uri $url -Method GET -UseBasicParsing -TimeoutSec 300
    $bytes = [byte[]]$resp.Content
    if ($null -eq $bytes -or $bytes.Length -le 0) {
        throw "WOPI contents empty (0 bytes)"
    }
    return $bytes
}

function Get-DgFileBytes {
    param([string]$StoragePath)
    if ([string]::IsNullOrWhiteSpace($StoragePath)) { return $null }
    $uri = "$Gateway/data/api/v1/files/download?filePath=$([uri]::EscapeDataString($StoragePath))"
    $resp = Invoke-WebRequest -Uri $uri -Method GET -Headers $headers -UseBasicParsing -TimeoutSec 300
    $bytes = [byte[]]$resp.Content
    if ($null -eq $bytes -or $bytes.Length -le 0) {
        throw "DG download empty for path=$StoragePath"
    }
    return $bytes
}

function Save-Json($Obj, [string]$Path) {
    $dir = Split-Path $Path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    ($Obj | ConvertTo-Json -Depth 100) | Set-Content -Path $Path -Encoding UTF8
}

function Get-AllCategoryNodes {
    param([object[]]$Nodes)
    $all = @()
    foreach ($n in $Nodes) {
        $all += $n
        $children = Get-Prop $n "children"
        if ($children) { $all += Get-AllCategoryNodes -Nodes @($children) }
    }
    return $all
}

Write-Host "Output: $OutputRoot" -ForegroundColor Cyan
Write-Host "Gateway: $Gateway | WOPI: $WopiHost | Domain: $Domain" -ForegroundColor Cyan

foreach ($sub in @("categories", "templates", "letterheads", "cover-pages")) {
    $p = Join-Path $OutputRoot $sub
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p -Force | Out-Null }
}

# --- Categories ---
Write-Host "`n=== Categories ===" -ForegroundColor Cyan
$tree = Invoke-DiJson "$docsBase/template-categories/tree"
Save-Json $tree (Join-Path $OutputRoot "categories\tree.json")
$flatCats = @(Get-AllCategoryNodes -Nodes @($tree))
$counts.categories = $flatCats.Count
Write-Host "  categories (flat nodes): $($counts.categories)" -ForegroundColor Green

# --- Templates ---
Write-Host "`n=== Templates ===" -ForegroundColor Cyan
$templateIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

# List without categoryId (all)
try {
    $allList = Invoke-DiJson "$docsBase/templates"
    $items = @(Get-Prop $allList "items")
    if (-not $items -or $items.Count -eq 0) { $items = @($allList) }
    foreach ($t in $items) {
        $tid = [string](Get-Prop $t "id")
        if ($tid) { [void]$templateIds.Add($tid) }
    }
    Write-Host "  list(all): $($templateIds.Count) ids" -ForegroundColor DarkGray
}
catch {
    Write-Host "  list(all) failed: $($_.Exception.Message) — falling back to per-category" -ForegroundColor Yellow
}

foreach ($cat in $flatCats) {
    $cid = [string](Get-Prop $cat "id")
    if ([string]::IsNullOrWhiteSpace($cid)) { continue }
    try {
        $list = Invoke-DiJson "$docsBase/templates?categoryId=$([uri]::EscapeDataString($cid))"
        $items = @(Get-Prop $list "items")
        if (-not $items) { $items = @() }
        foreach ($t in $items) {
            $tid = [string](Get-Prop $t "id")
            if ($tid) { [void]$templateIds.Add($tid) }
        }
    }
    catch {
        Write-Host "  WARN templates?categoryId=$cid : $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "  unique template ids: $($templateIds.Count)" -ForegroundColor Green
$counts.templates = $templateIds.Count

foreach ($tid in ($templateIds | Sort-Object)) {
    $code = $tid
    try {
        $meta = Invoke-DiJson "$docsBase/templates/$([uri]::EscapeDataString($tid))"
        $code = [string](Get-Prop $meta "code")
        if ([string]::IsNullOrWhiteSpace($code)) { $code = $tid }
        $folderName = ConvertTo-SafeFolderName $code $tid
        $dir = Join-Path $OutputRoot "templates\$folderName"
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Save-Json $meta (Join-Path $dir "meta.json")

        $sourceFileName = [string](Get-Prop $meta "sourceFileName")
        $ext = Resolve-SourceExtension $sourceFileName ".docx"
        $binPath = Join-Path $dir ("source" + $ext)
        $bytes = $null
        $errParts = [System.Collections.Generic.List[string]]::new()

        try {
            $session = Invoke-DiJson "$docsBase/templates/$([uri]::EscapeDataString($tid))/editor-session"
            $accessToken = [string](Get-Prop $session "accessToken")
            $wopiSrc = [string](Get-Prop $session "wopiSrc")
            if ([string]::IsNullOrWhiteSpace($accessToken)) { throw "editor-session accessToken empty" }
            $bytes = Get-WopiBytes -FileId $tid -AccessToken $accessToken -WopiSrc $wopiSrc
        }
        catch {
            $errParts.Add("WOPI/editor-session: $($_.Exception.Message)")
        }

        if ($null -eq $bytes) {
            try {
                $storage = [string](Get-Prop $meta "sourceStoragePath")
                if ([string]::IsNullOrWhiteSpace($storage)) { throw "sourceStoragePath empty" }
                $bytes = Get-DgFileBytes -StoragePath $storage
            }
            catch {
                $errParts.Add("DG: $($_.Exception.Message)")
            }
        }

        if ($null -ne $bytes -and $bytes.Length -gt 0) {
            [IO.File]::WriteAllBytes($binPath, $bytes)
            $counts.templatesWithBinary++
            Write-Host "  OK template $code ($($bytes.Length) bytes)" -ForegroundColor Green
        }
        else {
            Add-Failure "template" $tid $code (($errParts -join " | "))
        }
    }
    catch {
        Add-Failure "template" $tid $code $_.Exception.Message
    }
}

# --- Letterheads ---
Write-Host "`n=== Letterheads ===" -ForegroundColor Cyan
$lhList = Invoke-DiJson "$docsBase/letterheads"
$lhItems = @(Get-Prop $lhList "items")
if (-not $lhItems) { $lhItems = @() }
$counts.letterheads = $lhItems.Count
Write-Host "  letterheads: $($counts.letterheads)" -ForegroundColor Green

foreach ($lh in $lhItems) {
    $lid = [string](Get-Prop $lh "id")
    $code = [string](Get-Prop $lh "code")
    if ([string]::IsNullOrWhiteSpace($code)) { $code = $lid }
    try {
        $meta = Invoke-DiJson "$docsBase/letterheads/$([uri]::EscapeDataString($lid))"
        $code = [string](Get-Prop $meta "code")
        if ([string]::IsNullOrWhiteSpace($code)) { $code = $lid }
        $folderName = ConvertTo-SafeFolderName $code $lid
        $dir = Join-Path $OutputRoot "letterheads\$folderName"
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Save-Json $meta (Join-Path $dir "meta.json")

        $designFileName = [string](Get-Prop $meta "designFileName")
        $ext = Resolve-SourceExtension $designFileName ".docx"
        $binPath = Join-Path $dir ("design" + $ext)
        $bytes = $null
        $errParts = [System.Collections.Generic.List[string]]::new()

        try {
            $session = Invoke-DiJson "$docsBase/letterheads/$([uri]::EscapeDataString($lid))/design-session"
            $accessToken = [string](Get-Prop $session "accessToken")
            $wopiSrc = [string](Get-Prop $session "wopiSrc")
            $wopiId = [string](Get-Prop $session "letterheadId")
            if ([string]::IsNullOrWhiteSpace($wopiId)) { $wopiId = $lid }
            if ([string]::IsNullOrWhiteSpace($accessToken)) { throw "design-session accessToken empty" }
            $bytes = Get-WopiBytes -FileId $wopiId -AccessToken $accessToken -WopiSrc $wopiSrc
        }
        catch {
            $errParts.Add("WOPI/design-session: $($_.Exception.Message)")
        }

        if ($null -eq $bytes) {
            try {
                $storage = [string](Get-Prop $meta "designStoragePath")
                if ([string]::IsNullOrWhiteSpace($storage)) { throw "designStoragePath empty" }
                $bytes = Get-DgFileBytes -StoragePath $storage
            }
            catch {
                $errParts.Add("DG: $($_.Exception.Message)")
            }
        }

        if ($null -ne $bytes -and $bytes.Length -gt 0) {
            [IO.File]::WriteAllBytes($binPath, $bytes)
            $counts.letterheadsWithBinary++
            Write-Host "  OK letterhead $code ($($bytes.Length) bytes)" -ForegroundColor Green
        }
        else {
            Add-Failure "letterhead" $lid $code (($errParts -join " | "))
        }
    }
    catch {
        Add-Failure "letterhead" $lid $code $_.Exception.Message
    }
}

# --- Cover pages ---
Write-Host "`n=== Cover pages ===" -ForegroundColor Cyan
$cpList = Invoke-DiJson "$docsBase/cover-pages"
$cpItems = @(Get-Prop $cpList "items")
if (-not $cpItems) { $cpItems = @() }
$counts.coverPages = $cpItems.Count
Write-Host "  cover-pages: $($counts.coverPages)" -ForegroundColor Green

foreach ($cp in $cpItems) {
    $cid = [string](Get-Prop $cp "id")
    $code = [string](Get-Prop $cp "code")
    if ([string]::IsNullOrWhiteSpace($code)) { $code = $cid }
    try {
        $meta = Invoke-DiJson "$docsBase/cover-pages/$([uri]::EscapeDataString($cid))"
        $code = [string](Get-Prop $meta "code")
        if ([string]::IsNullOrWhiteSpace($code)) { $code = $cid }
        $folderName = ConvertTo-SafeFolderName $code $cid
        $dir = Join-Path $OutputRoot "cover-pages\$folderName"
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Save-Json $meta (Join-Path $dir "meta.json")

        $designFileName = [string](Get-Prop $meta "designFileName")
        $ext = Resolve-SourceExtension $designFileName ".docx"
        $binPath = Join-Path $dir ("design" + $ext)
        $bytes = $null
        $errParts = [System.Collections.Generic.List[string]]::new()

        try {
            $session = Invoke-DiJson "$docsBase/cover-pages/$([uri]::EscapeDataString($cid))/design-session"
            $accessToken = [string](Get-Prop $session "accessToken")
            $wopiSrc = [string](Get-Prop $session "wopiSrc")
            $wopiId = [string](Get-Prop $session "coverPageId")
            if ([string]::IsNullOrWhiteSpace($wopiId)) { $wopiId = $cid }
            if ([string]::IsNullOrWhiteSpace($accessToken)) { throw "design-session accessToken empty" }
            $bytes = Get-WopiBytes -FileId $wopiId -AccessToken $accessToken -WopiSrc $wopiSrc
        }
        catch {
            $errParts.Add("WOPI/design-session: $($_.Exception.Message)")
        }

        if ($null -eq $bytes) {
            try {
                $storage = [string](Get-Prop $meta "designStoragePath")
                if ([string]::IsNullOrWhiteSpace($storage)) { throw "designStoragePath empty" }
                $bytes = Get-DgFileBytes -StoragePath $storage
            }
            catch {
                $errParts.Add("DG: $($_.Exception.Message)")
            }
        }

        if ($null -ne $bytes -and $bytes.Length -gt 0) {
            [IO.File]::WriteAllBytes($binPath, $bytes)
            $counts.coverPagesWithBinary++
            Write-Host "  OK cover-page $code ($($bytes.Length) bytes)" -ForegroundColor Green
        }
        else {
            Add-Failure "cover" $cid $code (($errParts -join " | "))
        }
    }
    catch {
        Add-Failure "cover" $cid $code $_.Exception.Message
    }
}

# --- Manifest ---
$manifest = [ordered]@{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    source     = [ordered]@{
        host    = "192.168.20.20"
        domain  = $Domain
        gateway = $Gateway
        wopi    = $WopiHost
    }
    counts     = [ordered]@{
        categories          = $counts.categories
        templates           = $counts.templates
        templatesWithBinary = $counts.templatesWithBinary
        letterheads         = $counts.letterheads
        letterheadsWithBinary = $counts.letterheadsWithBinary
        coverPages          = $counts.coverPages
        coverPagesWithBinary = $counts.coverPagesWithBinary
    }
    failures   = @($failures)
    notes      = "For local import: categories first, then letterheads/covers, then templates via from-reference + parameters PUT. Mongo dm_* ids will change; match by code."
}
Save-Json $manifest (Join-Path $OutputRoot "manifest.json")

Write-Host "`n=== DONE ===" -ForegroundColor Cyan
Write-Host "Path: $OutputRoot"
Write-Host ("Counts: categories={0} templates={1} (bin={2}) letterheads={3} (bin={4}) coverPages={5} (bin={6})" -f `
    $counts.categories, $counts.templates, $counts.templatesWithBinary, `
    $counts.letterheads, $counts.letterheadsWithBinary, `
    $counts.coverPages, $counts.coverPagesWithBinary)
Write-Host "Failures: $($failures.Count)"
foreach ($f in $failures) {
    Write-Host "  - [$($f.kind)] $($f.code): $($f.error)" -ForegroundColor Yellow
}

if ($counts.templatesWithBinary -lt 1) {
    Write-Host "WARNING: success criteria not met (need >=1 template binary)" -ForegroundColor Red
    exit 2
}
if (-not (Test-Path (Join-Path $OutputRoot "categories\tree.json"))) {
    Write-Host "WARNING: categories/tree.json missing" -ForegroundColor Red
    exit 2
}
exit 0
