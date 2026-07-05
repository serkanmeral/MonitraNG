# Legacy Kalite contacts -> JSON export (SQL dump)
#
# Usage:
#   .\export-legacy-contacts-from-sql.ps1
#   .\export-legacy-contacts-from-sql.ps1 -OutputFile .\datasets\legacy-contacts.json

param(
    [string]$SqlDumpPath = "",
    [string]$OutputFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$SqlDumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
if ([string]::IsNullOrEmpty($OutputFile)) {
    $OutputFile = Join-Path $scriptDir "..\datasets\legacy-contacts.json"
}

Write-Host "`n=== export-legacy-contacts-from-sql ===" -ForegroundColor Cyan
Write-Host "SQL dump: $SqlDumpPath" -ForegroundColor Gray

$rows = Read-SqlInsertRows -Path $SqlDumpPath -TableName "contacts"
$contacts = @()
foreach ($fields in $rows) {
    if ($fields.Count -lt 8) { continue }
    $name = Limit-LegacyText $fields[2] 120
    $surname = Limit-LegacyText $fields[3] 120
    $ad = "$name $surname".Trim()
    if ([string]::IsNullOrWhiteSpace($ad)) { continue }
    $email = Limit-LegacyText $fields[7] 200
    if ([string]::IsNullOrWhiteSpace($email)) {
        $email = "legacy-contact-$($fields[0])@odak.local"
    }
    $contacts += [pscustomobject]@{
        id        = [string]$fields[0]
        firm_id   = [string]$fields[1]
        ad        = $ad
        position  = Limit-LegacyText $fields[4] 120
        tel       = Limit-LegacyText $fields[5] 40
        email     = $email
    }
}

$export = @{
    exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    count      = $contacts.Count
    contacts   = @($contacts)
    source     = @{
        engine  = "sql-dump"
        sqlDump = $SqlDumpPath
    }
}

Write-Utf8JsonFile -Path $OutputFile -Object $export -Depth 6
Write-Host "OK: $($contacts.Count) contact -> $OutputFile" -ForegroundColor Green
