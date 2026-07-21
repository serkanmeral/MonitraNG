# Shared helpers for monitrang.com (online) deploy scripts.
# Uses OpenSSH (ssh/scp) + BatchMode — no Posh-SSH.

function Get-MngOnlineRepoRoot {
    param([string]$ScriptRoot)
    return (Resolve-Path (Join-Path $ScriptRoot "../..")).Path
}

function Test-MngOnlineSsh {
    param(
        [string]$Server = "monitrang-server",
        [int]$TimeoutSec = 20
    )
    $probe = ssh -o BatchMode=yes -o ConnectTimeout=$TimeoutSec $Server "echo OK"
    if ($LASTEXITCODE -ne 0 -or $probe -notmatch "OK") {
        throw "SSH failed for $Server. Check ACCESS.md / ~/.ssh/config (Host monitrang-server)."
    }
}

function Invoke-MngOnlineSsh {
    param(
        [string]$Server = "monitrang-server",
        [Parameter(Mandatory = $true)][string]$Command,
        [int]$TimeoutSec = 20
    )
    # ConnectTimeout is SSH handshake only; long builds need ServerAlive.
    $sshArgs = @(
        "-o", "BatchMode=yes",
        "-o", "ConnectTimeout=$TimeoutSec",
        "-o", "ServerAliveInterval=30",
        "-o", "ServerAliveCountMax=120",
        $Server,
        $Command
    )
    # Pipe through Write-Host so stdout does not pollute the function return value.
    & ssh @sshArgs 2>&1 | ForEach-Object {
        if ($_ -is [System.Management.Automation.ErrorRecord]) {
            Write-Host $_.Exception.Message -ForegroundColor Red
        }
        else {
            Write-Host $_
        }
    }
    return [int]$LASTEXITCODE
}

function Invoke-MngOnlineRemoteBash {
    param(
        [string]$Server = "monitrang-server",
        [Parameter(Mandatory = $true)][string]$ScriptBody,
        [string]$RemoteName = "mngonline-remote.sh"
    )
    # Avoid Windows→ssh multiline quoting issues: upload script, then bash it.
    $localTmp = Join-Path $env:TEMP $RemoteName
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($localTmp, ($ScriptBody -replace "`r`n", "`n" -replace "`r", "`n"), $utf8NoBom)
    $remotePath = "/tmp/$RemoteName"
    Send-MngOnlineScp -Server $Server -LocalPath $localTmp -RemoteDestination $remotePath
    $null = Invoke-MngOnlineSsh -Server $Server -Command "bash '$remotePath'; ec=`$?; rm -f '$remotePath'; exit `$ec"
    $exit = [int]$LASTEXITCODE
    Remove-Item $localTmp -Force -ErrorAction SilentlyContinue
    return $exit
}

function Send-MngOnlineScp {
    param(
        [string]$Server = "monitrang-server",
        [Parameter(Mandatory = $true)][string]$LocalPath,
        [Parameter(Mandatory = $true)][string]$RemoteDestination,
        [int]$TimeoutSec = 20
    )
    if (-not (Test-Path $LocalPath)) {
        throw "Local path not found: $LocalPath"
    }
    $dest = "${Server}:${RemoteDestination}"
    scp -o BatchMode=yes -o ConnectTimeout=$TimeoutSec $LocalPath $dest
    if ($LASTEXITCODE -ne 0) {
        throw "scp failed: $LocalPath -> $dest"
    }
}

function Get-MngOnlineDefaultSyncPaths {
    return @(
        "ApplicationResources/mng_apps",
        "MngGateway", "MngKeeper", "MngDataGateway", "MngReactor", "MngEngine", "MngHub",
        "MngScheduler", "MngWorkflow", "MngAlarm", "MngOperations", "MngDocument", "MngAdmin", "MngNotifier",
        "MngLLM", "Mng.Ui", "MngDomainUI"
    )
}

function Get-MngOnlineRollingOrder {
    # Preferred order when multiple services are requested
    return @(
        "mngkeeper", "mngdatagateway", "mnghub", "mngllm", "mngscheduler",
        "mngadmin", "mngnotifier", "mnggateway", "mngui", "mngdomainui"
    )
}

function Sort-MngOnlineServices {
    param([string[]]$Services)
    $order = Get-MngOnlineRollingOrder
    $ranked = @()
    $rest = @()
    foreach ($s in $Services) {
        $name = $s.Trim()
        if (-not $name) { continue }
        $idx = [array]::IndexOf($order, $name)
        if ($idx -ge 0) {
            $ranked += [pscustomobject]@{ Name = $name; Rank = $idx }
        }
        else {
            $rest += $name
        }
    }
    $sorted = @($ranked | Sort-Object Rank | ForEach-Object { $_.Name })
    return @($sorted + $rest)
}
