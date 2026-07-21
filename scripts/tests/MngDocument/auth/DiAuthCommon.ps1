# Shared helpers for DI auth / permission suites.
# Dot-source from other scripts in scripts/tests/MngDocument/

function Get-DiAuthRepoRoot {
    $here = $PSScriptRoot
    if (-not $here) { $here = Split-Path -Parent $MyInvocation.MyCommand.Path }
    # auth/ -> MngDocument/ -> tests/ -> scripts/ -> repo
    return (Resolve-Path (Join-Path $here "..\..\..\..")).Path
}

function Get-DiAuthPersonas {
    param([string]$Path = (Join-Path $PSScriptRoot "personas.json"))
    if (-not (Test-Path $Path)) {
        throw "personas.json not found: $Path"
    }
    return Get-Content -Path $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-DiPersonaToken {
    param(
        [Parameter(Mandatory)][string]$Persona,
        [string]$Gateway = "http://localhost:5040",
        [string]$DomainName = "odak",
        [object]$PersonasConfig
    )

    if (-not $PersonasConfig) {
        $PersonasConfig = Get-DiAuthPersonas
    }

    $p = $PersonasConfig.personas.$Persona
    if (-not $p) {
        throw "Unknown persona: $Persona. Known: $($PersonasConfig.personas.PSObject.Properties.Name -join ', ')"
    }

    $password = if ($p.passwordEnv -eq "adminPassword") {
        $PersonasConfig.adminPassword
    } else {
        $PersonasConfig.defaultPassword
    }

    $tokenFile = Join-Path $env:TEMP "di_auth_${Persona}_token.txt"
    $repoRoot = Get-DiAuthRepoRoot
    $getToken = Join-Path $repoRoot "scripts\tests\MngDataGateway\auth\get-token.ps1"

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $null = & $getToken `
            -KeeperBaseUrl $Gateway `
            -KeeperPath "/keeper/api/auth/token" `
            -DomainName $DomainName `
            -Username $p.username `
            -Password $password `
            -TokenFile $tokenFile 2>&1
    } finally {
        $ErrorActionPreference = $prevEap
    }

    if (-not (Test-Path $tokenFile)) {
        throw "Token file missing for persona $Persona ($($p.username))"
    }
    $token = (Get-Content $tokenFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Empty token for persona $Persona ($($p.username))"
    }
    return $token
}

function Invoke-DiDocs {
    param(
        [Parameter(Mandatory)][string]$Gateway,
        [Parameter(Mandatory)][string]$Token,
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body = $null,
        [int]$TimeoutSec = 60
    )

    $uri = "$Gateway/documents/api/v1$Path"
    $headers = @{
        Authorization = "Bearer $Token"
        Accept        = "application/json"
    }

    $params = @{
        Uri             = $uri
        Method          = $Method
        Headers         = $headers
        TimeoutSec      = $TimeoutSec
        SkipHttpErrorCheck = $true
    }

    # PowerShell 5 may not have SkipHttpErrorCheck — fall back
    $supportsSkip = (Get-Command Invoke-WebRequest).Parameters.ContainsKey("SkipHttpErrorCheck")

    if ($null -ne $Body) {
        $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 -Compress }
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
    }

    try {
        if ($supportsSkip) {
            $resp = Invoke-WebRequest @params
            return [pscustomobject]@{
                StatusCode = [int]$resp.StatusCode
                Content    = $resp.Content
                Headers    = $resp.Headers
            }
        }

        try {
            $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                -ContentType $(if ($null -ne $Body) { "application/json; charset=utf-8" } else { $null }) `
                -Body $(if ($null -ne $Body) { $params.Body } else { $null }) `
                -TimeoutSec $TimeoutSec -UseBasicParsing
            return [pscustomobject]@{
                StatusCode = [int]$resp.StatusCode
                Content    = $resp.Content
                Headers    = $resp.Headers
            }
        } catch {
            $code = 0
            if ($_.Exception.Response) {
                $code = [int]$_.Exception.Response.StatusCode
                $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                $content = $reader.ReadToEnd()
                $reader.Close()
                return [pscustomobject]@{ StatusCode = $code; Content = $content; Headers = $null }
            }
            throw
        }
    } catch {
        throw "Invoke-DiDocs $Method $Path failed: $($_.Exception.Message)"
    }
}

function ConvertFrom-DiJson {
    param([string]$Content)
    if ([string]::IsNullOrWhiteSpace($Content)) { return $null }
    return $Content | ConvertFrom-Json
}

function Find-DiFolderInTree {
    param(
        [object]$TreeNodes,
        [string]$FolderId
    )
    if (-not $TreeNodes) { return $false }
    $stack = [System.Collections.Generic.Stack[object]]::new()
    foreach ($n in @($TreeNodes)) { $stack.Push($n) }
    while ($stack.Count -gt 0) {
        $cur = $stack.Pop()
        if ($cur.id -eq $FolderId -or $cur.Id -eq $FolderId) { return $true }
        foreach ($c in @($cur.children + $cur.Children)) {
            if ($c) { $stack.Push($c) }
        }
    }
    return $false
}

function Get-DiFixtureStatePath {
    Join-Path $env:TEMP "di_auth_fixture_state.json"
}
