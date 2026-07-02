# Aggregate odak_sevkiyat_kalemleri -> odak_siparis_kalemleri.shippedQuantity (list API, no filter)

function Get-OdakShipmentStatusNormalized {
    param([string]$LegacyStatus)
    $s = [string]$LegacyStatus
    if ($s -match 'Tamam') { return "Tamamlandi" }
    if ($s -match 'Iptal|İptal') { return "Iptal" }
    if ($s -match 'Plan') { return "Planlandi" }
    return "Planlandi"
}

function Get-OdakDgListItems {
    param(
        [scriptblock]$InvokeDg,
        [string]$BaseUrl,
        [string]$DataPath,
        [string]$Dataset,
        [int]$Skip,
        [int]$Limit = 500
    )
    $uri = '{0}{1}/{2}?skip={3}&limit={4}' -f $BaseUrl, $DataPath, $Dataset, $Skip, $Limit
    $raw = & $InvokeDg -Method GET -Uri $uri
    if ($raw -is [Array]) { return @($raw) }
    if ($raw.items) { return @($raw.items) }
    if ($raw.data) { return @($raw.data) }
    if ($raw.__dataId -or $raw.dataId) { return @($raw) }
    return @()
}

function Invoke-OdakLineShippedQuantityBackfill {
    param(
        [scriptblock]$InvokeDg,
        [string]$BaseUrl,
        [string]$DataPath,
        [switch]$DryRun
    )

    Write-Host "`n=== Kalem sevk miktarlari (list API) ===" -ForegroundColor Cyan

    $completedShipmentIds = @{}
    $skip = 0
    $limit = 500
    $shipTotal = 0
    Write-Host "Tamamlanan sevkiyatlar taranıyor..." -ForegroundColor Gray
    while ($true) {
        $items = Get-OdakDgListItems -InvokeDg $InvokeDg -BaseUrl $BaseUrl -DataPath $DataPath -Dataset "odak_sevkiyatlar" -Skip $skip -Limit $limit
        if (-not $items.Count) { break }
        foreach ($s in $items) {
            if ((Get-OdakShipmentStatusNormalized ([string]$s.status)) -ne "Tamamlandi") { continue }
            $id = $s.__dataId; if (-not $id) { $id = $s.dataId }
            if ($id) {
                $completedShipmentIds[[string]$id] = $true
                $shipTotal++
            }
        }
        Write-Host "  odak_sevkiyatlar skip=$skip +$($items.Count) (tamamlanan toplam $shipTotal)" -ForegroundColor DarkGray
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }

    $lineQty = @{}
    $skip = 0
    $shipLineRows = 0
    Write-Host "Sevkiyat kalemleri toplaniyor..." -ForegroundColor Gray
    while ($true) {
        $items = Get-OdakDgListItems -InvokeDg $InvokeDg -BaseUrl $BaseUrl -DataPath $DataPath -Dataset "odak_sevkiyat_kalemleri" -Skip $skip -Limit $limit
        if (-not $items.Count) { break }
        foreach ($si in $items) {
            $shipId = Get-RelationId $si.parentShipmentId
            if (-not $shipId -or -not $completedShipmentIds.ContainsKey([string]$shipId)) { continue }
            $parentLineId = Get-RelationId $si.parentLineId
            if (-not $parentLineId) { continue }
            $q = [double]$si.shippedQuantity
            $key = [string]$parentLineId
            if (-not $lineQty.ContainsKey($key)) { $lineQty[$key] = 0.0 }
            $lineQty[$key] += $q
            $shipLineRows++
        }
        Write-Host "  odak_sevkiyat_kalemleri skip=$skip +$($items.Count) (eslesen satir $shipLineRows)" -ForegroundColor DarkGray
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    Write-Host "  $($lineQty.Count) siparis kalemi icin toplam sevk miktari hesaplandi" -ForegroundColor Gray

    $skip = 0
    $updated = 0
    $unchanged = 0
    $errors = 0
    Write-Host "Siparis kalemleri guncelleniyor..." -ForegroundColor Gray
    while ($true) {
        $items = Get-OdakDgListItems -InvokeDg $InvokeDg -BaseUrl $BaseUrl -DataPath $DataPath -Dataset "odak_siparis_kalemleri" -Skip $skip -Limit $limit
        if (-not $items.Count) { break }
        foreach ($line in $items) {
            $lid = $line.__dataId; if (-not $lid) { $lid = $line.dataId }
            if (-not $lid) { continue }
            $target = if ($lineQty.ContainsKey([string]$lid)) { $lineQty[[string]$lid] } else { 0.0 }
            $current = [double]$line.shippedQuantity
            if ([math]::Abs($current - $target) -lt 0.0001) {
                $unchanged++
                continue
            }
            if ($DryRun) {
                $updated++
                continue
            }
            try {
                & $InvokeDg -Method PUT -Uri ('{0}{1}/odak_siparis_kalemleri/{2}' -f $BaseUrl, $DataPath, $lid) -Body @{ shippedQuantity = $target } | Out-Null
                $updated++
            }
            catch {
                $errors++
                if ($errors -le 5) {
                    Write-Warning "Kalem $lid guncellenemedi: $($_.Exception.Message)"
                }
            }
        }
        if (($skip / $limit) % 2 -eq 0) {
            Write-Host "  odak_siparis_kalemleri skip=$skip guncellenen=$updated degismeyen=$unchanged hata=$errors" -ForegroundColor DarkGray
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }

    Write-Host "Tamamlandi: guncellenen=$updated degismeyen=$unchanged hata=$errors dryRun=$DryRun" -ForegroundColor Green
    return @{
        updated   = $updated
        unchanged = $unchanged
        errors    = $errors
        lineQtyKeys = $lineQty.Count
    }
}
