# Shared helpers — native local Kalite MySQL (kalite-legacy-local)
# Dot-source: . (Join-Path $PSScriptRoot "lib/LegacyMysqlCommon.ps1")

function Get-LegacyMySqlExecutable {
    $base = Join-Path $env:USERPROFILE "kalite-legacy-local"
    $candidates = @(
        (Join-Path $base "mysql\mysql-8.0.39-winx64\bin\mysql.exe"),
        (Join-Path $base "mysql\bin\mysql.exe")
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    $cmd = Get-Command mysql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "mysql bulunamadi. Native: $($candidates[0]) veya PATH'e mysql client ekleyin."
}

function Invoke-LegacyMySqlQuery {
    param(
        [Parameter(Mandatory = $true)][string]$Sql,
        [string]$MySqlHost = "127.0.0.1",
        [int]$Port = 3307,
        [string]$User = "root",
        [string]$Password = "",
        [string]$Database = "kalite",
        [switch]$BatchMode
    )
    $mysql = Get-LegacyMySqlExecutable
    $args = @(
        "-h", $MySqlHost,
        "-P", $Port,
        "-u", $User,
        "--default-character-set=utf8mb4",
        $Database,
        "-N"
    )
    if ($BatchMode) { $args += "-B" }
    $args += "-e", $Sql
    if ($Password) {
        $args = @("-h", $MySqlHost, "-P", $Port, "-u", $User, "-p$Password") + $args[6..($args.Length - 1)]
    }
    $raw = & $mysql @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "MySQL hatasi: $raw" }
    return $raw
}

function Invoke-LegacyMySqlJsonRows {
    param(
        [Parameter(Mandatory = $true)][string]$Sql,
        [string]$MySqlHost = "127.0.0.1",
        [int]$Port = 3307,
        [string]$User = "root",
        [string]$Password = "",
        [string]$Database = "kalite"
    )
    $mysql = Get-LegacyMySqlExecutable
    $tmp = [IO.Path]::GetTempFileName()
    $oneLineSql = ($Sql -replace '\s+', ' ').Trim()
    try {
        $cmdLine = "`"$mysql`" -h $MySqlHost -P $Port -u $User --default-character-set=utf8mb4 $Database -N -B -r -e `"$oneLineSql`""
        cmd /c "$cmdLine > `"$tmp`" 2>&1"
        if ($LASTEXITCODE -ne 0) {
            $err = [IO.File]::ReadAllText($tmp)
            throw "MySQL hatasi (exit $LASTEXITCODE): $($err.Substring(0, [Math]::Min(500, $err.Length)))"
        }
        $head = [IO.File]::ReadAllText($tmp)
        if ($head.StartsWith("mysql") -and $head.Contains("Usage:")) {
            throw "MySQL arguman hatasi: $($head.Substring(0, [Math]::Min(500, $head.Length)))"
        }
        $lines = [IO.File]::ReadAllLines($tmp, [Text.Encoding]::UTF8)
    }
    finally {
        Remove-Item $tmp -ErrorAction SilentlyContinue
    }

    $rows = @()
    $lineNo = 0
    foreach ($line in $lines) {
        $lineNo++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $rows += ($line | ConvertFrom-Json)
        }
        catch {
            throw "JSON satiri parse edilemedi (satir $lineNo): $($line.Substring(0, [Math]::Min(120, $line.Length)))..."
        }
    }
    return $rows
}

function Write-Utf8JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Object,
        [int]$Depth = 8
    )
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $json = $Object | ConvertTo-Json -Depth $Depth
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
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
