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
        if ($c -eq "'" -and -not $inString) {
            $inString = $true
            continue
        }
        if ($c -eq "'" -and $inString) {
            if ($i + 1 -lt $Body.Length -and $Body[$i + 1] -eq "'") {
                $i++
                continue
            }
            $inString = $false
            continue
        }
        if ($inString) { continue }

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
    return [regex]::Split($Inner, ",(?=(?:[^']*'[^']*')*[^']*$)")
}

function Parse-SqlValue {
    param([string]$Raw)
    if ($null -eq $Raw) { return $null }
    $s = $Raw.Trim()
    if ($s -eq 'NULL') { return $null }
    if ($s.StartsWith("'") -and $s.EndsWith("'")) {
        return $s.Substring(1, $s.Length - 2).Replace("''", "'")
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
    $parts = $body -split '\),\('
    $rows = @()

    for ($i = 0; $i -lt $parts.Count; $i++) {
        $j = $i
        $inner = $parts[$j].Trim().Trim("()")
        while ($true) {
            $fields = @(Split-SqlFields $inner | ForEach-Object { Parse-SqlValue $_ })
            if ($expectedFields -le 0 -or $fields.Count -ge $expectedFields -or $j -ge ($parts.Count - 1)) {
                break
            }
            $j++
            $inner = $inner + "),(" + $parts[$j].Trim().Trim("()")
        }

        if ($expectedFields -gt 0 -and $fields.Count -ge $expectedFields) {
            $rows += ,@($fields[0..($expectedFields - 1)])
        }
        elseif ($expectedFields -le 0 -and $fields.Count -gt 0) {
            $rows += ,@($fields)
        }

        $i = $j
    }
    return $rows
}
