# Odak Egitim legacy -> DG migration helpers

function Get-LegacyEgitimDatasetsDir {
    param([string]$ScriptDir)
    return (Join-Path $ScriptDir "..\datasets")
}

function Get-LegacyEgitimExportPath {
    param([string]$ScriptDir, [string]$Override = "")
    if ($Override) { return $Override }
    return Join-Path (Get-LegacyEgitimDatasetsDir -ScriptDir $ScriptDir) "legacy-egitim-export.json"
}

function Get-LegacyEgitimDivisionMappingPath {
    param([string]$ScriptDir)
    return Join-Path (Get-LegacyEgitimDatasetsDir -ScriptDir $ScriptDir) "migration-division-mapping.json"
}

function Get-LegacyEgitimTrainingMappingPath {
    param([string]$ScriptDir)
    return Join-Path (Get-LegacyEgitimDatasetsDir -ScriptDir $ScriptDir) "migration-training-mapping.json"
}

function Get-LegacyEgitimPersonGapReportPath {
    param([string]$ScriptDir)
    return Join-Path (Get-LegacyEgitimDatasetsDir -ScriptDir $ScriptDir) "legacy-egitim-person-gap-report.json"
}

function Convert-LegacySqlDateTime {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    $text = $text.Trim()
    if ($text -match '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$') {
        return ($text -replace ' ', 'T')
    }
    if ($text -match '^\d{4}-\d{2}-\d{2}$') {
        return "${text}T00:00:00"
    }
    return $text
}

function Get-EgitimNoYear {
    param(
        [object]$GerceklesenTarih,
        [object]$PlanlananTarih,
        [object]$Created
    )
    foreach ($candidate in @($GerceklesenTarih, $PlanlananTarih, $Created)) {
        if ($null -eq $candidate -or [string]::IsNullOrWhiteSpace([string]$candidate)) { continue }
        $dt = [datetime]::MinValue
        if ([datetime]::TryParse([string]$candidate, [ref]$dt)) {
            return $dt.Year
        }
    }
    return (Get-Date).Year
}

function Build-EgitimNo {
    param(
        [string]$LegacyTrainingId,
        [object]$GerceklesenTarih,
        [object]$PlanlananTarih,
        [object]$Created
    )
    $year = Get-EgitimNoYear -GerceklesenTarih $GerceklesenTarih -PlanlananTarih $PlanlananTarih -Created $Created
    return "EGTM$year/$LegacyTrainingId"
}

function Resolve-LegacyTrainingDurum {
    param([object]$GerceklesenTarih)
    if ($null -ne $GerceklesenTarih -and -not [string]::IsNullOrWhiteSpace([string]$GerceklesenTarih)) {
        return "Tamamlandi"
    }
    return "Planlandi"
}

function Format-DivisionKod {
    param([string]$LegacyDivisionId)
    $num = [int]$LegacyDivisionId
    return "BRM-{0:D3}" -f $num
}

function Initialize-LegacyEgitimDgContext {
    param(
        [string]$RepoRoot,
        [string]$BaseUrl = "http://192.168.20.8:5040",
        [switch]$UseGateway = $true
    )
    $libPath = Join-Path $RepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
    . $libPath
    $env:MNG_OC_USE_PROD_TOKEN = "1"
    $tokenScript = Join-Path $RepoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    $ctx = Initialize-DgMigrationHeaders -TokenScriptPath $tokenScript
    $dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
    return @{
        AuthContext = $ctx
        BaseUrl     = $BaseUrl.TrimEnd("/")
        DataPath    = $dataPath
        InvokeDg    = {
            param([string]$Method, [string]$Uri, [object]$Body = $null)
            Invoke-DgMigrationApi -AuthContext $ctx -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
        }.GetNewClosure()
    }
}

function Get-DgMigrationDataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return [string]$id
}

function Get-DgMigrationItems {
    param($Response)
    if ($Response -is [Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    if ($Response.__dataId -or $Response.dataId) { return @($Response) }
    return @()
}

function Load-EmployeeKeeperMapFromGapReport {
    param([string]$GapReportPath)
    if (-not (Test-Path $GapReportPath)) {
        throw "Gap raporu yok: $GapReportPath — once analyze-legacy-egitim-person-gaps.ps1"
    }
    $report = Get-Content $GapReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $map = @{}
    foreach ($row in @($report.employees)) {
        $empId = [string]$row.employeeId
        $keeperId = [string]$row.keeperUserId
        if ($empId -and $keeperId) { $map[$empId] = $keeperId }
    }
    return $map
}

function Test-LegacyBoolField {
    param([object]$Value, [switch]$DefaultTrue)
    if ($null -eq $Value) { return [bool]$DefaultTrue }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return [bool]$DefaultTrue }
    if ($text -match '^(?i)(1|true|yes|evet)$') { return $true }
    if ($text -match '^(?i)(0|false|no|hayir)$') { return $false }
    return [bool]$DefaultTrue
}
