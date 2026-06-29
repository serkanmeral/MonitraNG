# IT Destek — Talep olustur + Agent kuyrugu board silme (Production)
# Olay (Incident) board kalir. Bagli work item'lar once Olay board'a tasinir.
#
#   .\get-operationcore-token-prod.ps1
#   .\remove-it-helpdesk-legacy-boards-prod.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$moPath = if ($UseGateway) { "/operations/api/v1" } else { "/api/v1" }

$workspaceId = "383e85b7-d835-4843-a7ab-8862996ec1ee"
$olayBoardId = "f4d58e69-51e6-4a6b-8fd4-91434c2d0814"
$boardsToRemove = @(
    @{ Name = "Talep olustur"; Id = "c24f1f5a-6918-47aa-832b-7a466c9af1f0" },
    @{ Name = "Agent kuyrugu"; Id = "60eb6bef-ab79-420b-bca5-5e4a9c3bd204" }
)

$loadTokenScript = Join-Path $PSScriptRoot "load-operationcore-token-prod.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Get-Items($Response) {
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items", "Items")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return $items }
            return @($items)
        }
    }
    return @($Response)
}

Write-Host "=== IT Destek legacy board kaldirma ===" -ForegroundColor Cyan

# 1. Work items -> Olay board
foreach ($board in $boardsToRemove) {
    $filter = [Uri]::EscapeDataString("boardId:eq:$($board.Id)")
    $items = @(Get-Items (Invoke-RestMethod -Uri "$BaseUrl$dataPath/op_work_items?filter=$filter&limit=200" -Headers $headers))
    if ($items.Count -eq 0) {
        Write-Host "[wi] $($board.Name): bagli kayit yok" -ForegroundColor Gray
        continue
    }
    Write-Host "[wi] $($board.Name): $($items.Count) kayit -> Olay board" -ForegroundColor Yellow
    foreach ($wi in $items) {
        $wiId = $wi.__dataId
        Write-Host "  $($wi.key) ($wiId)" -ForegroundColor Gray
        if ($WhatIf) { continue }
        $body = (@{ boardId = $olayBoardId } | ConvertTo-Json -Compress)
        Invoke-RestMethod -Uri "$BaseUrl$moPath/work-items/$wiId" -Method PATCH -Headers $headers -Body $body | Out-Null
    }
}

# 2. Delete boards
foreach ($board in $boardsToRemove) {
    $filter = [Uri]::EscapeDataString("boardId:eq:$($board.Id)")
    $remaining = @(Get-Items (Invoke-RestMethod -Uri "$BaseUrl$dataPath/op_work_items?filter=$filter&limit=5" -Headers $headers))
    if ($remaining.Count -gt 0) {
        Write-Host "HATA: $($board.Name) hala $($remaining.Count) work item bagli — silinmedi." -ForegroundColor Red
        continue
    }
    if ($WhatIf) {
        Write-Host "[board] WhatIf: silinecek $($board.Name) ($($board.Id))" -ForegroundColor Yellow
        continue
    }
    Invoke-RestMethod -Uri "$BaseUrl$dataPath/op_boards/$($board.Id)" -Method DELETE -Headers $headers | Out-Null
    Write-Host "[board] Silindi: $($board.Name) ($($board.Id))" -ForegroundColor Green
}

# 3. Verify
$filter = [Uri]::EscapeDataString("workspaceId:eq:$workspaceId")
$left = @(Get-Items (Invoke-RestMethod -Uri "$BaseUrl$dataPath/op_boards?filter=$filter&limit=20" -Headers $headers))
Write-Host "`nKalan board'lar ($($left.Count)):" -ForegroundColor Cyan
$left | ForEach-Object { Write-Host "  $($_.name) [$($_.__dataId)]" }
