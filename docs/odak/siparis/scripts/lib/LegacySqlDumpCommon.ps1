# Shared SQL dump parsing — kalite-legacy-docker/db/init/01-kalite.sql

function Get-LegacySqlDumpPath {
    param([string]$SqlDumpPath = "")
    if ([string]::IsNullOrEmpty($SqlDumpPath)) {
        $SqlDumpPath = Join-Path $env:USERPROFILE "kalite-legacy-docker\db\init\01-kalite.sql"
    }
    if (-not (Test-Path $SqlDumpPath)) {
        throw "SQL dump bulunamadi: $SqlDumpPath"
    }
    return (Resolve-Path $SqlDumpPath).Path
}

function Split-SqlTuples {
    param([string]$Body)
    $tuples = [System.Collections.Generic.List[string]]::new()
    $depth = 0
    $start = -1
    $inString = $false
    for ($i = 0; $i -lt $Body.Length; $i++) {
        $c = $Body[$i]
        if ($inString) {
            if ($c -eq '\') {
                if ($i + 1 -lt $Body.Length) { $i++ }
                continue
            }
            if ($c -eq "'") {
                if ($i + 1 -lt $Body.Length -and $Body[$i + 1] -eq "'") {
                    $i++
                    continue
                }
                $inString = $false
            }
            continue
        }
        if ($c -eq "'") {
            $inString = $true
            continue
        }

        if ($c -eq '(') {
            if ($depth -eq 0) { $start = $i }
            $depth++
        }
        elseif ($c -eq ')') {
            $depth--
            if ($depth -eq 0 -and $start -ge 0) {
                $tuples.Add($Body.Substring($start, $i - $start + 1))
                $start = -1
            }
        }
    }
    return $tuples
}

function Split-SqlFields {
    param([string]$Inner)
    if ([string]::IsNullOrWhiteSpace($Inner)) { return @() }
    $fields = [System.Collections.Generic.List[string]]::new()
    $sb = New-Object System.Text.StringBuilder
    $inString = $false
    for ($i = 0; $i -lt $Inner.Length; $i++) {
        $c = $Inner[$i]
        if ($inString) {
            [void]$sb.Append($c)
            if ($c -eq '\') {
                if ($i + 1 -lt $Inner.Length) {
                    $i++
                    [void]$sb.Append($Inner[$i])
                }
                continue
            }
            if ($c -eq "'") {
                if ($i + 1 -lt $Inner.Length -and $Inner[$i + 1] -eq "'") {
                    $i++
                    [void]$sb.Append("'")
                    continue
                }
                $inString = $false
            }
            continue
        }
        if ($c -eq "'") {
            $inString = $true
            [void]$sb.Append($c)
            continue
        }
        if ($c -eq ',') {
            $fields.Add($sb.ToString())
            $sb.Clear() | Out-Null
            continue
        }
        [void]$sb.Append($c)
    }
    if ($sb.Length -gt 0 -or $fields.Count -gt 0) {
        $fields.Add($sb.ToString())
    }
    return @($fields)
}

function Parse-SqlValue {
    param([string]$Raw)
    if ($null -eq $Raw) { return $null }
    $s = $Raw.Trim()
    if ($s -eq 'NULL') { return $null }
    if ($s.StartsWith("'") -and $s.EndsWith("'")) {
        $inner = $s.Substring(1, $s.Length - 2)
        $inner = $inner.Replace("''", "'")
        # MySQL dump backslash escapes (e.g. \' inside strings)
        $inner = $inner -replace "\\'", "'"
        $inner = $inner -replace '\\r\\n', "`r`n"
        $inner = $inner -replace '\\n', "`n"
        $inner = $inner -replace '\\r', "`r"
        $inner = $inner -replace '\\t', "`t"
        return $inner
    }
    return $s
}

function Get-InsertBody {
    param(
        [string]$Path,
        [string]$TableName
    )
    $prefix = "INSERT INTO ``$TableName`` VALUES "
    $reader = [System.IO.StreamReader]::new($Path, [System.Text.Encoding]::UTF8, $true)
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.StartsWith($prefix)) {
                return $line.Substring($prefix.Length)
            }
        }
    }
    finally {
        $reader.Close()
    }
    return $null
}

function Get-SqlTableFieldCount {
    param([string]$TableName)
    switch ($TableName) {
        "firms" { return 19 }
        "packages" { return 27 }
        "packageitems" { return 23 }
        "contacts" { return 14 }
        "divisions" { return 2 }
        "trainings" { return 16 }
        "employees" { return 21 }
        "employees_trainings" { return 5 }
        "users" { return 18 }
        default { return 0 }
    }
}

function Read-SqlInsertRows {
    param(
        [string]$Path,
        [string]$TableName
    )
    $body = Get-InsertBody -Path $Path -TableName $TableName
    if (-not $body) { return @() }
    Write-Host "  Parse: $TableName ..." -ForegroundColor Gray

    $expectedFields = Get-SqlTableFieldCount -TableName $TableName
    $rows = @()
    $tuples = Split-SqlTuples -Body $body

    foreach ($tuple in $tuples) {
        $inner = $tuple.Trim().Trim("()")
        if ([string]::IsNullOrWhiteSpace($inner)) { continue }
        $fields = @(Split-SqlFields $inner | ForEach-Object { Parse-SqlValue $_ })
        if ($expectedFields -gt 0 -and $fields.Count -ge $expectedFields) {
            $rows += ,@($fields[0..($expectedFields - 1)])
        }
        elseif ($expectedFields -le 0 -and $fields.Count -gt 0) {
            $rows += ,@($fields)
        }
    }
    return $rows
}
