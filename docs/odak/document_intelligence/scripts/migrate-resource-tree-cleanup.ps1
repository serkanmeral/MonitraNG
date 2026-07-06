# Document Intelligence — kok agaci temizlik ve tasima (P-B devam)
#
# Hedef convention:
#   Sayfalar/     — wiki / test sayfalari
#   Dokumanlar/   — resmi uretim ciktilari (CoC, Activity, ...)
#
# Varsayilan tasima plani (kok seviye):
#   Odak           -> Dokumanlar/Uretilen/Odak
#   Klasor 1       -> Sayfalar/Test/Klasor 1   (test markdownlari)
#   Ogreticiler    -> icerik: Sayfalar/MonitraNG/Ogreticiler/Arsiv/eski-kok/
#                     (bos kalirsa kok Ogreticiler klasoru silinir)
#
# On kosul: seed-resource-root-folders.ps1 calistirilmis olmali.
#
# Usage (repo kokunden):
#   .\docs\odak\document_intelligence\scripts\migrate-resource-tree-cleanup.ps1 -WhatIf
#   .\docs\odak\document_intelligence\scripts\migrate-resource-tree-cleanup.ps1
#   .\docs\odak\document_intelligence\scripts\migrate-resource-tree-cleanup.ps1 -BaseUrl "http://192.168.20.20:5040"
#   .\docs\odak\document_intelligence\scripts\migrate-resource-tree-cleanup.ps1 -SkipOdak -SkipTestFolders

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false,
    [switch]$SkipOdak = $false,
    [switch]$SkipTestFolders = $false,
    [switch]$SkipLegacyOgreticiler = $false,
    [switch]$RemoveEmptySourceFolders = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$isProd = $BaseUrl -match "192\.168\.20\.8"

$token = $env:DI_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = if ($isProd) {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript -AutoRefresh }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. get-operationcore-token.ps1 veya `$env:DI_TOKEN" -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $($token.Trim())" }
$apiBase = "$BaseUrl/documents/api/v1/resources"
$utf8 = [System.Text.Encoding]::UTF8

$RootSayfalar = "Sayfalar"
$RootDokumanlar = "Dökümanlar"
$ProtectedRootNames = @($RootSayfalar, $RootDokumanlar)

# Kokte kalmasi gereken / plan disi klasor adlari (bilgi amacli)
$KnownLegacyRootFolders = @("Odak", "Öğreticiler", "Klasör 1", "MonitraNG", "System")

function Invoke-DocApi {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Body
    )
    $uri = "$apiBase$Path"
    if ($Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $bytes = $utf8.GetBytes($json)
        return Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method -Body $bytes -ContentType "application/json; charset=utf-8"
    }
    return Invoke-RestMethod -Uri $uri -Headers $headers -Method $Method
}

function Get-Items($response) {
    if ($null -eq $response) { return @() }
    if ($null -ne $response.items) {
        return @($response.items | ForEach-Object { $_ })
    }
    if ($response -is [System.Array]) {
        return @($response | ForEach-Object { $_ })
    }
    return @($response)
}

function Get-RootChildren {
    return Get-Items (Invoke-DocApi -Method GET -Path "/children")
}

function Get-RootFolders {
    $folders = @()
    foreach ($item in Get-RootChildren) {
        if ($item.type -eq "folder") {
            $folders += $item
        }
    }
    return $folders
}

function Find-FolderUnderParent {
    param(
        [string]$Name,
        [string]$ParentId = $null
    )
    $siblings = if ($ParentId) {
        Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$ParentId")
    } else {
        Get-RootChildren
    }
    return $siblings | Where-Object { $_.type -eq "folder" -and $_.name -eq $Name } | Select-Object -First 1
}

function Ensure-Folder {
    param(
        [string]$Name,
        [string]$ParentId
    )
    $existing = Find-FolderUnderParent -Name $Name -ParentId $ParentId
    if ($existing) { return $existing.id }

    if ($WhatIf) {
        Write-Host "  WhatIf POST /folder '$Name' (parent=$ParentId)" -ForegroundColor Yellow
        return "<whatif-$Name>"
    }

    $body = @{ name = $Name; parentId = $ParentId }
    $created = Invoke-DocApi -Method POST -Path "/folder" -Body $body
    Write-Host "  OK klasor: $Name (id=$($created.id))" -ForegroundColor Green
    return $created.id
}

function Get-ResourceLabel($item) {
    if ($item.type -eq "markdown") { return $item.title ?? $item.name }
    return $item.name
}

function Move-Resource {
    param(
        [object]$Item,
        [string]$NewParentId,
        [string]$Reason
    )
    $label = Get-ResourceLabel $Item
    if ($WhatIf) {
        Write-Host "  WhatIf MOVE [$($Item.type)] '$label' -> parent=$NewParentId ($Reason)" -ForegroundColor Yellow
        return
    }
    Write-Host "  MOVE [$($Item.type)] '$label' ..." -ForegroundColor Yellow
    Invoke-DocApi -Method PUT -Path "/$($Item.id)/move" -Body @{ newParentId = $NewParentId } | Out-Null
    Write-Host "    OK" -ForegroundColor Green
}

function Move-FolderTree {
    param(
        [object]$Folder,
        [string]$NewParentId,
        [string]$Reason
    )
    $conflict = Find-FolderUnderParent -Name $Folder.name -ParentId $NewParentId
    if ($conflict -and $conflict.id -ne $Folder.id) {
        Write-Host "  UYARI: Hedefte '$($Folder.name)' zaten var; icerik tek tek tasinacak." -ForegroundColor Yellow
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$($Folder.id)")
        foreach ($child in $children) {
            if ($child.type -eq "folder") {
                Move-FolderTree -Folder $child -NewParentId $NewParentId -Reason "$Reason (cocuk)"
            }
            else {
                Move-Resource -Item $child -NewParentId $NewParentId -Reason $Reason
            }
        }
        return
    }

    Move-Resource -Item $Folder -NewParentId $NewParentId -Reason $Reason
}

function Remove-EmptyFolder {
    param(
        [object]$Folder
    )
    $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$($Folder.id)")
    if ($children.Count -gt 0) {
        Write-Host "  Atlandi silme (bos degil): $($Folder.name) ($($children.Count) oge)" -ForegroundColor Yellow
        return $false
    }

    if ($WhatIf) {
        Write-Host "  WhatIf DELETE folder '$($Folder.name)'" -ForegroundColor Yellow
        return $true
    }

    Write-Host "  DELETE bos klasor '$($Folder.name)' ..." -ForegroundColor Yellow
    Invoke-RestMethod -Uri "$apiBase/$($Folder.id)?force=false" -Headers $headers -Method Delete | Out-Null
    Write-Host "    OK" -ForegroundColor Green
    return $true
}

function Resolve-SayfalarId {
    $f = Find-FolderUnderParent -Name $RootSayfalar
    if (-not $f) { throw "'$RootSayfalar' klasoru yok. Once seed-resource-root-folders.ps1 calistirin." }
    return $f.id
}

function Resolve-DokumanlarId {
    $f = Find-FolderUnderParent -Name $RootDokumanlar
    if (-not $f) { throw "'$RootDokumanlar' klasoru yok. Once seed-resource-root-folders.ps1 calistirin." }
    return $f.id
}

function Resolve-MonitraNgOgreticilerId {
    param([string]$SayfalarId)
    $monitra = Find-FolderUnderParent -Name "MonitraNG" -ParentId $SayfalarId
    if (-not $monitra) {
        throw "Sayfalar/MonitraNG bulunamadi."
    }
    $ogreticiler = Find-FolderUnderParent -Name "Öğreticiler" -ParentId $monitra.id
    if (-not $ogreticiler) {
        throw "Sayfalar/MonitraNG/Ogreticiler bulunamadi."
    }
    return $ogreticiler.id
}

function Migrate-OdakToUretilen {
    param([string]$DokumanlarId)

    Write-Host "`n[1/3] Odak -> Dokumanlar/Uretilen/Odak" -ForegroundColor Cyan
    $odak = $null
    foreach ($folder in Get-RootFolders) {
        if ($folder.name -eq "Odak") {
            $odak = $folder
            break
        }
    }
    if (-not $odak) {
        Write-Host "  Atlandi (kokte Odak yok)" -ForegroundColor Gray
        return
    }

    $uretilenId = Ensure-Folder -Name "Üretilen" -ParentId $DokumanlarId
    $existingTarget = Find-FolderUnderParent -Name "Odak" -ParentId $uretilenId

    if ($existingTarget -and $existingTarget.id -ne $odak.id) {
        Write-Host "  Dokumanlar/Uretilen/Odak zaten var; kok icerigi birlestiriliyor..." -ForegroundColor Yellow
        $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$($odak.id)")
        foreach ($child in $children) {
            if ($child.type -eq "folder") {
                Move-FolderTree -Folder $child -NewParentId $existingTarget.id -Reason "Odak birlestirme"
            }
            else {
                Move-Resource -Item $child -NewParentId $existingTarget.id -Reason "Odak birlestirme"
            }
        }
        if ($RemoveEmptySourceFolders) { Remove-EmptyFolder -Folder $odak | Out-Null }
    }
    else {
        Move-Resource -Item $odak -NewParentId $uretilenId -Reason "Odak uretim ciktilari"
    }
}

function Migrate-TestFolder {
    param([string]$SayfalarId)

    Write-Host "`n[2/3] Test klasorleri -> Sayfalar/Test/" -ForegroundColor Cyan
    $testRoots = @(
        Get-RootFolders | Where-Object {
            ($ProtectedRootNames -notcontains $_.name) -and
            ($_.name -like "Klas*r*" -or $_.name -eq "Test")
        }
    )

    if (-not $testRoots -or $testRoots.Count -eq 0) {
        Write-Host "  Atlandi (Klasor * yok)" -ForegroundColor Gray
        return
    }

    $testAreaId = Ensure-Folder -Name "Test" -ParentId $SayfalarId

    foreach ($folder in $testRoots) {
        if ($folder.name -eq "Test") { continue }
        $dest = Find-FolderUnderParent -Name $folder.name -ParentId $testAreaId
        if ($dest) {
            Write-Host "  '$($folder.name)' hedefte var; icerik tasiniyor..." -ForegroundColor Yellow
            $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$($folder.id)")
            foreach ($child in $children) {
                if ($child.type -eq "folder") {
                    Move-FolderTree -Folder $child -NewParentId $testAreaId -Reason "test icerik"
                }
                else {
                    Move-Resource -Item $child -NewParentId $testAreaId -Reason "test icerik"
                }
            }
            if ($RemoveEmptySourceFolders) { Remove-EmptyFolder -Folder $folder | Out-Null }
        }
        else {
            Move-FolderTree -Folder $folder -NewParentId $testAreaId -Reason "test klasoru"
        }
    }
}

function Migrate-LegacyRootOgreticiler {
    param([string]$SayfalarId)

    Write-Host "`n[3/3] Kok Ogreticiler -> MonitraNG/Ogreticiler/Arsiv/eski-kok/" -ForegroundColor Cyan
    $legacy = $null
    foreach ($folder in Get-RootFolders) {
        if ($folder.name -like "*retici*" -and ($ProtectedRootNames -notcontains $folder.name)) {
            $legacy = $folder
            break
        }
    }
    if (-not $legacy) {
        Write-Host "  Atlandi (kokte Ogreticiler yok)" -ForegroundColor Gray
        return
    }

    $canonicalOgreticilerId = Resolve-MonitraNgOgreticilerId -SayfalarId $SayfalarId
    $archiveId = Ensure-Folder -Name "Arşiv" -ParentId $canonicalOgreticilerId
    $eskiKokId = Ensure-Folder -Name "eski-kök" -ParentId $archiveId

    $children = Get-Items (Invoke-DocApi -Method GET -Path "/children?parentId=$($legacy.id)")
    if ($children.Count -eq 0) {
        Write-Host "  Kok Ogreticiler zaten bos." -ForegroundColor Gray
        if ($RemoveEmptySourceFolders) { Remove-EmptyFolder -Folder $legacy | Out-Null }
        return
    }

    Write-Host "  $($children.Count) oge tasinacak (eski kok kopyalari; guncel icerik MonitraNG/Ogreticiler altinda)." -ForegroundColor Gray
    foreach ($child in $children) {
        if ($child.type -eq "folder") {
            Move-FolderTree -Folder $child -NewParentId $eskiKokId -Reason "legacy ogreticiler"
        }
        else {
            Move-Resource -Item $child -NewParentId $eskiKokId -Reason "legacy ogreticiler"
        }
    }

    if ($RemoveEmptySourceFolders) {
        Remove-EmptyFolder -Folder $legacy | Out-Null
    }
}

function Show-RemainingRootFolders {
    Write-Host "`nKok klasorler (islem sonrasi):" -ForegroundColor Cyan
    foreach ($folder in Get-RootFolders) {
        $mark = if ($ProtectedRootNames -contains $folder.name) { "[alan]" } else { "[?]" }
        Write-Host "  $mark $($folder.name) ($($folder.id))"
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "DI Agac Temizlik / Tasima" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
if ($WhatIf) { Write-Host "(WhatIf — yazma yok)" -ForegroundColor Yellow }
Write-Host "========================================" -ForegroundColor Cyan

$sayfalarId = Resolve-SayfalarId
$dokumanlarId = Resolve-DokumanlarId
Write-Host "Sayfalar: $sayfalarId" -ForegroundColor DarkGray
Write-Host "Dokumanlar: $dokumanlarId" -ForegroundColor DarkGray

if (-not $SkipOdak) { Migrate-OdakToUretilen -DokumanlarId $dokumanlarId }
if (-not $SkipTestFolders) { Migrate-TestFolder -SayfalarId $sayfalarId }
if (-not $SkipLegacyOgreticiler) { Migrate-LegacyRootOgreticiler -SayfalarId $sayfalarId }

Show-RemainingRootFolders

Write-Host "`nTamamlandi." -ForegroundColor Cyan
Write-Host "Beklenen kok: Sayfalar, Dokumanlar (+ istege bagli diger is alanlari)" -ForegroundColor Gray
