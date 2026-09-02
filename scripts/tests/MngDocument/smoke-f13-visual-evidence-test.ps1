# Smoke: F1-3 görsel kanıt — SVG yükle, kind=diagram, sürüm, replace (Odak test)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$svgName = "F13-kanit-$stamp.svg"

$svgV1 = @'
<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><rect width="16" height="16" fill="#1976d2"/></svg>
'@
$svgV2 = @'
<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><rect width="16" height="16" fill="#c62828"/></svg>
'@
$drawioXml = @'
<mxfile host="app.diagrams.net"><diagram name="Page-1" id="f13"><mxGraphModel><root><mxCell id="0"/><mxCell id="1" parent="0"/><mxCell id="2" value="F13" style="rounded=1;whiteSpace=wrap;html=1;" vertex="1" parent="1"><mxGeometry x="40" y="40" width="80" height="40" as="geometry"/></mxCell></root></mxGraphModel></diagram></mxfile>
'@

function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
    throw "Token alinamadi."
}

function Invoke-Docs {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null
    )
    $uri = "$Gateway/documents/api/v1$Path"
    $params = @{
        Uri        = $uri
        Method     = $Method
        Headers    = $script:Headers
        TimeoutSec = 120
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    $result = Invoke-RestMethod @params -SkipCertificateCheck
    # Prevent PowerShell from unwrapping JSON arrays on `return`.
    return ,$result
}

function Get-NodeId([object]$node) {
    if ($null -eq $node) { return $null }
    if ($node -is [System.Array]) { $node = $node[0] }
    $id = $node.id
    if ($id -is [System.Array]) { return [string]$id[0] }
    return [string]$id
}

function Find-AnyFolder {
    $roots = @(Invoke-Docs -Path "/resources/tree/roots")
    $docsRoot = $roots | Where-Object { $_.name -match 'D.k.man' } | Select-Object -First 1
    if ($docsRoot) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($(Get-NodeId $docsRoot)))")
        $folder = @($children | Select-Object -First 1)[0]
        if ($folder) { return $folder }
    }
    foreach ($root in $roots) {
        $children = @(Invoke-Docs -Path "/resources/tree/children?parentId=$([uri]::EscapeDataString($(Get-NodeId $root)))")
        $folder = @($children | Select-Object -First 1)[0]
        if ($folder) { return $folder }
    }
    return @($roots | Select-Object -First 1)[0]
}

function To-Base64([string]$text) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text))
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host "== Smoke: F1-3 visual evidence ==" -ForegroundColor Cyan

$folder = Find-AnyFolder
if (-not $folder) { throw "Klasor bulunamadi." }
$folderId = Get-NodeId $folder
Write-Host "Folder: $($folder.name) ($folderId)"

$created = Invoke-Docs -Method POST -Path "/resources/file" -Body @{
    parentId         = $folderId
    name             = $svgName
    originalFileName = $svgName
    mimeType         = "image/svg+xml"
    extension        = "svg"
    content          = (To-Base64 $svgV1)
}
$svgId = Get-NodeId $created
if (-not $svgId) { throw "SVG create id yok." }
if ($created.kind -ne "diagram") { throw "SVG kind beklenen diagram, gelen: $($created.kind)" }
Write-Host "OK create SVG kind=$($created.kind) id=$svgId v$($created.currentVersionNumber)"

$versions = @(Invoke-Docs -Path "/resources/$svgId/versions")
if ($versions.Count -lt 1) { throw "SVG surum gecmisi bos." }
Write-Host "OK versions after create: $($versions.Count)"

$drawioName = "F13-diyagram-$stamp.drawio"
$drawio = Invoke-Docs -Method POST -Path "/resources/file" -Body @{
    parentId         = $folderId
    name             = $drawioName
    originalFileName = $drawioName
    mimeType         = "application/vnd.jgraph.mxfile"
    extension        = "drawio"
    content          = (To-Base64 $drawioXml)
}
$drawioId = Get-NodeId $drawio
if ($drawio.kind -ne "diagram") { throw "drawio kind beklenen diagram, gelen: $($drawio.kind)" }
Write-Host "OK create drawio kind=$($drawio.kind) id=$drawioId"

$replaced = Invoke-Docs -Method PUT -Path "/resources/$svgId/file-content" -Body @{
    originalFileName = $svgName
    mimeType         = "image/svg+xml"
    extension        = "svg"
    changeNote       = "f13-replace"
    content          = (To-Base64 $svgV2)
}
if (($replaced.currentVersionNumber | ForEach-Object { $_ }) -lt 2) {
    throw "Replace sonrasi surum 2+ beklenirdi: $($replaced.currentVersionNumber)"
}
Write-Host "OK replace SVG v$($replaced.currentVersionNumber)"

$versions2 = @()
for ($i = 0; $i -lt 10; $i++) {
    $versions2 = @($(Invoke-Docs -Path "/resources/$svgId/versions"))
    if ($versions2.Count -ge 2) { break }
    Start-Sleep -Milliseconds 200
}
if ($versions2.Count -lt 2) { throw "Replace sonrasi en az 2 surum beklenirdi: $($versions2.Count)" }
Write-Host "OK versions after replace: $($versions2.Count)"

if (-not $KeepArtifacts) {
    Invoke-Docs -Method DELETE -Path "/resources/$svgId" | Out-Null
    Invoke-Docs -Method DELETE -Path "/resources/$drawioId" | Out-Null
    Write-Host "OK cleanup"
}

Write-Host "== F1-3 smoke OK ==" -ForegroundColor Green
