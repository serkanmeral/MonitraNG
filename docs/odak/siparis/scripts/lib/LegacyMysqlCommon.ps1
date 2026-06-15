# Shared helpers — native local Kalite MySQL (kalite-legacy-local)
# Dot-source: . (Join-Path $PSScriptRoot "lib/LegacyMysqlCommon.ps1")

function Get-LegacyMySqlExecutable {
    $native = Join-Path $env:USERPROFILE "kalite-legacy-local\mysql\bin\mysql.exe"
    if (Test-Path $native) { return $native }
    $cmd = Get-Command mysql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "mysql bulunamadi. Native: $native veya PATH'e mysql client ekleyin."
}

function Invoke-LegacyMySqlQuery {
    param(
        [Parameter(Mandatory = $true)][string]$Sql,
        [string]$Host = "127.0.0.1",
        [int]$Port = 3307,
        [string]$User = "root",
        [string]$Password = "",
        [string]$Database = "kalite"
    )
    $mysql = Get-LegacyMySqlExecutable
    $args = @(
        "-h", $Host,
        "-P", $Port,
        "-u", $User,
        $Database,
        "-N", "-B",
        "-e", $Sql
    )
    if ($Password) {
        $args = @("-h", $Host, "-P", $Port, "-u", $User, "-p$Password") + $args[4..($args.Length - 1)]
    }
    $raw = & $mysql @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "MySQL hatasi: $raw" }
    return $raw
}

function Convert-LegacyTsvRows {
    param(
        [string[]]$Lines,
        [string[]]$Columns
    )
    $rows = @()
    foreach ($line in $Lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        $obj = [ordered]@{}
        for ($i = 0; $i -lt $Columns.Count; $i++) {
            $val = if ($i -lt $parts.Count) { $parts[$i] } else { $null }
            if ($val -eq "NULL" -or $val -eq "\N") { $val = $null }
            $obj[$Columns[$i]] = $val
        }
        $rows += [pscustomobject]$obj
    }
    return $rows
}

function Escape-LegacySqlString {
    param([string]$Value)
    if ($null -eq $Value) { return $null }
    return ($Value -replace "'", "''")
}
