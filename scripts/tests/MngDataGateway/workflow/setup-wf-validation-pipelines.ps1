# Workflow — @wf_validation_pipelines dataset + Task Manager (tm_issues) pipeline seed
# Ref: docs/content/workflow/WORKFLOW_PLANNING.md, docs/content/task_manager/TASK_MANAGER_PLANNING.md
#
# Sonra: tm_issues dataset'ine HTTP validation ekleyin (Gateway uzerinden MngWorkflow):
#   URL: https://localhost:5040/workflow/api/v1/validate/tm_issues
#   Method: POST
#   (Docker: host yerine gateway servis adi / port)
#
param(
    [string]$BaseUrl = "https://localhost:5040",
    [switch]$UseGateway = $true
)
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$dataPath     = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = Get-Location }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a,$b,$c,$d) $true }

function Invoke-CreateDataset {
    param([string]$Name, [object]$Schema)
    $uri = "$BaseUrl$datasetsPath"
    $body = $Schema | ConvertTo-Json -Depth 20 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  $Name olusturuldu" -ForegroundColor Green
                return $true
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already|zaten")) {
                Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow
                return $true
            }
            Write-Host "  HATA: HTTP $httpCode" -ForegroundColor Red
            if ($responseBody) { Write-Host "  $responseBody" -ForegroundColor Gray }
            return $false
        } catch {
            Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    try {
        $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $null = Invoke-RestMethod @irmParams
        Write-Host "  $Name olusturuldu" -ForegroundColor Green
        return $true
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already|zaten")) {
            Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow
            return $true
        }
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Invoke-SeedRecord {
    param([string]$DatasetName, [hashtable]$Record, [string]$Label)
    $enc = [uri]::EscapeDataString($DatasetName)
    $uri = "$BaseUrl$dataPath/$enc"
    $body = $Record | ConvertTo-Json -Depth 25 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  Seed: $Label" -ForegroundColor Green
                return $true
            }
            Write-Host "  Seed (uyari): $Label HTTP $httpCode" -ForegroundColor Yellow
            return $true
        } catch {
            return $false
        }
    }
    try {
        $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $null = Invoke-RestMethod @irmParams
        Write-Host "  Seed: $Label" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "  Seed hata: $Label" -ForegroundColor Red
        return $false
    }
}

Write-Host "`n@wf_validation_pipelines dataset + tm_issues pipeline`n" -ForegroundColor Cyan

$schema = @{
    Name        = "@wf_validation_pipelines"
    Description = "MngWorkflow - Dataset bazli validation pipeline tanimlari"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Pipeline adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "dataset"; title = "Hedef dataset (ornek tm_issues)"; mandatory = $true; isArray = $false },
        @{ fieldType = "number"; name = "order"; title = "Calisma sirasi"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "steps"; title = "Adimlar (fetch, assert, return)"; mandatory = $true; isArray = $true }
    )
    IndexList   = @(
        @{ name = "idx_dataset"; fields = @{ dataset = 1 }; unique = $false },
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true }
    )
}

if (-not (Invoke-CreateDataset "@wf_validation_pipelines" $schema)) { exit 1 }

$steps = @(
    @{ type = "fetch"; dataset = "tm_projects"; by = "__dataId"; value = "projectId" },
    @{ type = "assert"; expr = "result.key == payload.projectKey"; message = "projectKey, secilen proje kodu ile eslesmiyor" }
)

$pipelineRecord = @{
    name    = "tm_issues_project_key"
    dataset = "tm_issues"
    order   = 0
    steps   = $steps
}

Write-Host "`nPipeline kaydi ekleniyor..." -ForegroundColor Yellow
Invoke-SeedRecord -DatasetName "@wf_validation_pipelines" -Record $pipelineRecord -Label "tm_issues_project_key" | Out-Null

Write-Host "`nTamam. Sonraki adim: tm_issues dataset schema validations -> HTTP POST workflow URL" -ForegroundColor Green
Write-Host "  Ornek URL (Gateway): $BaseUrl/workflow/api/v1/validate/tm_issues" -ForegroundColor Gray
Write-Host ""
