# MngReactor Config Sync Test
# GET /api/v1/engine/config, /api/v1/engine/config-string
# Onkosullar: MngReactor, MngKeeper, MongoDB calisiyor; mon_engines'te en az 1 engine var

param(
    [string]$BaseUrl = "http://localhost:15010",
    [string]$EngineId = ""
)

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11
} catch { }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$loadTokenScript = Join-Path $scriptPath "auth\load-token.ps1"
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "Hata: load-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host "MngReactor Config Sync Test"
Write-Host "========================================"
Write-Host ""

# Token al
Write-Host "[1] Token aliniyor..."
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Hata: Token alinamadi. MngKeeper calisiyor mu?" -ForegroundColor Red
    exit 1
}
Write-Host "  OK: Token alindi" -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$params = @{ Uri = ""; Method = "GET"; Headers = $headers; ErrorAction = "Stop" }
$hasSkipCertCheck = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }
if ($hasSkipCertCheck) { $params.SkipCertificateCheck = $true }

# EngineId yoksa mon_engines'ten al (GET /monitoring/engines)
if ([string]::IsNullOrEmpty($EngineId)) {
    Write-Host "[2] Engine listesi aliniyor..."
    try {
        $params.Uri = "$BaseUrl/api/v1/monitoring/engines"
        $engines = Invoke-RestMethod @params
        if ($engines -and $engines.data -and $engines.data.Count -gt 0) {
            $EngineId = $engines.data[0].__dataId
            Write-Host "  OK: engineId = $EngineId" -ForegroundColor Green
        } else {
            Write-Host "  UYARI: mon_engines bos. Test domain'de engine olusturun." -ForegroundColor Yellow
            Write-Host "  Ornek: .\test-config-sync.ps1 -EngineId 'test-engine-1'" -ForegroundColor Gray
            exit 1
        }
    } catch {
        Write-Host "  Hata: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[2] EngineId parametreden: $EngineId"
}
Write-Host ""

# Config Sync
Write-Host "[3] GET /api/v1/engine/config?engineId=$EngineId ..."
try {
    $params.Uri = "$BaseUrl/api/v1/engine/config?engineId=$EngineId"
    $config = Invoke-RestMethod @params
    if ($config -and ($config.agents -ne $null -or $config.assetConfigs -ne $null)) {
        Write-Host "  PASS: Config alindi (agents/assetConfigs mevcut)" -ForegroundColor Green
        if ($config.agents) { Write-Host "    agents: $($config.agents.Count)" }
        if ($config.assetConfigs) { Write-Host "    assetConfigs: $($config.assetConfigs.Count)" }
        # Ham JSON - agentName, assetName, itemName kontrolu icin
        Write-Host ""
        Write-Host "  --- assetConfigs ornegi (ilk kayit) ---" -ForegroundColor Cyan
        if ($config.assetConfigs -and $config.assetConfigs.Count -gt 0) {
            $config.assetConfigs[0] | ConvertTo-Json -Depth 5
        }
    } else {
        Write-Host "  WARN: Config alindi ama bos olabilir" -ForegroundColor Yellow
    }
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404) {
        Write-Host "  404: Engine bulunamadi" -ForegroundColor Yellow
    } else {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Config String
Write-Host "[4] GET /api/v1/engine/config-string?engineId=$EngineId ..."
try {
    $params.Uri = "$BaseUrl/api/v1/engine/config-string?engineId=$EngineId"
    $configStr = Invoke-RestMethod @params
    if ($configStr -and $configStr.configString) {
        $len = $configStr.configString.Length
        Write-Host "  PASS: configString alindi (uzunluk: $len)" -ForegroundColor Green
    } else {
        Write-Host "  WARN: configString bos veya null" -ForegroundColor Yellow
    }
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404) {
        Write-Host "  404: Engine bulunamadi" -ForegroundColor Yellow
    } else {
        Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

Write-Host "========================================"
Write-Host "Config Sync test tamamlandi."
exit 0
