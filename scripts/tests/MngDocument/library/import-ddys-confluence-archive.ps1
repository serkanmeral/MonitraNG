# Mirror a local folder tree into a project Document Intelligence library.
# Folders: POST /documents/api/v1/resources/folder
# Files:   POST /documents/api/v1/resources/file  (server sets origin=upload)
# The caller token is forwarded to DataGateway, so __history.userEmail is that user.
#
# Local:  -GatewayBaseUrl https://localhost:5040
# Cafe:   -GatewayBaseUrl http://192.168.1.42:5040
# Resume: names that already exist in the same folder are skipped.
#
# Before the first run on a host, DataGateway must accept the archive:
#   FileStorage Validation MaxFileSize >= 104857600 (100 MB)
#   AllowedExtensions must include the product defaults plus .mpp .msg .xml .wsdl .xsd .xls .doc .html .xlsm .json .bin
#   Gateway, Document and DataGateway Kestrel MaxRequestBodySize >= 104857600
#   (two Figma .rar files are ~54 MB; base64 JSON is ~72 MB)

param(
    [string]$GatewayBaseUrl = "https://localhost:5040",
    [string]$Domain = "proline",
    [string]$Username = "kivanc.pak",
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$ProjectCode = "DDYS",
    [string]$ArchiveName = "",
    [string]$SourceRoot = "C:\Users\serkan.meral\Desktop\Turkcar\CONFLUENCE YEDEKLEME (2026.08.26)\Dijital Demiryolu Yönetim Sistemi",
    [string]$LogFile = ""
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if ([string]::IsNullOrWhiteSpace($ArchiveName)) {
    $ArchiveName = "Ar$([char]0x015F)iv"
}
if ([string]::IsNullOrWhiteSpace($LogFile)) {
    $LogFile = Join-Path $env:TEMP "ddys-confluence-archive.log"
}

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")
$docs = "$GatewayBaseUrl/documents/api/v1/resources"
$script:uploaded = 0
$script:skipped = 0
$script:foldersCreated = 0
$script:failed = New-Object System.Collections.Generic.List[string]

function Write-Log([string]$Message) {
    $line = "{0:HH:mm:ss} {1}" -f (Get-Date), $Message
    Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
    Write-Host $line
}

function ConvertTo-JsonString([string]$Value) {
    if ($null -eq $Value) { return '""' }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $Value.ToCharArray()) {
        $code = [int]$ch
        switch ($code) {
            8 { [void]$sb.Append('\b'); break }
            9 { [void]$sb.Append('\t'); break }
            10 { [void]$sb.Append('\n'); break }
            12 { [void]$sb.Append('\f'); break }
            13 { [void]$sb.Append('\r'); break }
            34 { [void]$sb.Append('\"'); break }
            92 { [void]$sb.Append('\\'); break }
            default {
                if ($code -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', $code) }
                else { [void]$sb.Append($ch) }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Url,
        [string]$BodyFile = "",
        [int]$MaxTime = 120
    )
    $out = [System.IO.Path]::GetTempFileName()
    $args = @("-s", "-k", "-X", $Method, "-w", "%{http_code}", "-o", $out, "-H", "Authorization: Bearer $script:token", "--max-time", "$MaxTime")
    if ($BodyFile) {
        $args += @("-H", "Content-Type: application/json; charset=utf-8", "--data-binary", "@$BodyFile")
    }
    $code = & curl.exe @args $Url
    $code = "$code".Trim()
    $text = ""
    if (Test-Path -LiteralPath $out) {
        $text = [System.IO.File]::ReadAllText($out)
        Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue
    }
    return @{ Code = $code; Body = $text }
}

function Get-Resource([string]$Id) {
    $res = Invoke-Api -Method GET -Url "$docs/$Id"
    if ($res.Code -ne "200") {
        throw "kaynak $Id HTTP $($res.Code)"
    }
    return ($res.Body | ConvertFrom-Json)
}

function Get-NameMap([string]$ParentId) {
    $url = "$docs/children?parentId=$([uri]::EscapeDataString($ParentId))"
    $res = Invoke-Api -Method GET -Url $url
    if ($res.Code -ne "200") {
        throw "children $ParentId HTTP $($res.Code) $($res.Body.Substring(0, [Math]::Min(300, $res.Body.Length)))"
    }
    $parsed = $res.Body | ConvertFrom-Json
    $map = @{}
    foreach ($item in @($parsed.items)) {
        if ($item -and $item.name) { $map[$item.name] = $item }
    }
    return $map
}

function New-Folder([string]$ParentId, [string]$Name) {
    $bodyPath = [System.IO.Path]::GetTempFileName()
    $json = '{"parentId":' + (ConvertTo-JsonString $ParentId) + ',"name":' + (ConvertTo-JsonString $Name) + '}'
    [System.IO.File]::WriteAllText($bodyPath, $json, [System.Text.UTF8Encoding]::new($false))
    $res = Invoke-Api -Method POST -Url "$docs/folder" -BodyFile $bodyPath
    Remove-Item -LiteralPath $bodyPath -Force -ErrorAction SilentlyContinue
    if ($res.Code -ne "201" -and $res.Code -ne "200") {
        throw "folder '$Name' HTTP $($res.Code) $($res.Body.Substring(0, [Math]::Min(400, $res.Body.Length)))"
    }
    $created = $res.Body | ConvertFrom-Json
    $script:foldersCreated++
    return $created
}

function Send-File([string]$ParentId, [System.IO.FileInfo]$File) {
    $ext = $File.Extension.TrimStart('.').ToLowerInvariant()
    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    $b64 = [Convert]::ToBase64String($bytes)
    $json = '{"parentId":' + (ConvertTo-JsonString $ParentId) +
        ',"name":' + (ConvertTo-JsonString $File.Name) +
        ',"originalFileName":' + (ConvertTo-JsonString $File.Name) +
        ',"extension":' + (ConvertTo-JsonString $ext) +
        ',"size":' + $File.Length +
        ',"content":"' + $b64 + '"}'
    $bodyPath = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($bodyPath, $json, [System.Text.UTF8Encoding]::new($false))
    $max = 180
    if ($File.Length -gt 20MB) { $max = 900 }
    $res = Invoke-Api -Method POST -Url "$docs/file" -BodyFile $bodyPath -MaxTime $max
    Remove-Item -LiteralPath $bodyPath -Force -ErrorAction SilentlyContinue
    $bytes = $null
    $b64 = $null
    $json = $null
    if ($res.Code -ne "201" -and $res.Code -ne "200") {
        $snippet = $res.Body
        if ($snippet.Length -gt 240) { $snippet = $snippet.Substring(0, 240) }
        throw "HTTP $($res.Code) $snippet"
    }
    return ($res.Body | ConvertFrom-Json)
}

function Import-Tree([string]$DiskPath, [string]$ParentId) {
    $map = Get-NameMap $ParentId
    foreach ($dir in @(Get-ChildItem -LiteralPath $DiskPath -Directory | Sort-Object Name)) {
        $existing = $map[$dir.Name]
        if ($existing) {
            $folderId = $existing.id
            Write-Log "klasor var  $($dir.FullName.Substring($SourceRoot.Length))"
        }
        else {
            $created = New-Folder -ParentId $ParentId -Name $dir.Name
            $folderId = $created.id
            $map[$dir.Name] = $created
            Write-Log "klasor +    $($dir.Name)  by=$($created.createdBy)"
        }
        Import-Tree -DiskPath $dir.FullName -ParentId $folderId
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $DiskPath -File | Sort-Object Name)) {
        if ($file.Name -in @("desktop.ini", "Thumbs.db", ".DS_Store")) { continue }
        $rel = $file.FullName.Substring($SourceRoot.Length).TrimStart('\')
        if ($map.ContainsKey($file.Name)) {
            $script:skipped++
            Write-Log "atlandi    $rel"
            continue
        }
        try {
            $created = Send-File -ParentId $ParentId -File $file
            $map[$file.Name] = $created
            $script:uploaded++
            Write-Log ("yuklendi   {0:N1} MB  {1}  origin={2}  by={3}" -f ($file.Length / 1MB), $rel, $created.origin, $created.createdBy)
        }
        catch {
            $script:failed.Add($rel)
            Write-Log "HATA       $rel  $($_.Exception.Message)"
        }
    }
}

if (-not (Test-Path -LiteralPath $SourceRoot)) {
    throw "Kaynak klasor yok: $SourceRoot"
}

Write-Log "basladi  $Username  $GatewayBaseUrl  proje=$ProjectCode  arsiv=$ArchiveName"
$loginBody = @{ username = $Username; password = $Password; domain = $Domain } | ConvertTo-Json -Compress
$loginFile = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($loginFile, $loginBody, [System.Text.UTF8Encoding]::new($false))
$loginOut = [System.IO.Path]::GetTempFileName()
& curl.exe -s -k -X POST -H "Content-Type: application/json" --data-binary "@$loginFile" -o $loginOut "$GatewayBaseUrl/keeper/api/auth/token" | Out-Null
Remove-Item -LiteralPath $loginFile -Force
$loginRaw = [System.IO.File]::ReadAllText($loginOut).Trim().TrimStart([char]0xFEFF)
Remove-Item -LiteralPath $loginOut -Force
$login = $loginRaw | ConvertFrom-Json
if (-not $login.accessToken) { throw "Token alinamadi" }
$script:token = $login.accessToken
$login = $null
$loginRaw = $null

$projOut = Invoke-Api -Method GET -Url "$GatewayBaseUrl/operations/api/v1/projects"
if ($projOut.Code -ne "200") { throw "projeler HTTP $($projOut.Code)" }
$projects = @($projOut.Body | ConvertFrom-Json)
$project = $projects | Where-Object { $_.code -eq $ProjectCode } | Select-Object -First 1
if (-not $project) { throw "Proje yok: $ProjectCode" }
if ([string]::IsNullOrWhiteSpace($project.diFolderId)) { throw "Projede kutuphane klasoru yok: $ProjectCode" }
Write-Log "kutuphane $($project.diFolderId)"

$hubMap = Get-NameMap $project.diFolderId
$archive = $hubMap[$ArchiveName]
if ($archive) {
    $archiveId = $archive.id
    Write-Log "arsiv var  $archiveId  by=$($archive.createdBy)"
}
else {
    $archive = New-Folder -ParentId $project.diFolderId -Name $ArchiveName
    $archiveId = $archive.id
    if ([string]::IsNullOrWhiteSpace($archive.createdBy)) {
        $archive = Get-Resource $archiveId
    }
    Write-Log "arsiv +    $archiveId  by=$($archive.createdBy)"
    $actor = [regex]::Escape(($Username.Split('@')[0]))
    if ([string]::IsNullOrWhiteSpace($archive.createdBy) -or ($archive.createdBy -notmatch $actor)) {
        throw "Arsiv klasoru beklenen kullaniciya yazilmadi: $($archive.createdBy)"
    }
}

Import-Tree -DiskPath $SourceRoot -ParentId $archiveId

Write-Log ("BITTI  klasor={0}  yuklenen={1}  atlanan={2}  hata={3}" -f $script:foldersCreated, $script:uploaded, $script:skipped, $script:failed.Count)
if ($script:failed.Count -gt 0) {
    foreach ($item in $script:failed) { Write-Log "KALAN  $item" }
}
Write-Log "IMPORT_DONE"
if ($script:failed.Count -gt 0) { exit 2 }
exit 0
