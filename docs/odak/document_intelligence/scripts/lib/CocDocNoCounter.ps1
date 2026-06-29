# ODK-COC belge numarasi sayaci — pilot (D2 deploy oncesi).
# Format: ODK-COC-{yy}-{seq}  ornek: ODK-COC-26-1

function Get-CocDocNoCounterPath {
    param([string]$RepoRoot = "")
    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
    }
    return Join-Path $RepoRoot "docs/odak/document_intelligence/datasets/coc-docno-counter.json"
}

function Get-NextCocDocNo {
    param(
        [string]$CounterFile = "",
        [int]$StartValue = 1
    )
    if ([string]::IsNullOrWhiteSpace($CounterFile)) {
        $CounterFile = Get-CocDocNoCounterPath
    }

    $yearKey = (Get-Date).ToString('yy')
    $dir = Split-Path $CounterFile -Parent
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

    $state = [ordered]@{ schemaVersion = 1; startValue = $StartValue; years = [ordered]@{} }
    if (Test-Path $CounterFile) {
        $loaded = [IO.File]::ReadAllText($CounterFile, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json
        if ($loaded.schemaVersion) { $state.schemaVersion = [int]$loaded.schemaVersion }
        if ($loaded.startValue) { $state.startValue = [int]$loaded.startValue }
        if ($loaded.years) {
            foreach ($prop in $loaded.years.PSObject.Properties) {
                $state.years[$prop.Name] = [int]$prop.Value
            }
        }
    }

    if (-not $state.years.Contains($yearKey)) {
        $state.years[$yearKey] = 0
    }

    $next = [int]$state.years[$yearKey] + 1
    if ($next -lt $StartValue) { $next = $StartValue }
    $state.years[$yearKey] = $next

    $json = ($state | ConvertTo-Json -Depth 4)
    [IO.File]::WriteAllText($CounterFile, $json, [Text.UTF8Encoding]::new($false))

    return "ODK-COC-$yearKey-$next"
}
