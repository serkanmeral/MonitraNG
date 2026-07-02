# Legacy Kalite (MySQL) vs MngKeeper kullanici karsilastirma raporu
#
# Eslestirme: username (birincil) veya ad+soyad (ikincil, tek aday varsa)
#
# Kullanim:
#   .\scripts\tests\MngKeeper\users\compare-legacy-kalite-users.ps1
#   .\scripts\tests\MngKeeper\users\compare-legacy-kalite-users.ps1 -KeeperBaseUrl "http://192.168.20.8:5040"

param(
    [string]$LegacyServer = "192.168.20.30",
    [string]$LegacySshUser = "odak",
    [string]$LegacySshPassword = "Odak333221",
    [string]$LegacyDbUser = "kalite_ro",
    [string]$LegacyDbPassword = "KaliteRo333221",
    [string]$KeeperBaseUrl = "http://192.168.20.8:5040",
    [string]$KeeperPath = "/keeper/api",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt",
    [string]$OutputDir = "",
    [switch]$SkipLegacyFetch,
    [switch]$SkipKeeperFetch
)

$ErrorActionPreference = "Stop"

function Normalize-Username {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return $Value.Trim().ToLowerInvariant()
}

function Fold-AsciiTurkish {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    $normalized = $Value.ToLowerInvariant()
    $normalized = $normalized -replace '\s+', ' '
    foreach ($pair in @(
        @([char]0x00E7, 'c'), @([char]0x011F, 'g'), @([char]0x0131, 'i'), @([char]0x00F6, 'o'),
        @([char]0x015F, 's'), @([char]0x00FC, 'u'), @([char]0x00C7, 'c'), @([char]0x011E, 'g'),
        @([char]0x0130, 'i'), @([char]0x00D6, 'o'), @([char]0x015E, 's'), @([char]0x00DC, 'u')
    )) {
        $normalized = $normalized.Replace([string]$pair[0], [string]$pair[1])
    }
    return $normalized.Trim()
}

function Normalize-NameKey {
    param([string]$First, [string]$Last)
    return (Fold-AsciiTurkish "$First $Last".Trim())
}

function Get-NameWordParts {
    param([string]$First, [string]$Last)
    $full = Fold-AsciiTurkish "$First $Last".Trim()
    if ([string]::IsNullOrWhiteSpace($full)) {
        return @{ Full = ""; First = ""; Last = ""; FirstLastKey = "" }
    }
    $words = @($full -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $first = if ($words.Count -gt 0) { $words[0] } else { "" }
    $last = if ($words.Count -gt 1) { $words[$words.Count - 1] } else { "" }
    $firstLastKey = if ($first -and $last) { "$first|$last" } else { "" }
    return @{
        Full          = $full
        First         = $first
        Last          = $last
        FirstLastKey  = $firstLastKey
    }
}

function Test-LikelySamAccountName {
    param([string]$Username)
    $u = Normalize-Username $Username
    if ([string]::IsNullOrWhiteSpace($u)) { return $false }
    if ($u -match '\s') { return $false }
    if ($u -match '\$$') { return $false }
    if ($u -match '^(administrator|admin|dummy|cnc|erp-)') { return $false }
    return $true
}

function Select-BestKeeperCandidate {
    param([array]$Candidates)
    if ($Candidates.Count -eq 1) { return $Candidates[0] }
    $scored = foreach ($c in $Candidates) {
        $score = 0
        if ($c.isActive) { $score += 4 }
        if (Test-LikelySamAccountName $c.username) { $score += 8 }
        if ($c.provisioningSource -eq 'Directory') { $score += 1 }
        [pscustomobject]@{ User = $c; Score = $score }
    }
    $best = @($scored | Sort-Object Score -Descending)[0]
    $same = @($scored | Where-Object { $_.Score -eq $best.Score })
    if ($same.Count -gt 1) { return $null }
    return $best.User
}

function Get-LegacyUsers {
    if (-not (Get-Module -ListAvailable Posh-SSH)) {
        throw "Posh-SSH gerekli: Install-Module Posh-SSH -Scope CurrentUser"
    }
    Import-Module Posh-SSH -ErrorAction Stop

    $sec = ConvertTo-SecureString $LegacySshPassword -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential($LegacySshUser, $sec)
    $session = New-SSHSession -ComputerName $LegacyServer -Credential $cred -AcceptKey -ErrorAction Stop
    try {
        $sql = @"
SELECT JSON_ARRAYAGG(
  JSON_OBJECT(
    'id', u.id,
    'username', IFNULL(u.username, ''),
    'email', IFNULL(u.email, ''),
    'name', IFNULL(u.name, ''),
    'surname', IFNULL(u.surname, ''),
    'active', IFNULL(u.status, 0)
  )
)
FROM users u;
"@
        $escapedPw = $LegacyDbPassword.Replace("'", "'\\''")
        $cmd = "mysql -u $LegacyDbUser -p'$escapedPw' kalite -N -B -e `"$($sql -replace '"','\\"')`""
        $result = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd -TimeOut 60
        if ($result.ExitStatus -ne 0) {
            throw "Legacy MySQL hatasi: $($result.Error)"
        }
        $json = ($result.Output -join "").Trim()
        if ([string]::IsNullOrWhiteSpace($json) -or $json -eq "NULL") {
            return @()
        }
        return @($json | ConvertFrom-Json)
    }
    finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }
}

function Get-KeeperToken {
    if (-not (Test-Path $TokenFile)) {
        $tokenScript = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path "docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1"
        if (-not (Test-Path $tokenScript)) { throw "Token dosyasi yok ve prod token script bulunamadi: $TokenFile" }
        & $tokenScript
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) { throw "Prod token alinamadi." }
    }
    $token = (Get-Content $TokenFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Token dosyasi bos: $TokenFile" }
    return $token
}

function Get-KeeperUsers {
    param([string]$Token)
    $headers = @{
        Authorization = "Bearer $Token"
        Accept        = "application/json"
    }
    $all = New-Object System.Collections.Generic.List[object]
    $page = 1
    $pageSize = 200
    do {
        $uri = "$KeeperBaseUrl$KeeperPath/User?page=$page&pageSize=$pageSize&sortBy=username&sortOrder=asc"
        $resp = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -SkipCertificateCheck
        if (-not $resp.isSuccess) { throw "Keeper API hatasi: $($resp.errorMessage)" }
        foreach ($u in @($resp.users)) { [void]$all.Add($u) }
        $totalPages = [int]$resp.totalPages
        if ($totalPages -le 0) { $totalPages = 1 }
        $page++
    } while ($page -le $totalPages)
    return $all
}

function Compare-UserSets {
    param($LegacyUsers, $KeeperUsers)

    $keeperByUsername = @{}
    $keeperByFullName = @{}
    $keeperByFirstLast = @{}
    foreach ($k in $KeeperUsers) {
        $uKey = Normalize-Username $k.username
        if ($uKey) {
            if (-not $keeperByUsername.ContainsKey($uKey)) { $keeperByUsername[$uKey] = @() }
            $keeperByUsername[$uKey] = $keeperByUsername[$uKey] + @($k)
        }
        $parts = Get-NameWordParts $k.firstName $k.lastName
        if ($parts.Full) {
            if (-not $keeperByFullName.ContainsKey($parts.Full)) { $keeperByFullName[$parts.Full] = @() }
            $keeperByFullName[$parts.Full] = $keeperByFullName[$parts.Full] + @($k)
        }
        if ($parts.FirstLastKey) {
            if (-not $keeperByFirstLast.ContainsKey($parts.FirstLastKey)) { $keeperByFirstLast[$parts.FirstLastKey] = @() }
            $keeperByFirstLast[$parts.FirstLastKey] = $keeperByFirstLast[$parts.FirstLastKey] + @($k)
        }
        # AD'de bazen username = tam ad (or. "ahmet emin gezer")
        if ($uKey -and ($k.username -match '\s')) {
            $fromUsername = Fold-AsciiTurkish $k.username
            if ($fromUsername) {
                if (-not $keeperByFullName.ContainsKey($fromUsername)) { $keeperByFullName[$fromUsername] = @() }
                $keeperByFullName[$fromUsername] = @($keeperByFullName[$fromUsername] + @($k) | Select-Object -Unique userId)
            }
        }
    }

    $matched = New-Object System.Collections.Generic.List[object]
    $legacyOnly = New-Object System.Collections.Generic.List[object]
    $ambiguous = New-Object System.Collections.Generic.List[object]
    $matchedKeeperIds = New-Object System.Collections.Generic.HashSet[string]

    foreach ($l in $LegacyUsers) {
        $legacyUsername = Normalize-Username $l.username
        $legacyParts = Get-NameWordParts $l.name $l.surname
        $legacyDisplayName = "$($l.name) $($l.surname)".Trim()
        $match = $null
        $matchType = $null

        if ($legacyUsername -and $keeperByUsername.ContainsKey($legacyUsername)) {
            $candidates = @($keeperByUsername[$legacyUsername])
            $match = Select-BestKeeperCandidate $candidates
            if ($match) { $matchType = "username" }
            elseif ($candidates.Count -gt 1) {
                [void]$ambiguous.Add([ordered]@{
                    legacyId = $l.id; legacyUsername = $l.username; legacyName = $legacyDisplayName
                    legacyActive = ($l.active -eq 1 -or $l.active -eq $true); reason = "multiple_keeper_username"; keeperCount = $candidates.Count
                })
                continue
            }
        }

        if (-not $match -and $legacyParts.Full -and $keeperByFullName.ContainsKey($legacyParts.Full)) {
            $candidates = @($keeperByFullName[$legacyParts.Full] | Where-Object { -not $matchedKeeperIds.Contains([string]$_.userId) })
            if ($candidates.Count -gt 0) {
                $match = Select-BestKeeperCandidate $candidates
                if ($match) { $matchType = "name_exact" }
                elseif ($candidates.Count -gt 1) {
                    [void]$ambiguous.Add([ordered]@{
                        legacyId = $l.id; legacyUsername = $l.username; legacyName = $legacyDisplayName
                        legacyActive = ($l.active -eq 1 -or $l.active -eq $true); reason = "multiple_keeper_name_exact"; keeperCount = $candidates.Count
                        nameKey = $legacyParts.Full
                    })
                    continue
                }
            }
        }

        if (-not $match -and $legacyParts.FirstLastKey -and $keeperByFirstLast.ContainsKey($legacyParts.FirstLastKey)) {
            $candidates = @($keeperByFirstLast[$legacyParts.FirstLastKey] | Where-Object { -not $matchedKeeperIds.Contains([string]$_.userId) })
            if ($candidates.Count -gt 0) {
                $match = Select-BestKeeperCandidate $candidates
                if ($match) { $matchType = "name_first_last" }
                elseif ($candidates.Count -gt 1) {
                    [void]$ambiguous.Add([ordered]@{
                        legacyId = $l.id; legacyUsername = $l.username; legacyName = $legacyDisplayName
                        legacyActive = ($l.active -eq 1 -or $l.active -eq $true); reason = "multiple_keeper_name_first_last"; keeperCount = $candidates.Count
                        nameKey = $legacyParts.FirstLastKey
                    })
                    continue
                }
            }
        }

        if ($match) {
            [void]$matchedKeeperIds.Add([string]$match.userId)
            $keeperParts = Get-NameWordParts $match.firstName $match.lastName
            [void]$matched.Add([ordered]@{
                matchType      = $matchType
                legacyId       = $l.id
                legacyUsername = $l.username
                legacyName     = $legacyDisplayName
                legacyActive   = [bool]$l.active
                keeperUserId   = $match.userId
                keeperUsername = $match.username
                keeperName     = "$($match.firstName) $($match.lastName)".Trim()
                keeperActive   = [bool]$match.isActive
                keeperSource   = $match.provisioningSource
                usernameMatch  = ($legacyUsername -eq (Normalize-Username $match.username))
                nameExactMatch = ($legacyParts.Full -eq $keeperParts.Full)
                nameFirstLastMatch = ($legacyParts.FirstLastKey -and $legacyParts.FirstLastKey -eq $keeperParts.FirstLastKey)
            })
        }
        else {
            [void]$legacyOnly.Add([ordered]@{
                legacyId = $l.id; legacyUsername = $l.username; legacyName = $legacyDisplayName
                legacyActive = ($l.active -eq 1 -or $l.active -eq $true)
            })
        }
    }

    $keeperOnly = New-Object System.Collections.Generic.List[object]
    foreach ($k in $KeeperUsers) {
        if (-not $matchedKeeperIds.Contains([string]$k.userId)) {
            [void]$keeperOnly.Add([ordered]@{
                keeperUserId = $k.userId; keeperUsername = $k.username
                keeperName = "$($k.firstName) $($k.lastName)".Trim()
                keeperActive = [bool]$k.isActive; keeperSource = $k.provisioningSource
                likelyDuplicate = (-not (Test-LikelySamAccountName $k.username))
            })
        }
    }

    $legacyOnlyActive = @($legacyOnly | Where-Object { $_.legacyActive }).Count
    $keeperOnlyActive = @($keeperOnly | Where-Object { $_.keeperActive }).Count
    $keeperOnlyDuplicate = @($keeperOnly | Where-Object { $_.likelyDuplicate }).Count

    return [pscustomobject]@{
        summary = [ordered]@{
            legacyTotal           = @($LegacyUsers).Count
            keeperTotal           = @($KeeperUsers).Count
            matched               = $matched.Count
            matchedByUsername     = @($matched | Where-Object { $_.matchType -eq "username" }).Count
            matchedByNameExact    = @($matched | Where-Object { $_.matchType -eq "name_exact" }).Count
            matchedByNameFirstLast = @($matched | Where-Object { $_.matchType -eq "name_first_last" }).Count
            legacyOnly            = $legacyOnly.Count
            legacyOnlyActive      = $legacyOnlyActive
            legacyOnlyInactive    = ($legacyOnly.Count - $legacyOnlyActive)
            keeperOnly            = $keeperOnly.Count
            keeperOnlyActive      = $keeperOnlyActive
            keeperOnlyLikelyDuplicate = $keeperOnlyDuplicate
            ambiguous             = $ambiguous.Count
            generatedAt           = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
            legacyServer          = $LegacyServer
            keeperBaseUrl         = $KeeperBaseUrl
        }
        matched     = @($matched | ForEach-Object { $_ })
        legacyOnly  = @($legacyOnly | ForEach-Object { $_ })
        keeperOnly  = @($keeperOnly | ForEach-Object { $_ })
        ambiguous   = @($ambiguous | ForEach-Object { $_ })
    }
}

function Escape-MdCell {
    param([string]$Value)
    if ($null -eq $Value) { return "" }
    return ([string]$Value).Replace('|', '\|')
}

function Format-ActiveBadge {
    param([bool]$Active)
    if ($Active) { return "Aktif" }
    return "Pasif"
}

function Write-UserCompareMarkdownReport {
    param($Report, [string]$Path)

    $s = $Report.summary
    $lines = New-Object System.Collections.Generic.List[string]

    [void]$lines.Add("# Legacy Kalite vs MngKeeper — Kullanici Karsilastirma Raporu")
    [void]$lines.Add("")
    [void]$lines.Add("**Olusturulma:** $($s.generatedAt)  ")
    [void]$lines.Add("**Legacy kaynak:** ``$($s.legacyServer)`` · MySQL ``kalite.users``  ")
    [void]$lines.Add("**Keeper kaynak:** ``$($s.keeperBaseUrl)`` · prod domain ``odak``")
    [void]$lines.Add("")
    [void]$lines.Add("> E-posta eslestirmesi **yapilmadi** (kullanici talebi). Eslestirme: username, tam ad, ad+soyad (ilk/son kelime).")
    [void]$lines.Add("")
    [void]$lines.Add("---")
    [void]$lines.Add("")
    [void]$lines.Add("## Ozet")
    [void]$lines.Add("")
    [void]$lines.Add("| Metrik | Deger |")
    [void]$lines.Add("|--------|------:|")
    [void]$lines.Add("| Legacy Kalite kullanicisi | $($s.legacyTotal) |")
    [void]$lines.Add("| Prod Keeper kullanicisi | $($s.keeperTotal) |")
    [void]$lines.Add("| **Toplam eslesen** | **$($s.matched)** |")
    [void]$lines.Add("| — username ile | $($s.matchedByUsername) |")
    [void]$lines.Add("| — tam ad ile | $($s.matchedByNameExact) |")
    [void]$lines.Add("| — ad + soyad (ilk/son kelime) ile | $($s.matchedByNameFirstLast) |")
    [void]$lines.Add("| Sadece Legacy'de | $($s.legacyOnly) (aktif: $($s.legacyOnlyActive), pasif: $($s.legacyOnlyInactive)) |")
    [void]$lines.Add("| Sadece Keeper'da | $($s.keeperOnly) (aktif: $($s.keeperOnlyActive); muhtemel AD duplicate: $($s.keeperOnlyLikelyDuplicate)) |")
    [void]$lines.Add("| Belirsiz (coklu aday) | $($s.ambiguous) |")
    [void]$lines.Add("")
    [void]$lines.Add("### Eslestirme kurallari")
    [void]$lines.Add("")
    [void]$lines.Add("1. **username** — normalize edilmis kullanici adi (birincil)")
    [void]$lines.Add("2. **name_exact** — ad+soyad tam metin (Turkce karakter toleransli)")
    [void]$lines.Add("3. **name_first_last** — ilk ad + son soyad kelimesi (or. ``Murat`` + ``Kucuk``; ikinci ad / evlilik soyadi farklarini tolere eder)")
    [void]$lines.Add("4. Coklu adayda: aktif + sAMAccountName benzeri username tercih edilir")
    [void]$lines.Add("")
    [void]$lines.Add("---")
    [void]$lines.Add("")
    [void]$lines.Add("## Eslesen kullanicilar ($($s.matched))")
    [void]$lines.Add("")
    [void]$lines.Add("| Eslestirme | Legacy kullanici | Legacy ad | L. durum | Keeper kullanici | Keeper ad | K. durum | Kaynak |")
    [void]$lines.Add("|------------|------------------|-----------|----------|------------------|-----------|----------|--------|")
    foreach ($m in @($Report.matched | Sort-Object legacyUsername, legacyId)) {
        [void]$lines.Add("| $($m.matchType) | $(Escape-MdCell $m.legacyUsername) | $(Escape-MdCell $m.legacyName) | $(Format-ActiveBadge $m.legacyActive) | $(Escape-MdCell $m.keeperUsername) | $(Escape-MdCell $m.keeperName) | $(Format-ActiveBadge $m.keeperActive) | $($m.keeperSource) |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("---")
    [void]$lines.Add("")
    [void]$lines.Add("## Sadece Legacy Kalite'de ($($s.legacyOnly))")
    [void]$lines.Add("")
    [void]$lines.Add("Keeper'da karsiligi bulunamayan hesaplar. Pasif kayitlar buyuk olasilikla ayrilmis personel.")
    [void]$lines.Add("")
    [void]$lines.Add("### Aktif ($($s.legacyOnlyActive))")
    [void]$lines.Add("")
    [void]$lines.Add("| Legacy kullanici | Ad soyad |")
    [void]$lines.Add("|------------------|----------|")
    foreach ($l in @($Report.legacyOnly | Where-Object { $_.legacyActive } | Sort-Object legacyUsername)) {
        [void]$lines.Add("| $(Escape-MdCell $l.legacyUsername) | $(Escape-MdCell $l.legacyName) |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("### Pasif ($($s.legacyOnlyInactive))")
    [void]$lines.Add("")
    [void]$lines.Add("<details>")
    [void]$lines.Add("<summary>Pasif legacy kullanicilar ($($s.legacyOnlyInactive) kayit)</summary>")
    [void]$lines.Add("")
    [void]$lines.Add("| Legacy kullanici | Ad soyad |")
    [void]$lines.Add("|------------------|----------|")
    foreach ($l in @($Report.legacyOnly | Where-Object { -not $_.legacyActive } | Sort-Object legacyUsername)) {
        [void]$lines.Add("| $(Escape-MdCell $l.legacyUsername) | $(Escape-MdCell $l.legacyName) |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("</details>")
    [void]$lines.Add("")
    [void]$lines.Add("---")
    [void]$lines.Add("")
    [void]$lines.Add("## Sadece Keeper'da ($($s.keeperOnly))")
    [void]$lines.Add("")
    [void]$lines.Add("Legacy'de karsiligi yok veya username/ad eslesmedi. ``AD duplicate`` = username bosluk iceriyor (CN kaydi); ayni kisi icin sAMAccountName kaydi ayrica olabilir.")
    [void]$lines.Add("")
    [void]$lines.Add("### Aktif — muhtemel gercek kullanici / servis ($($s.keeperOnlyActive))")
    [void]$lines.Add("")
    [void]$lines.Add("| Keeper kullanici | Ad soyad | Kaynak | Not |")
    [void]$lines.Add("|------------------|----------|--------|-----|")
    foreach ($k in @($Report.keeperOnly | Where-Object { $_.keeperActive } | Sort-Object keeperUsername)) {
        $note = if ($k.likelyDuplicate) { "AD duplicate?" } else { "" }
        [void]$lines.Add("| $(Escape-MdCell $k.keeperUsername) | $(Escape-MdCell $k.keeperName) | $($k.keeperSource) | $note |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("### Pasif")
    [void]$lines.Add("")
    [void]$lines.Add("<details>")
    [void]$lines.Add("<summary>Pasif Keeper kullanicilar</summary>")
    [void]$lines.Add("")
    [void]$lines.Add("| Keeper kullanici | Ad soyad | Kaynak | Not |")
    [void]$lines.Add("|------------------|----------|--------|-----|")
    foreach ($k in @($Report.keeperOnly | Where-Object { -not $_.keeperActive } | Sort-Object keeperUsername)) {
        $note = if ($k.likelyDuplicate) { "AD duplicate?" } else { "" }
        [void]$lines.Add("| $(Escape-MdCell $k.keeperUsername) | $(Escape-MdCell $k.keeperName) | $($k.keeperSource) | $note |")
    }
    [void]$lines.Add("")
    [void]$lines.Add("</details>")
    [void]$lines.Add("")
    $ambiguousList = @($Report.ambiguous | ForEach-Object { $_ })
    if ($ambiguousList.Count -gt 0) {
        [void]$lines.Add("---")
        [void]$lines.Add("")
        [void]$lines.Add("## Belirsiz eslestirmeler ($($s.ambiguous))")
        [void]$lines.Add("")
        [void]$lines.Add("| Legacy kullanici | Ad soyad | Neden | Aday sayisi |")
        [void]$lines.Add("|------------------|----------|-------|------------:|")
        foreach ($a in $ambiguousList) {
            [void]$lines.Add("| $(Escape-MdCell $a.legacyUsername) | $(Escape-MdCell $a.legacyName) | $($a.reason) | $($a.keeperCount) |")
        }
        [void]$lines.Add("")
    }
    [void]$lines.Add("---")
    [void]$lines.Add("")
    [void]$lines.Add("## Degerlendirme — olasi sonraki adimlar")
    [void]$lines.Add("")
    [void]$lines.Add("| # | Konu | Oneri |")
    [void]$lines.Add("|---|------|-------|")
    [void]$lines.Add("| 1 | Aktif legacy-only ($($s.legacyOnlyActive) kisi) | AD'de var mi / Keeper sync kapsami genisletilmeli mi kontrol |")
    [void]$lines.Add("| 2 | Keeper AD duplicate (~$($s.keeperOnlyLikelyDuplicate) kayit) | Keycloak LDAP mapper (sAMAccountName); CN-tabanli ikinci kayitlar temizlenebilir |")
    [void]$lines.Add("| 3 | Pasif legacy ($($s.legacyOnlyInactive) kisi) | Migrasyon disi birakilabilir; arsiv amacli tutulur |")
    [void]$lines.Add("| 4 | Eslesen $($s.matched) kisi | MonitraNG'de oturum acabilir; legacy hesap referans olarak eslenebilir |")
    [void]$lines.Add("| 5 | Legacy admin | Keeper'da ayri yonetim; bire bir eslestirme gerekmez |")
    [void]$lines.Add("")
    [void]$lines.Add("*Script:* ``scripts/tests/MngKeeper/users/compare-legacy-kalite-users.ps1``")

    Set-Content -Path $Path -Value ($lines -join "`n") -Encoding UTF8
}
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "docs/odak/eskiapp/reports"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Legacy kullanicilar aliniyor ($LegacyServer)..." -ForegroundColor Cyan
$legacyUsers = if ($SkipLegacyFetch) {
    $cache = Join-Path $OutputDir "legacy-users-cache.json"
    if (-not (Test-Path $cache)) { throw "Cache yok: $cache" }
    Get-Content $cache -Raw | ConvertFrom-Json
} else {
    $u = Get-LegacyUsers
    $u | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $OutputDir "legacy-users-cache.json") -Encoding UTF8
    $u
}
Write-Host "  Legacy: $(@($legacyUsers).Count) kayit" -ForegroundColor Green

Write-Host "Keeper kullanicilar aliniyor ($KeeperBaseUrl)..." -ForegroundColor Cyan
$keeperUsers = if ($SkipKeeperFetch) {
    $cache = Join-Path $OutputDir "keeper-users-cache.json"
    if (-not (Test-Path $cache)) { throw "Cache yok: $cache" }
    Get-Content $cache -Raw | ConvertFrom-Json
} else {
    $token = Get-KeeperToken
    $u = Get-KeeperUsers -Token $token
    $u | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $OutputDir "keeper-users-cache.json") -Encoding UTF8
    $u
}
Write-Host "  Keeper: $(@($keeperUsers).Count) kayit" -ForegroundColor Green

Write-Host "Karsilastiriliyor..." -ForegroundColor Cyan
$report = Compare-UserSets -LegacyUsers $legacyUsers -KeeperUsers $keeperUsers

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$jsonPath = Join-Path $OutputDir "legacy-keeper-user-compare_$stamp.json"
$csvPath  = Join-Path $OutputDir "legacy-keeper-user-compare_$stamp.csv"
$mdPath   = Join-Path $OutputDir "legacy-keeper-user-compare_$stamp.md"
$mdLatest = Join-Path $OutputDir "legacy-keeper-user-compare_LATEST.md"

$report | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding UTF8

$csvRows = New-Object System.Collections.Generic.List[object]
foreach ($m in $report.matched) {
    [void]$csvRows.Add([pscustomobject]@{
        category       = "matched"
        matchType      = $m.matchType
        legacyUsername = $m.legacyUsername
        legacyName     = $m.legacyName
        legacyActive   = $m.legacyActive
        keeperUsername = $m.keeperUsername
        keeperName     = $m.keeperName
        keeperActive   = $m.keeperActive
        keeperSource   = $m.keeperSource
    })
}
foreach ($l in $report.legacyOnly) {
    [void]$csvRows.Add([pscustomobject]@{
        category       = "legacy_only"
        matchType      = ""
        legacyUsername = $l.legacyUsername
        legacyName     = $l.legacyName
        legacyActive   = $l.legacyActive
        keeperUsername = ""
        keeperName     = ""
        keeperActive   = ""
        keeperSource   = ""
    })
}
foreach ($k in $report.keeperOnly) {
    [void]$csvRows.Add([pscustomobject]@{
        category       = "keeper_only"
        matchType      = ""
        legacyUsername = ""
        legacyName     = ""
        legacyActive   = ""
        keeperUsername = $k.keeperUsername
        keeperName     = $k.keeperName
        keeperActive   = $k.keeperActive
        keeperSource   = $k.keeperSource
    })
}
foreach ($a in $report.ambiguous) {
    [void]$csvRows.Add([pscustomobject]@{
        category       = "ambiguous"
        matchType      = $a.reason
        legacyUsername = $a.legacyUsername
        legacyName     = $a.legacyName
        legacyActive   = $a.legacyActive
        keeperUsername = ""
        keeperName     = ""
        keeperActive   = ""
        keeperSource   = "count=$($a.keeperCount)"
    })
}
$csvRows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

Write-UserCompareMarkdownReport -Report $report -Path $mdPath
Copy-Item -Path $mdPath -Destination $mdLatest -Force

Write-Host ""
Write-Host "=== Ozet ===" -ForegroundColor Yellow
$report.summary.GetEnumerator() | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0,-28} {1}" -f ($_.Key + ":"), $_.Value)
}
Write-Host ""
Write-Host "JSON:   $jsonPath" -ForegroundColor Green
Write-Host "CSV:    $csvPath" -ForegroundColor Green
Write-Host "MD:     $mdPath" -ForegroundColor Green
Write-Host "LATEST: $mdLatest" -ForegroundColor Green
