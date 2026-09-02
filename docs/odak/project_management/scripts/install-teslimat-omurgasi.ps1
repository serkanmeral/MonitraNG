# Teslimat Omurgasi — tek kurulum girisi (F1-0)
#
# Yeni ortamda DI (+ ileride proje) dataset/seed eksigi birakmadan ayağa kalkar.
# Test'e ozel degildir. Mevcut seed-document-intelligence-test.ps1 bu scripti cagirir.
#
# Repo kokunden:
#   .\docs\odak\project_management\scripts\install-teslimat-omurgasi.ps1
#   .\docs\odak\project_management\scripts\install-teslimat-omurgasi.ps1 -VerifyOnly
#   .\docs\odak\project_management\scripts\install-teslimat-omurgasi.ps1 -IncludeOdakContent
#   .\docs\odak\project_management\scripts\install-teslimat-omurgasi.ps1 -WhatIf
#
# Kural: yeni dataset alani veya seed, once manifest.json + bu akisa girer; sonra ortama uygulanir.

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$IncludeOdakContent = $false,
    [switch]$IncludePacks = $false,
    [switch]$SkipSeeds = $false,
    [string[]]$SkipSeedIds = @(),
    [switch]$VerifyOnly = $false,
    [switch]$WhatIf = $false,
    [hashtable]$SeedExtraParams = @{}
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/TeslimatInstallCommon.ps1")

$manifestPath = Join-Path $repoRoot "docs/odak/project_management/install/manifest.json"
if (-not (Test-Path $manifestPath)) { throw "Manifest yok: $manifestPath" }
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

$token = Get-TeslimatToken -Token $Token -BaseUrl $BaseUrl -RepoRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Token yok. get-operationcore-token.ps1 veya -Token / `$env:DI_TOKEN." -ForegroundColor Red
    exit 1
}
$env:DI_TOKEN = $token
$headers = New-TeslimatDgHeaders -Token $token

Write-Host ""
Write-Host "Teslimat Omurgasi install  v$($manifest.version)" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Gray
Write-Host "Manifest: docs/odak/project_management/install/manifest.json" -ForegroundColor Gray
if ($WhatIf) { Write-Host "WhatIf - no writes" -ForegroundColor Yellow }
if ($VerifyOnly) { Write-Host "VerifyOnly - schema/seed not applied" -ForegroundColor Yellow }
Write-Host ""

function Get-TeslimatScriptParamNames {
    param([string]$Path)
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if (-not $ast.ParamBlock) { return @() }
    return @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
}

function Invoke-TeslimatSeedScript {
    param(
        [string]$Title,
        [string]$RelativeScript,
        [hashtable]$Extra = @{}
    )
    $path = Join-Path $repoRoot $RelativeScript
    if (-not (Test-Path $path)) { throw "Seed script yok: $path" }
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    $scriptParams = Get-TeslimatScriptParamNames -Path $path
    $call = @{}
    $candidates = @{
        BaseUrl = $BaseUrl
        Token   = $token
        Server  = ([Uri]$BaseUrl).Host
        WhatIf  = $WhatIf
    }
    foreach ($k in $candidates.Keys) {
        if ($scriptParams -contains $k) { $call[$k] = $candidates[$k] }
    }
    foreach ($k in $Extra.Keys) {
        if ($scriptParams -contains $k) { $call[$k] = $Extra[$k] }
    }
    & $path @call
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Adim basarisiz: $RelativeScript (exit $LASTEXITCODE)"
    }
}

$schemaFiles = @($manifest.schemaFiles)
$order = @($manifest.datasetOrder)
if ($manifest.projectDatasets.datasetOrder) {
    $order += @($manifest.projectDatasets.datasetOrder)
}

$verifyMap = @{}
if ($manifest.verifyFields) {
    $manifest.verifyFields.PSObject.Properties | ForEach-Object { $verifyMap[$_.Name] = @($_.Value) }
}
if ($manifest.projectDatasets.verifyFields) {
    $manifest.projectDatasets.verifyFields.PSObject.Properties | ForEach-Object { $verifyMap[$_.Name] = @($_.Value) }
}

if (-not $VerifyOnly) {
    Write-Host "=== Category + dataset schemas ===" -ForegroundColor Cyan
    $catFile = Join-Path $repoRoot $manifest.category.file
    $categoryId = Ensure-TeslimatDatasetCategory -BaseUrl $BaseUrl -Headers $headers -CategoryFile $catFile -WhatIf:$WhatIf
    $byName = Import-TeslimatSchemaMap -RepoRoot $repoRoot -RelativeFiles $schemaFiles

    foreach ($name in $manifest.datasetOrder) {
        if (-not $byName.ContainsKey($name)) {
            Write-Host "  Manifest dataset tanimi yok: $name" -ForegroundColor Red
            throw "Eksik schema: $name"
        }
        Write-Host "$name" -ForegroundColor Yellow
        $null = Ensure-TeslimatDataset -BaseUrl $BaseUrl -Headers $headers -Schema $byName[$name] -CategoryId $categoryId -WhatIf:$WhatIf
    }

    if ($manifest.projectDatasets -and $manifest.projectDatasets.schemaFiles) {
        Write-Host ""
        Write-Host "=== Project datasets (pm_*) ===" -ForegroundColor Cyan
        $pmCatRel = [string]$manifest.projectDatasets.category.file
        if ([string]::IsNullOrWhiteSpace($pmCatRel)) {
            throw "manifest.projectDatasets.category.file gerekli"
        }
        $pmCatFile = Join-Path $repoRoot $pmCatRel
        $pmCategoryId = Ensure-TeslimatDatasetCategory -BaseUrl $BaseUrl -Headers $headers -CategoryFile $pmCatFile -WhatIf:$WhatIf
        $pmFiles = @($manifest.projectDatasets.schemaFiles)
        $pmByName = Import-TeslimatSchemaMap -RepoRoot $repoRoot -RelativeFiles $pmFiles
        foreach ($name in @($manifest.projectDatasets.datasetOrder)) {
            if (-not $pmByName.ContainsKey($name)) {
                Write-Host "  Manifest project dataset tanimi yok: $name" -ForegroundColor Red
                throw "Eksik schema: $name"
            }
            Write-Host "$name" -ForegroundColor Yellow
            $null = Ensure-TeslimatDataset -BaseUrl $BaseUrl -Headers $headers -Schema $pmByName[$name] -CategoryId $pmCategoryId -WhatIf:$WhatIf
        }
    }

    if (-not $SkipSeeds) {
        $skip = @($SkipSeedIds)
        foreach ($step in @($manifest.profiles.core.seeds)) {
            if ($skip -contains $step.id) {
                Write-Host "SKIP seed $($step.id)" -ForegroundColor Yellow
                continue
            }
            $extra = @{}
            if ($SeedExtraParams -and $SeedExtraParams.ContainsKey($step.id)) { $extra = $SeedExtraParams[$step.id] }
            Invoke-TeslimatSeedScript -Title $step.id -RelativeScript $step.script -Extra $extra
        }
        if ($IncludeOdakContent) {
            foreach ($step in @($manifest.profiles.'odak-content'.seeds)) {
                if ($skip -contains $step.id) {
                    Write-Host "SKIP seed $($step.id)" -ForegroundColor Yellow
                    continue
                }
                $extra = @{}
                if ($SeedExtraParams -and $SeedExtraParams.ContainsKey($step.id)) { $extra = $SeedExtraParams[$step.id] }
                Invoke-TeslimatSeedScript -Title $step.id -RelativeScript $step.script -Extra $extra
            }
        }
        if ($IncludePacks) {
            $packSeeds = @($manifest.profiles.packs.seeds)
            if ($packSeeds.Count -eq 0) {
                Write-Host ""
                Write-Host "=== packs ===" -ForegroundColor Cyan
                Write-Host "  (empty)" -ForegroundColor Gray
            }
            else {
                foreach ($step in $packSeeds) {
                    Invoke-TeslimatSeedScript -Title $step.id -RelativeScript $step.script
                }
            }
        }
    }
}

Write-Host ""
Write-Host "=== Verify datasets ===" -ForegroundColor Cyan
$failed = @()
foreach ($name in $order) {
    $required = @()
    if ($verifyMap.ContainsKey($name)) { $required = $verifyMap[$name] }
    $result = Test-TeslimatDataset -BaseUrl $BaseUrl -Headers $headers -Name $name -RequiredFields $required
    if ($result.Ok) {
        $fieldCount = @($result.Fields).Count
        Write-Host ("  OK {0} ({1} fields)" -f $name, $fieldCount) -ForegroundColor Green
    }
    else {
        $failed += $name
        $missing = $result.Missing -join ","
        Write-Host ("  FAIL {0} {1} missing={2}" -f $name, $result.Error, $missing) -ForegroundColor Red
    }
}

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Host ("Kurulum dogrulamasi basarisiz: {0}" -f ($failed -join ", ")) -ForegroundColor Red
    exit 1
}

Write-Host ("Teslimat Omurgasi core verified ({0} datasets)." -f $order.Count) -ForegroundColor Green
if (-not $IncludeOdakContent) {
    Write-Host "Odak ornek icerik icin: -IncludeOdakContent" -ForegroundColor Gray
}
