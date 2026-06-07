# op_notification_policies — mail policy alanlari (transitionKey, from/to state, emailSubject)
#
# Kullanim:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\notifications\scripts\patch-op-notification-policies-mail-fields.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
if (-not (Test-Path $tokenFile)) {
    throw "Token yok. Once: .\docs\odak\operationcore\scripts\get-operationcore-token.ps1"
}
$token = (Get-Content $tokenFile -Raw).Trim()
if ([string]::IsNullOrEmpty($token)) { throw "Token dosyasi bos." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$datasetName = "op_notification_policies"
$getUri = "$BaseUrl/data/api/v1/datasets/$datasetName"
$putUri = $getUri

Write-Host "GET $getUri" -ForegroundColor Cyan
$dataset = Invoke-RestMethod -Method GET -Uri $getUri -Headers $headers

$fields = @($dataset.fields)
if (-not $fields -and $dataset.Fields) { $fields = @($dataset.Fields) }

$existingNames = @{}
foreach ($f in $fields) {
    $n = if ($f.name) { $f.name } else { $f.Name }
    if ($n) { $existingNames[$n] = $true }
}

$newFields = @(
    @{
        fieldType        = "text"
        name             = "transitionKey"
        title            = "Gecis anahtari"
        mandatory        = $false
        unique           = $false
        isArray          = $false
        relationDataset  = $null
        incrementalOptions = $null
    },
    @{
        fieldType        = "relation"
        name             = "fromStateId"
        title            = "Kaynak durum"
        mandatory        = $false
        unique           = $false
        isArray          = $false
        relationDataset  = "op_states"
        incrementalOptions = $null
    },
    @{
        fieldType        = "relation"
        name             = "toStateId"
        title            = "Hedef durum"
        mandatory        = $false
        unique           = $false
        isArray          = $false
        relationDataset  = "op_states"
        incrementalOptions = $null
    },
    @{
        fieldType        = "text"
        name             = "emailSubject"
        title            = "E-posta konu override"
        mandatory        = $false
        unique           = $false
        isArray          = $false
        relationDataset  = $null
        incrementalOptions = $null
    }
)

$added = 0
foreach ($nf in $newFields) {
    if (-not $existingNames.ContainsKey($nf.name)) {
        $fields += $nf
        $added++
        Write-Host "  + alan: $($nf.name)" -ForegroundColor Green
    }
    else {
        Write-Host "  = zaten var: $($nf.name)" -ForegroundColor DarkYellow
    }
}

if ($added -eq 0) {
    Write-Host "Yeni alan yok — patch gerekmedi." -ForegroundColor Cyan
    exit 0
}

$body = @{
    Fields = $fields
} | ConvertTo-Json -Depth 30 -Compress

Write-Host "PUT $putUri ($added yeni alan)" -ForegroundColor Cyan
Invoke-RestMethod -Method PUT -Uri $putUri -Headers $headers -Body $body | Out-Null
Write-Host "OK — op_notification_policies guncellendi." -ForegroundColor Green
