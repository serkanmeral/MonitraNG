# Zimmet — master veri seed (F0-F1)
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\setup-zimmet-datasets-and-forms.ps1
#   .\docs\odak\zimmet\scripts\seed-zimmet-master-data.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$SeedFile = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/ZimmetDgCommon.ps1")

if ([string]::IsNullOrEmpty($SeedFile)) {
    $SeedFile = Join-Path $repoRoot "docs/odak/zimmet/seed/zimmet_master_seed.json"
}
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $repoRoot "docs/odak/zimmet/seed/zimmet_master_ids.json"
}

$ctx = Initialize-ZimmetDgSession -BaseUrl $BaseUrl -UseGateway:$UseGateway -RepoRoot $repoRoot
$seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json

$ids = [ordered]@{
    urunGruplari = @{}
    urunler      = @{}
    depolar      = @{}
    lokasyonlar  = @{}
    demirbaslar  = @{}
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Zimmet — master data seed" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[1] Urun gruplari..." -ForegroundColor Yellow
foreach ($g in $seed.urunGruplari) {
    $body = @{
        kod           = $g.kod
        ad            = $g.ad
        trackBySerial = [bool]$g.trackBySerial
        isFixedAsset  = [bool]$g.isFixedAsset
        isConsumable  = [bool]$g.isConsumable
        aktif         = [bool]$g.aktif
    }
    if ($g.aciklama) { $body.aciklama = $g.aciklama }
    $id = Sync-ZimmetRecord -Ctx $ctx -Collection "zimmet_urun_gruplari" -Filter "kod:eq:$($g.kod)" -Body $body -Label $g.kod
    $ids.urunGruplari[$g.kod] = $id
}

Write-Host "[2] Urun katalogu..." -ForegroundColor Yellow
foreach ($u in $seed.urunler) {
    $grupId = $ids.urunGruplari[$u.urunGrubuKod]
    if (-not $grupId) { throw "Urun grubu bulunamadi: $($u.urunGrubuKod)" }
    $body = @{
        kod                 = $u.kod
        ad                  = $u.ad
        urunGrubuId         = $grupId
        marka               = $u.marka
        model               = $u.model
        varsayilanGarantiAy = $u.varsayilanGarantiAy
        aktif               = [bool]$u.aktif
    }
    if ($u.aciklama) { $body.aciklama = $u.aciklama }
    $id = Sync-ZimmetRecord -Ctx $ctx -Collection "zimmet_urunler" -Filter "kod:eq:$($u.kod)" -Body $body -Label $u.kod
    $ids.urunler[$u.kod] = $id
}

Write-Host "[3] Depolar..." -ForegroundColor Yellow
foreach ($d in $seed.depolar) {
    $body = @{
        kod   = $d.kod
        ad    = $d.ad
        aktif = [bool]$d.aktif
    }
    if ($d.adres) { $body.adres = $d.adres }
    $id = Sync-ZimmetRecord -Ctx $ctx -Collection "zimmet_depolar" -Filter "kod:eq:$($d.kod)" -Body $body -Label $d.kod
    $ids.depolar[$d.kod] = $id
}

Write-Host "[4] Lokasyonlar..." -ForegroundColor Yellow
foreach ($l in $seed.lokasyonlar) {
    $depoId = $ids.depolar[$l.depoKod]
    if (-not $depoId) { throw "Depo bulunamadi: $($l.depoKod)" }
    $body = @{
        kod    = $l.kod
        ad     = $l.ad
        depoId = $depoId
        aktif  = [bool]$l.aktif
    }
    $filter = "kod:eq:$($l.kod)"
    $id = Sync-ZimmetRecord -Ctx $ctx -Collection "zimmet_depo_lokasyonlari" -Filter $filter -Body $body -Label "$($l.depoKod)/$($l.kod)"
    $ids.lokasyonlar["$($l.depoKod)/$($l.kod)"] = $id
}

Write-Host "[5] Demirbaslar..." -ForegroundColor Yellow
foreach ($dm in $seed.demirbaslar) {
    $katalogId = $ids.urunler[$dm.katalogUrunKod]
    $depoId = $ids.depolar[$dm.depoKod]
    $lokKey = "$($dm.depoKod)/$($dm.lokasyonKod)"
    $lokId = $ids.lokasyonlar[$lokKey]
    if (-not $katalogId) { throw "Katalog urun yok: $($dm.katalogUrunKod)" }

    $body = @{
        katalogUrunId = $katalogId
        durum         = $dm.durum
        girisTarihi   = $dm.girisTarihi
    }
    if ($dm.seriNo) { $body.seriNo = $dm.seriNo }
    if ($dm.marka) { $body.marka = $dm.marka }
    if ($dm.model) { $body.model = $dm.model }
    if ($depoId) { $body.depoId = $depoId }
    if ($lokId) { $body.lokasyonId = $lokId }
    if ($dm.garantiBitis) { $body.garantiBitis = $dm.garantiBitis }
    if ($dm.alisFiyati) { $body.alisFiyati = $dm.alisFiyati }
    if ($dm.notlar) { $body.notlar = $dm.notlar }
    if ($dm.girisRef) { $body.girisRef = $dm.girisRef }
    if ($dm.zimmetRef) { $body.zimmetRef = $dm.zimmetRef }
    if ($dm.zimmetliPersonelId) { $body.zimmetliPersonelId = $dm.zimmetliPersonelId }

    $filter = if ($dm.seriNo) { "seriNo:eq:$($dm.seriNo)" } else { "notlar:eq:$($dm.seedKey)" }
    if ($dm.seriNo) {
        $id = Sync-ZimmetRecord -Ctx $ctx -Collection "zimmet_demirbaslar" -Filter $filter -Body $body -Label $dm.seedKey
    }
    else {
        $id = Find-ZimmetOrCreate -Ctx $ctx -Collection "zimmet_demirbaslar" -Filter $filter -Body ($body + @{ notlar = $dm.seedKey }) -Label $dm.seedKey
    }
    $ids.demirbaslar[$dm.seedKey] = $id
}

$out = @{
    seededAt = (Get-Date).ToUniversalTime().ToString("o")
    gateway  = $BaseUrl
    ids      = $ids
}
$out | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "`nID ozeti: $OutputFile" -ForegroundColor Cyan
Write-Host "Tamamlandi." -ForegroundColor Green
