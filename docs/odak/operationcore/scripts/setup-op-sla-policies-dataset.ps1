# op_sla_policies dataset — Odak DG (SLA-0)
#   .\docs\odak\operationcore\scripts\setup-op-sla-policies-dataset.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
$loadTokenScript = Join-Path $PSScriptRoot "load-operationcore-token.ps1"
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$body = @{
    Name        = "op_sla_policies"
    Description = "Operational Core - SLA Policies"
    ForceSchema = $false
    Logging     = "self"
    PublishMode = "basic"
    Fields      = @()
} | ConvertTo-Json -Compress

$uri = "$BaseUrl/data/api/v1/datasets"
Write-Host "POST $uri (op_sla_policies)" -ForegroundColor Cyan
try {
    $null = Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -Body $body
    Write-Host "OK - op_sla_policies olusturuldu." -ForegroundColor Green
}
catch {
    $msg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
    if ($msg -match "mevcut|already|duplicate|409|zaten|400") {
        Write-Host "Zaten var veya dataset mevcut - atlandi." -ForegroundColor Yellow
    }
    else {
        Write-Host "HATA: $msg" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Tamam. Workspace SLA sekmesinden politika tanimlayin." -ForegroundColor Cyan
