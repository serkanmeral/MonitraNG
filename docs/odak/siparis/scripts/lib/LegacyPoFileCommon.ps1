# Legacy Kalite PO PDF — polink + package_no + po_version (PackagesController::po)

function Get-LegacyPoRelativePdfPath {
    param(
        [Parameter(Mandatory = $true)][string]$Polink,
        [Parameter(Mandatory = $true)][string]$PackageNo,
        [string]$PoVersion = ""
    )
    $dir = $Polink.Trim().TrimStart('/\').Replace('/', '\')
    if ($dir -and -not $dir.EndsWith('\')) { $dir += '\' }
    $versionSuffix = if ([string]::IsNullOrWhiteSpace($PoVersion)) { '' } else { "_$($PoVersion.Trim())" }
    return "$dir$PackageNo$versionSuffix.pdf"
}

function Get-LegacyPoAbsolutePdfPath {
    param(
        [Parameter(Mandatory = $true)][string]$UploadRoot,
        [Parameter(Mandatory = $true)][string]$Polink,
        [Parameter(Mandatory = $true)][string]$PackageNo,
        [string]$PoVersion = ""
    )
    $relative = Get-LegacyPoRelativePdfPath -Polink $Polink -PackageNo $PackageNo -PoVersion $PoVersion
    return Join-Path $UploadRoot $relative
}

function Test-LegacyPoPdfExists {
    param(
        [Parameter(Mandatory = $true)][string]$UploadRoot,
        [Parameter(Mandatory = $true)][string]$Polink,
        [Parameter(Mandatory = $true)][string]$PackageNo,
        [string]$PoVersion = ""
    )
    $path = Get-LegacyPoAbsolutePdfPath -UploadRoot $UploadRoot -Polink $Polink -PackageNo $PackageNo -PoVersion $PoVersion
    return Test-Path $path -PathType Leaf
}

function Resolve-LegacyPoPdfPath {
    param(
        [Parameter(Mandatory = $true)][string]$UploadRoot,
        [Parameter(Mandatory = $true)][string]$Polink,
        [Parameter(Mandatory = $true)][string]$PackageNo,
        [string]$PoVersion = ""
    )
    $primary = Get-LegacyPoAbsolutePdfPath -UploadRoot $UploadRoot -Polink $Polink -PackageNo $PackageNo -PoVersion $PoVersion
    if (Test-Path $primary -PathType Leaf) {
        return (Resolve-Path $primary).Path
    }

    # Alternatif: bazen buyuk harf .PDF
    $alt = [IO.Path]::ChangeExtension($primary, '.PDF')
    if (Test-Path $alt -PathType Leaf) {
        return (Resolve-Path $alt).Path
    }

    return $null
}

function Get-LegacyPoOriginalFileName {
    param(
        [Parameter(Mandatory = $true)][string]$PackageNo,
        [string]$PoVersion = ""
    )
    $versionSuffix = if ([string]::IsNullOrWhiteSpace($PoVersion)) { '' } else { "_$($PoVersion.Trim())" }
    return "$PackageNo$versionSuffix.pdf"
}

function Test-DgHasStoredPoDocument {
    param($PackageRow)
    if (-not $PackageRow) { return $false }
    $doc = $PackageRow.poDocument
    if ($null -eq $doc) { return $false }
    if ($doc -is [string] -and $doc.Trim()) { return $true }
    if ($doc.path -and [string]$doc.path.Trim()) { return $true }
    if ($doc.Path -and [string]$doc.Path.Trim()) { return $true }
    return $false
}
