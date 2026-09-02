# Smoke: F4-2 paket sokmede bos DI klasor silme (Odak test)
# UI ayni kurallari uygular; bu script Documents API uzerinden sozlesmeyi dogrular.
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$code = "F42-$stamp"

function Get-Token {
    $fresh = & $loadToken -AutoRefresh
    if ($fresh) { return $fresh.Trim() }
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    throw "Token alinamadi."
}

function Assert-True($cond, [string]$msg) {
    if (-not $cond) { throw "FAIL: $msg" }
    Write-Host "  OK $msg" -ForegroundColor Green
}

function Invoke-Doc {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null,
        [int[]]$ExpectStatus = @(200, 201, 204)
    )
    $uri = "$Gateway/documents/api/v1/resources$Path"
    $status = 0
    $params = @{
        Uri                  = $uri
        Method               = $Method
        Headers              = $script:Headers
        TimeoutSec           = 60
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
    }
    $result = Invoke-RestMethod @params
    $script:LastStatus = [int]$status
    if ($ExpectStatus -notcontains $script:LastStatus) {
        $err = $null
        if ($result) {
            try { $err = $result | ConvertTo-Json -Compress -Depth 6 } catch { $err = [string]$result }
        }
        throw "HTTP $script:LastStatus $Method $Path : $err"
    }
    return , $result
}

function Get-Items($response) {
    if ($null -eq $response) { return @() }
    if ($null -ne $response.items) { return @($response.items) }
    if ($response -is [Array]) { return @($response) }
    return @($response)
}

function Normalize-FolderName([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name)) { return "" }
    $n = $Name.Trim().ToLowerInvariant()
    $n = $n.Replace([char]0x00F6, "o").Replace([char]0x00D6, "o")
    $n = $n.Replace([char]0x00FC, "u").Replace([char]0x00DC, "u")
    $n = $n.Replace([char]0x00E7, "c").Replace([char]0x00C7, "c")
    $n = $n.Replace([char]0x011F, "g").Replace([char]0x011E, "g")
    $n = $n.Replace([char]0x0131, "i").Replace([char]0x0130, "i")
    $n = $n.Replace([char]0x015F, "s").Replace([char]0x015E, "s")
    return $n
}

function Find-Folder {
    param([string]$Name, [string]$ParentId = $null)
    if ($ParentId) {
        $siblings = Get-Items (Invoke-Doc -Path "/children?parentId=$ParentId")
    }
    else {
        $siblings = Get-Items (Invoke-Doc -Path "/children")
    }
    $want = Normalize-FolderName $Name
    return $siblings | Where-Object {
        $isFolder = [string]$_.type -eq "folder" -or [string]::IsNullOrWhiteSpace([string]$_.type)
        $isFolder -and (Normalize-FolderName ([string]$_.name)) -eq $want
    } | Select-Object -First 1
}

function Ensure-Folder {
    param([string]$Name, [string]$ParentId = $null)
    $existing = Find-Folder -Name $Name -ParentId $ParentId
    if ($existing) { return [string]$existing.id }
    $body = @{ name = $Name }
    if ($ParentId) { $body.parentId = $ParentId }
    $created = @(Invoke-Doc -Method POST -Path "/folder" -Body $body)[0]
    return [string]$created.id
}

function Test-FolderEmpty([string]$FolderId) {
    $listing = @(Invoke-Doc -Path "/children?parentId=$FolderId")[0]
    $items = Get-Items $listing
    $total = 0
    if ($listing.total) { $total = [int]$listing.total }
    return ($items.Count -eq 0 -and $total -le 0)
}

function Remove-EmptyPackFolders {
    param(
        [string]$HubId,
        [string[]]$Folders,
        [string[]]$KeepNames = @()
    )
    $removed = 0
    $kept = 0
    $keepSet = @{}
    foreach ($n in $KeepNames) { $keepSet[$n.ToLowerInvariant()] = $true }
    foreach ($name in $Folders) {
        if ($keepSet.ContainsKey($name.ToLowerInvariant())) {
            $kept++
            continue
        }
        $folder = Find-Folder -Name $name -ParentId $HubId
        if (-not $folder) { continue }
        $id = [string]$folder.id
        if (-not (Test-FolderEmpty $id)) {
            $kept++
            continue
        }
        Invoke-Doc -Method DELETE -Path "/$id" | Out-Null
        $removed++
    }
    return @{ Removed = $removed; Kept = $kept }
}

$token = Get-Token
$script:Headers = @{ Authorization = "Bearer $token" }
$script:LastStatus = 0
$hubId = $null
$keptFileId = $null

Write-Host "F4-2 smoke  code=$code  gateway=$Gateway" -ForegroundColor Cyan

try {
    $docs = $null
    foreach ($name in @("Dokumanlar", "Dökümanlar", "Documents")) {
        $docs = Find-Folder -Name $name
        if ($docs) { break }
    }
    Assert-True ($null -ne $docs -and $docs.id) "Dokumanlar koku bulundu ($($docs.name))"

    $projectsId = Ensure-Folder -Name "Projeler" -ParentId ([string]$docs.id)
    $hubId = Ensure-Folder -Name $code -ParentId $projectsId
    Assert-True ($hubId) "proje hub=$hubId"

    $emptyId = Ensure-Folder -Name "Diyagram" -ParentId $hubId
    $filledId = Ensure-Folder -Name "Plan" -ParentId $hubId
    $sharedId = Ensure-Folder -Name "Form" -ParentId $hubId
    Assert-True (Test-FolderEmpty $emptyId) "Diyagram bos"
    Assert-True (Test-FolderEmpty $sharedId) "Form bos (paylasilan)"

    $md = @(Invoke-Doc -Method POST -Path "/markdown" -Body @{
            parentId = $filledId
            title    = "F4-2 kanit"
            content  = "dolu klasor silinmez"
            isDraft  = $false
        })[0]
    $keptFileId = [string]$md.id
    Assert-True ($keptFileId) "Plan icine markdown=$keptFileId"
    Assert-True (-not (Test-FolderEmpty $filledId)) "Plan dolu"

    $first = Remove-EmptyPackFolders -HubId $hubId -Folders @("Diyagram", "Plan", "Form") -KeepNames @("Form")
    Assert-True ($first.Removed -eq 1) "ilk tur removed=$($first.Removed)"
    Assert-True ($first.Kept -ge 2) "ilk tur kept=$($first.Kept)"
    Assert-True ($null -eq (Find-Folder -Name "Diyagram" -ParentId $hubId)) "bos Diyagram silindi"
    Assert-True ($null -ne (Find-Folder -Name "Plan" -ParentId $hubId)) "dolu Plan kaldi"
    Assert-True ($null -ne (Find-Folder -Name "Form" -ParentId $hubId)) "paylasilan bos Form kaldi"

    $second = Remove-EmptyPackFolders -HubId $hubId -Folders @("Form") -KeepNames @()
    Assert-True ($second.Removed -eq 1) "ikinci tur Form silindi"
    Assert-True ($null -eq (Find-Folder -Name "Form" -ParentId $hubId)) "Form artik yok"
    Assert-True ($null -ne (Find-Folder -Name "Plan" -ParentId $hubId)) "Plan hâlâ duruyor"

    if (-not $KeepArtifacts) {
        if ($keptFileId) {
            try { Invoke-Doc -Method DELETE -Path "/$keptFileId" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
        foreach ($name in @("Plan", "Diyagram", "Form")) {
            $row = Find-Folder -Name $name -ParentId $hubId
            if ($row) {
                try { Invoke-Doc -Method DELETE -Path "/$($row.id)?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
            }
        }
        try { Invoke-Doc -Method DELETE -Path "/$hubId?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        $hubId = $null
        Write-Host "  cleanup OK" -ForegroundColor Green
    }
    else {
        Write-Host "KeepArtifacts: hub birakildi $hubId" -ForegroundColor Yellow
    }

    Write-Host "F4-2 smoke OK" -ForegroundColor Green
}
catch {
    Write-Host "F4-2 smoke FAIL: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $KeepArtifacts) {
        if ($keptFileId) {
            try { Invoke-Doc -Method DELETE -Path "/$keptFileId" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
        if ($hubId) {
            try { Invoke-Doc -Method DELETE -Path "/$hubId?force=true" -ExpectStatus @(200, 204, 404) | Out-Null } catch { }
        }
    }
    throw
}
