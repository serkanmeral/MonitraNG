param([string]$Gateway = "http://192.168.20.8:5040", [string]$DatasetId = "fc7c05a6-214a-4d8e-8b64-7fee814276d3")
$token = (Get-Content "$env:TEMP\operationcore_dg_token_prod.txt" -Raw).Trim()
$r = Invoke-RestMethod -Uri "$Gateway/data/api/v1/datasets/$DatasetId" -Headers @{ Authorization = "Bearer $token" }
$out = Join-Path $env:TEMP "dm-templates-dataset.json"
$r | ConvertTo-Json -Depth 12 | Out-File -Encoding utf8 $out
Write-Host "Saved: $out"
($r.fields | ForEach-Object { "$($_.fieldType) $($_.name)" }) -join "`n"
