# Ortak SSH kimlik bilgisi (sync / deploy scriptleri dot-source eder)
$script:OdakLocalCredFile = Join-Path $PSScriptRoot "local-credentials.ps1"
if (Test-Path $script:OdakLocalCredFile) {
    . $script:OdakLocalCredFile
}

$script:OdakProdServer = "192.168.20.8"
$script:OdakTestServer = "192.168.20.20"

function Import-OdakEnvFile {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return }
    Get-Content $Path | ForEach-Object {
        $line = $_.Trim()
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) { return }
        if ($line -match '^\s*(\w+)\s*=\s*(.+)\s*$') {
            $name = $matches[1]
            $value = $matches[2].Trim().Trim('"').Trim("'")
            Set-Item -Path "env:$name" -Value $value -Force
        }
    }
}

function Initialize-OdakSshEnvironment {
    param([string]$Server = $script:OdakTestServer)

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
    $isProd = ($Server -eq $script:OdakProdServer)

    if ($isProd) {
        Import-OdakEnvFile (Join-Path $repoRoot ".env.odak.prod.local")
        if (-not [string]::IsNullOrWhiteSpace($env:ODAK_PROD_SSH_PASSWORD)) {
            $env:ODAK_SSH_PASSWORD = $env:ODAK_PROD_SSH_PASSWORD
        }
    } else {
        Import-OdakEnvFile (Join-Path $repoRoot ".env.odak.local")
    }
}

# Varsayılan: test kimlik bilgisi (script -Server ile prod seçilince Initialize-OdakSshEnvironment çağrılır)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
if ([string]::IsNullOrWhiteSpace($env:ODAK_SSH_PASSWORD)) {
    Import-OdakEnvFile (Join-Path $repoRoot ".env.odak.local")
}

function Get-OdakComposeOdakFile {
    param([string]$Server = $script:OdakTestServer)
    if ($Server -eq $script:OdakProdServer) { return "docker-compose.odak.prod.yml" }
    return "docker-compose.odak.yml"
}

function Test-OdakProductionServer {
    param([string]$Server)
    return ($Server -eq $script:OdakProdServer)
}

function ConvertTo-UnixShell {
    param([string]$Script)
    return ($Script -replace "`r`n", "`n" -replace "`r", "`n")
}

function Get-OdakRabbitMqCredentials {
    param(
        [Parameter(Mandatory = $true)]
        $SshSession,
        [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps"
    )

    $username = $env:ODAK_RABBITMQ_USERNAME
    if ([string]::IsNullOrWhiteSpace($username)) { $username = "admin" }

    $password = $env:ODAK_RABBITMQ_PASSWORD
    if ([string]::IsNullOrWhiteSpace($password)) {
        $grepCmd = "grep '^RABBITMQ_PASSWORD=' '$RemoteAppsDir/.env' 2>/dev/null | head -1 | cut -d= -f2- | tr -d '\r'"
        $r = Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $grepCmd -TimeOut 15
        $password = ($r.Output -join "").Trim().Trim('"').Trim("'")
    }

    if ([string]::IsNullOrWhiteSpace($password)) { $password = "admin123" }

    return @{ Username = $username; Password = $password }
}

function Invoke-OdakRabbitMqPublish {
    param(
        [Parameter(Mandatory = $true)]
        $SshSession,
        [Parameter(Mandatory = $true)]
        [string]$Exchange,
        [Parameter(Mandatory = $true)]
        [string]$RoutingKey,
        [Parameter(Mandatory = $true)]
        [string]$Payload,
        [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps"
    )

    $creds = Get-OdakRabbitMqCredentials -SshSession $SshSession -RemoteAppsDir $RemoteAppsDir
    $escapedPassword = $creds.Password.Replace("'", "'\''")
    $cmd = "docker exec rabbitmq rabbitmqadmin -u $($creds.Username) -p '$escapedPassword' publish exchange=$Exchange routing_key=$RoutingKey payload='$Payload'"
    return Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $cmd -TimeOut 30
}

function Get-OdakMongoCredentials {
    param(
        [Parameter(Mandatory = $true)]
        $SshSession,
        [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps"
    )

    $username = $env:ODAK_MONGO_USERNAME
    if ([string]::IsNullOrWhiteSpace($username)) { $username = "admin" }

    $password = $env:ODAK_MONGO_PASSWORD
    if ([string]::IsNullOrWhiteSpace($password)) {
        $grepCmd = "grep '^MONGO_PASSWORD=' '$RemoteAppsDir/.env' 2>/dev/null | head -1 | cut -d= -f2- | tr -d '\r'"
        $r = Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $grepCmd -TimeOut 15
        $password = ($r.Output -join "").Trim().Trim('"').Trim("'")
    }

    if ([string]::IsNullOrWhiteSpace($password)) { $password = "admin123" }

    return @{ Username = $username; Password = $password }
}

function Invoke-OdakMongoJsonEval {
    param(
        [Parameter(Mandatory = $true)]
        $SshSession,
        [Parameter(Mandatory = $true)]
        [string]$JavaScript,
        [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps"
    )

    $mongo = Get-OdakMongoCredentials -SshSession $SshSession -RemoteAppsDir $RemoteAppsDir
    $escapedPassword = $mongo.Password.Replace("'", "'\''")
    $b64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($JavaScript))
    $cmd = "echo '$b64' | base64 -d | docker exec -i mongo mongosh -u $($mongo.Username) -p '$escapedPassword' --authenticationDatabase admin --quiet"
    return Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $cmd -TimeOut 90
}

function Initialize-OdakSecEventsMqCapture {
    param(
        [Parameter(Mandatory = $true)]
        $SshSession,
        [Parameter(Mandatory = $true)]
        [string]$Domain,
        [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps"
    )

    $creds = Get-OdakRabbitMqCredentials -SshSession $SshSession -RemoteAppsDir $RemoteAppsDir
    $escapedPassword = $creds.Password.Replace("'", "'\''")
    $queueName = "siem-e2e-$Domain-$(Get-Random -Maximum 999999)"
    $routingKey = "sec_events.created.$Domain"

    $cmd = @"
docker exec rabbitmq rabbitmqadmin -u $($creds.Username) -p '$escapedPassword' declare queue name=$queueName durable=false auto_delete=true
docker exec rabbitmq rabbitmqadmin -u $($creds.Username) -p '$escapedPassword' declare binding source=mng.topics destination=$queueName routing_key=$routingKey
echo QUEUE=$queueName
"@

    $r = Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $cmd -TimeOut 45
    $line = @($r.Output) | Where-Object { $_ -match '^QUEUE=' } | Select-Object -First 1
    if (-not $line) { throw "MQ capture queue olusturulamadi" }
    return ($line -replace '^QUEUE=', '').Trim()
}

function Get-OdakSecEventsMqMessages {
    param(
        [Parameter(Mandatory = $true)]
        $SshSession,
        [Parameter(Mandatory = $true)]
        [string]$QueueName,
        [int]$Count = 10,
        [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps"
    )

    $creds = Get-OdakRabbitMqCredentials -SshSession $SshSession -RemoteAppsDir $RemoteAppsDir
    $escapedPassword = $creds.Password.Replace("'", "'\''")
    $cmd = "docker exec rabbitmq rabbitmqadmin -u $($creds.Username) -p '$escapedPassword' get queue=$QueueName count=$Count ackmode=ack_requeue_false"
    return Invoke-SSHCommand -SessionId $SshSession.SessionId -Command $cmd -TimeOut 45
}

function Get-OdakSshCredential {
    param(
        [string]$User = "odak",
        [string]$Server = "192.168.20.20",
        [SecureString]$Password
    )

    Initialize-OdakSshEnvironment -Server $Server

    if ($Password) {
        return New-Object System.Management.Automation.PSCredential($User, $Password)
    }

    $plain = $env:ODAK_SSH_PASSWORD
    if (-not [string]::IsNullOrWhiteSpace($plain)) {
        $sec = ConvertTo-SecureString $plain -AsPlainText -Force
        return New-Object System.Management.Automation.PSCredential($User, $sec)
    }

    Write-Host "SSH: $User@${Server} (parola gerekli)" -ForegroundColor Cyan
    $hint = if ($Server -eq $script:OdakProdServer) { ".env.odak.prod.local" } else { ".env.odak.local veya local-credentials.ps1" }
    Write-Host "  B: `$env:ODAK_SSH_PASSWORD veya $hint (gitignore)" -ForegroundColor Gray
    $pass = Read-Host "SSH password for ${User}@${Server}" -AsSecureString
    return New-Object System.Management.Automation.PSCredential($User, $pass)
}
