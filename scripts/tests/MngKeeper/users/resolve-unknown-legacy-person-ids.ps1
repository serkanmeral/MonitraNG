# Bilinmeyen legacy kisi ID'lerini Kalite MySQL'de cozumle
#
# Kullanim:
#   .\resolve-unknown-legacy-person-ids.ps1
#   .\resolve-unknown-legacy-person-ids.ps1 -LegacyIds 220,225,250

param(
    [string]$LegacyIds = "220,225,250",
    [string]$LegacyServer = "192.168.20.30",
    [string]$LegacySshUser = "odak",
    [string]$LegacySshPassword = "Odak333221",
    [string]$LegacyDbUser = "kalite_ro",
    [string]$LegacyDbPassword = "KaliteRo333221",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Get-LegacyArchiveReportsDir
}

$idList = @($LegacyIds -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' })
if (-not $idList.Count) { throw "Gecerli legacy ID yok." }

if (-not (Get-Module -ListAvailable Posh-SSH)) {
    throw "Posh-SSH gerekli: Install-Module Posh-SSH -Scope CurrentUser"
}
Import-Module Posh-SSH -ErrorAction Stop

function Invoke-LegacySql {
    param([string]$Sql)
    $escapedPw = $LegacyDbPassword.Replace("'", "'\\''")
    $escapedSql = $Sql -replace '"', '\"'
    $cmd = "mysql -u $LegacyDbUser -p'$escapedPw' kalite -N -B -e `"$escapedSql`""
    $result = Invoke-SSHCommand -SessionId $script:LegacySessionId -Command $cmd -TimeOut 90
    if ($result.ExitStatus -ne 0) {
        throw "Legacy MySQL hatasi: $($result.Error)"
    }
    return ($result.Output -join "`n").Trim()
}

Write-Host "=== Bilinmeyen legacy kisi ID cozumleme ===" -ForegroundColor Cyan
Write-Host "ID'ler: $($idList -join ', ')" -ForegroundColor Gray

$sec = ConvertTo-SecureString $LegacySshPassword -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($LegacySshUser, $sec)
$session = New-SSHSession -ComputerName $LegacyServer -Credential $cred -AcceptKey -ErrorAction Stop
$script:LegacySessionId = $session.SessionId

try {
    $inClause = ($idList -join ",")

    Write-Host "`n[1b] employees tablosu..." -ForegroundColor Yellow
    $empRaw = Invoke-LegacySql -Sql "SELECT id, name, surname, status FROM employees WHERE id IN ($inClause);"
    Write-Host $empRaw

    Write-Host "`n[1] users tablosu..." -ForegroundColor Yellow
    $usersRaw = Invoke-LegacySql -Sql "SELECT id, username, name, surname, status FROM users WHERE id IN ($inClause);"
    Write-Host $usersRaw

    Write-Host "`n[2] Tablo listesi (employ/person/staff/contact)..." -ForegroundColor Yellow
    $tablesRaw = Invoke-LegacySql -Sql "SHOW TABLES;"
    $tables = @($tablesRaw -split "`n" | Where-Object { $_ -match '(?i)employ|person|staff|contact|user' })
    Write-Host ($tables -join "`n")

    Write-Host "`n[3] packages referans dagilimi..." -ForegroundColor Yellow
    foreach ($id in $idList) {
        $pkgSql = @"
SELECT
  SUM(CASE WHEN responsible = $id THEN 1 ELSE 0 END) AS resp,
  SUM(CASE WHEN design_responsible = $id THEN 1 ELSE 0 END) AS design,
  SUM(CASE WHEN manufacture_responsible = $id THEN 1 ELSE 0 END) AS manuf
FROM packages;
"@
        $pkgRaw = Invoke-LegacySql -Sql $pkgSql
        Write-Host "  ID $id -> $pkgRaw"
    }

    Write-Host "`n[4] Ornek paketler (responsible=$($idList[0]))..." -ForegroundColor Yellow
    $sampleSql = "SELECT id, package_no, responsible, design_responsible, manufacture_responsible FROM packages WHERE responsible IN ($inClause) OR design_responsible IN ($inClause) OR manufacture_responsible IN ($inClause) LIMIT 5;"
    $sampleRaw = Invoke-LegacySql -Sql $sampleSql
    Write-Host $sampleRaw

    # JSON_ARRAYAGG users if any
    $employees = @()
    if ($empRaw) {
        foreach ($line in @($empRaw -split "`n")) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $p = $line -split "`t"
            if ($p.Count -ge 4) {
                $employees += [pscustomobject]@{
                    id      = $p[0]
                    name    = $p[1]
                    surname = $p[2]
                    status  = $p[3]
                }
            }
        }
    }

    $users = @()
    if ($usersRaw) {
        foreach ($line in @($usersRaw -split "`n")) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $p = $line -split "`t"
            if ($p.Count -ge 5) {
                $users += [pscustomobject]@{
                    id       = $p[0]
                    username = $p[1]
                    name     = $p[2]
                    surname  = $p[3]
                    status   = $p[4]
                }
            }
        }
    }

    $report = [ordered]@{
        generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        legacyIds     = $idList
        employees     = $employees
        users         = $users
        tables        = $tables
    }

    $libPath = Join-Path $LegacyArchiveRepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
    . $libPath
    $outPath = Join-Path $OutputDir "legacy-unknown-person-ids-resolved.json"
    Write-Utf8JsonFile -Path $outPath -Object $report -Depth 6
    Write-Host "`nRapor: $outPath" -ForegroundColor Green
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
