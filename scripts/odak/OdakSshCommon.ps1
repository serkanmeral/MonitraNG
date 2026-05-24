# Ortak SSH kimlik bilgisi (sync / deploy scriptleri dot-source eder)
$script:OdakLocalCredFile = Join-Path $PSScriptRoot "local-credentials.ps1"
if (Test-Path $script:OdakLocalCredFile) {
    . $script:OdakLocalCredFile
}

# Agent / otomasyon: repo kökünde .env.odak.local (ODAK_SSH_PASSWORD=...)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$envOdakLocal = Join-Path $repoRoot ".env.odak.local"
if ([string]::IsNullOrWhiteSpace($env:ODAK_SSH_PASSWORD) -and (Test-Path $envOdakLocal)) {
    Get-Content $envOdakLocal | ForEach-Object {
        $line = $_.Trim()
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) { return }
        if ($line -match '^\s*ODAK_SSH_PASSWORD\s*=\s*(.+)\s*$') {
            $env:ODAK_SSH_PASSWORD = $matches[1].Trim().Trim('"').Trim("'")
        }
    }
}

function Get-OdakSshCredential {
    param(
        [string]$User = "odak",
        [string]$Server = "192.168.20.20",
        [SecureString]$Password
    )

    if ($Password) {
        return New-Object System.Management.Automation.PSCredential($User, $Password)
    }

    $plain = $env:ODAK_SSH_PASSWORD
    if (-not [string]::IsNullOrWhiteSpace($plain)) {
        $sec = ConvertTo-SecureString $plain -AsPlainText -Force
        return New-Object System.Management.Automation.PSCredential($User, $sec)
    }

    Write-Host "SSH: $User@${Server} (parola gerekli)" -ForegroundColor Cyan
    Write-Host "  B: `$env:ODAK_SSH_PASSWORD veya scripts/odak/local-credentials.ps1 (gitignore)" -ForegroundColor Gray
    Write-Host "  Ornek: Copy-Item local-credentials.ps1.example local-credentials.ps1" -ForegroundColor Gray
    $pass = Read-Host "SSH password for ${User}@${Server}" -AsSecureString
    return New-Object System.Management.Automation.PSCredential($User, $pass)
}
