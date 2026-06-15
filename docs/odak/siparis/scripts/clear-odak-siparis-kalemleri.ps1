# odak_siparis_kalemleri — tum kayitlari sil (index repair oncesi)
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$h = @{ Authorization = "Bearer $token" }

$deleted = 0
$skip = 0
while ($true) {
    $uri = '{0}{1}/odak_siparis_kalemleri?skip={2}&limit=100' -f $BaseUrl, $dataPath, $skip
    $items = Invoke-RestMethod -Uri $uri -Headers $h
    if ($items -isnot [Array]) { $items = @($items) }
    if (-not $items.Count) { break }
    foreach ($item in $items) {
        $id = $item.__dataId; if (-not $id) { $id = $item.dataId }
        if (-not $id) { continue }
        if ($DryRun) { Write-Host "[DRY] delete $id"; continue }
        Invoke-RestMethod -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri/$id" -Method DELETE -Headers $h | Out-Null
        $deleted++
    }
    if ($items.Count -lt 100) { break }
}
Write-Host "Silinen kalem: $deleted (DryRun=$DryRun)" -ForegroundColor Green
