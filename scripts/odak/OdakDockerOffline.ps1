# Odak offline Docker deploy — paylasilan manifest ve yardimcilar
# dot-source: . (Join-Path $PSScriptRoot "OdakDockerOffline.ps1")

$script:OdakDockerRepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$script:OdakDockerArtifactDir = Join-Path $script:OdakDockerRepoRoot "artifacts/odak-docker"

# docker-compose.production.yml ile uyumlu servis tanimlari
$script:OdakDockerServiceCatalog = @{
    mnggateway         = @{ Context = "MngGateway";         Dockerfile = "Presentation/MngGateway.Api/Dockerfile"; Image = "mnggateway" }
    mngkeeper          = @{ Context = "MngKeeper";          Dockerfile = "Presentation/MngKeeper.Api/Dockerfile"; Image = "mngkeeper" }
    mngdatagateway     = @{ Context = "MngDataGateway";     Dockerfile = "Presentation/MngDataGateway.Api/Dockerfile"; Image = "mngdatagateway" }
    mngreactor         = @{ Context = "MngReactor";         Dockerfile = "Dockerfile"; Image = "mngreactor" }
    mngengine          = @{ Context = "MngEngine/MngEngine.Service"; Dockerfile = "Dockerfile"; Image = "mngengine" }
    mnghub             = @{ Context = "MngHub";             Dockerfile = "Presentation/MngHub.Api/Dockerfile"; Image = "mnghub" }
    mngllm             = @{ Context = "MngLLM";             Dockerfile = "Presentation/MngLLM.Api/Dockerfile"; Image = "mngllm" }
    mngscheduler       = @{ Context = "MngScheduler";       Dockerfile = "Presentation/MngScheduler.Api/Dockerfile"; Image = "mngscheduler" }
    mngworkflow        = @{ Context = "MngWorkflow";        Dockerfile = "Presentation/MngWorkflow.Api/Dockerfile"; Image = "mngworkflow" }
    "mngworkflow-worker" = @{ Context = "MngWorkflow";        Dockerfile = "Presentation/MngWorkflow.Worker/Dockerfile"; Image = "mngworkflow-worker" }
    mngalarm           = @{ Context = "MngAlarm";           Dockerfile = "Presentation/MngAlarm.Api/Dockerfile"; Image = "mngalarm" }
    "mngalarm-worker"  = @{ Context = "MngAlarm";           Dockerfile = "Presentation/MngAlarm.Worker/Dockerfile"; Image = "mngalarm-worker" }
    mngoperations      = @{ Context = "MngOperations";      Dockerfile = "Presentation/MngOperations.Api/Dockerfile"; Image = "mngoperations" }
    mngdocument        = @{ Context = "MngDocument";        Dockerfile = "Presentation/MngDocument.Api/Dockerfile"; Image = "mngdocument" }
    mngadmin           = @{ Context = "MngAdmin";           Dockerfile = "Presentation/MngAdmin.Api/Dockerfile"; Image = "mngadmin" }
    mngnotifier        = @{ Context = "MngNotifier";        Dockerfile = "Presentation/MngNotifier.Api/Dockerfile"; Image = "mngnotifier" }
    mngui              = @{ Context = "Mng.Ui";             Dockerfile = "Dockerfile"; Image = "mngui"; UiBuildArgs = $true }
    mngdomainui        = @{ Context = "MngDomainUI";        Dockerfile = "Dockerfile"; Image = "mngdomainui" }
}

# Prod/test sunucuda build OLMADAN calisacak ucuncu parti image'lar (bir kez yuklenir)
$script:OdakDockerThirdPartyImages = @(
    "gotenberg/gotenberg:8"
    "collabora/code:24.04.13.1.1"
)

# Local build icin on cekilecek base image'lar (internet olan gelistirme makinesi)
$script:OdakDockerBaseImages = @(
    "mcr.microsoft.com/dotnet/aspnet:9.0"
    "mcr.microsoft.com/dotnet/sdk:9.0"
    "node:18-alpine"
    "nginx:alpine"
)

function Get-OdakDockerServiceNames {
    return @($script:OdakDockerServiceCatalog.Keys)
}

function Resolve-OdakDockerServiceList {
    param([string[]]$Services)

    if (-not $Services -or $Services.Count -eq 0) {
        throw "En az bir servis belirtin (-Services mngdocument,mngui)."
    }

    $normalized = @()
    foreach ($raw in $Services) {
        foreach ($part in ($raw -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
            if (-not $script:OdakDockerServiceCatalog.ContainsKey($part)) {
                $known = ($script:OdakDockerServiceCatalog.Keys | Sort-Object) -join ', '
                throw "Bilinmeyen servis: '$part'. Bilinen: $known"
            }
            if ($normalized -notcontains $part) { $normalized += $part }
        }
    }
    return $normalized
}

function Get-OdakDockerImageRef {
    param(
        [string]$ServiceName,
        [string]$Version = "latest"
    )
    $image = $script:OdakDockerServiceCatalog[$ServiceName].Image
    return "${image}:${Version}"
}

function Get-OdakDockerSyncPathsForServices {
    param([string[]]$ServiceNames)

    $paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$paths.Add("ApplicationResources/mng_apps")
    foreach ($svc in $ServiceNames) {
        [void]$paths.Add($script:OdakDockerServiceCatalog[$svc].Context)
    }
    return @($paths)
}

function Get-OdakUiBuildArgs {
    param(
        [ValidateSet("prod", "test")]
        [string]$Target = "test"
    )

    $appsDir = Join-Path $script:OdakDockerRepoRoot "ApplicationResources/mng_apps"
    $envFile = if ($Target -eq "prod") {
        Join-Path $appsDir ".env.odak.prod.example"
    } else {
        Join-Path $appsDir ".env.odak.example"
    }

    $gateway = "http://192.168.20.20:5040"
    $hub = "http://192.168.20.20:5020"
    if (Test-Path $envFile) {
        Get-Content $envFile | ForEach-Object {
            $line = $_.Trim()
            if ($line -match '^\s*GATEWAY_URL\s*=\s*(.+)\s*$') { $gateway = $matches[1].Trim().Trim('"').Trim("'") }
            if ($line -match '^\s*HUB_URL\s*=\s*(.+)\s*$') { $hub = $matches[1].Trim().Trim('"').Trim("'") }
        }
    }

    return @{
        GATEWAY_URL = $gateway
        HUB_URL     = $hub
    }
}

function Test-OdakDockerAvailable {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { return $false }
    $null = docker info 2>&1
    return $LASTEXITCODE -eq 0
}

function Assert-OdakDockerAvailable {
    if (-not (Test-OdakDockerAvailable)) {
        throw @"
Docker calismiyor. Offline deploy icin gelistirme makinesinde Linux container destegi gerekir.
Windows Server: WSL2 + Docker Desktop (desktop-linux) kurulu ve ayakta olmali.
Kontrol: docker info
Alternatif: Docker calisan baska bir makinede build-odak-docker-images.ps1 calistirin, olusan .tar dosyasini deploy-odak-offline.ps1 -SkipBuild -ArchivePath ile gonderin.
"@
    }
}

function Build-OdakDockerServiceImage {
    param(
        [Parameter(Mandatory)]
        [string]$ServiceName,
        [string]$Version = "latest",
        [ValidateSet("prod", "test")]
        [string]$Target = "test",
        [switch]$NoCache
    )

    $def = $script:OdakDockerServiceCatalog[$ServiceName]
    $context = Join-Path $script:OdakDockerRepoRoot $def.Context
    $dockerfile = Join-Path $context $def.Dockerfile
    if (-not (Test-Path $dockerfile)) {
        throw "Dockerfile yok: $dockerfile"
    }

    $imageRef = Get-OdakDockerImageRef -ServiceName $ServiceName -Version $Version
    $args = @(
        "build",
        "-t", $imageRef,
        "-f", $def.Dockerfile,
        $context
    )
    if ($NoCache) { $args += "--no-cache" }

    if ($def.UiBuildArgs) {
        $uiArgs = Get-OdakUiBuildArgs -Target $Target
        $args += @("--build-arg", "GATEWAY_URL=$($uiArgs.GATEWAY_URL)")
        $args += @("--build-arg", "HUB_URL=$($uiArgs.HUB_URL)")
    }

    Write-Host "BUILD $ServiceName -> $imageRef" -ForegroundColor Cyan
    Write-Host "  docker $($args -join ' ')" -ForegroundColor DarkGray
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "docker build basarisiz: $ServiceName" }
    return $imageRef
}

function Export-OdakDockerImages {
    param(
        [Parameter(Mandatory)]
        [string[]]$ImageRefs,
        [Parameter(Mandatory)]
        [string]$ArchivePath
    )

    $dir = Split-Path $ArchivePath -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    if (Test-Path $ArchivePath) { Remove-Item $ArchivePath -Force }

    Write-Host "EXPORT -> $ArchivePath" -ForegroundColor Cyan
    & docker save @ImageRefs -o $ArchivePath
    if ($LASTEXITCODE -ne 0) { throw "docker save basarisiz" }

    $sizeMb = [Math]::Round((Get-Item $ArchivePath).Length / 1MB, 1)
    Write-Host "OK archive ($sizeMb MB): $ArchivePath" -ForegroundColor Green
    return $ArchivePath
}

function Get-OdakDockerDefaultArchivePath {
    param(
        [Parameter(Mandatory)]
        [string[]]$ServiceNames,
        [string]$Version = "latest",
        [string]$Server = "192.168.20.20"
    )

    if (-not (Test-Path $script:OdakDockerArtifactDir)) {
        New-Item -ItemType Directory -Path $script:OdakDockerArtifactDir -Force | Out-Null
    }

    $svcSlug = ($ServiceNames -join "-").Replace("/", "-")
    $hostSlug = $Server.Replace(".", "-")
    return Join-Path $script:OdakDockerArtifactDir "${hostSlug}-${svcSlug}-${Version}.tar"
}

function Import-OdakDockerArchiveRemote {
    param(
        [Parameter(Mandatory)]
        [string]$Server,
        [Parameter(Mandatory)]
        [System.Management.Automation.PSCredential]$Credential,
        [Parameter(Mandatory)]
        [string]$LocalArchivePath,
        [string]$RemoteDir = "/home/odak/odak-docker-import"
    )

    if (-not (Test-Path $LocalArchivePath)) {
        throw "Archive yok: $LocalArchivePath"
    }

    $fileName = [IO.Path]::GetFileName($LocalArchivePath)
    $remotePath = "$RemoteDir/$fileName"

    $session = New-SSHSession -ComputerName $Server -Credential $Credential -AcceptKey
    try {
        Invoke-SSHCommand -SessionId $session.SessionId -Command "mkdir -p '$RemoteDir'" -TimeOut 30 | Out-Null
    } finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }

    Send-OdakRemoteFile -ComputerName $Server -Credential $Credential -LocalPath $LocalArchivePath -RemoteDestination "$RemoteDir/" -AcceptKey

    $session = New-SSHSession -ComputerName $Server -Credential $Credential -AcceptKey
    try {
        Write-Host "REMOTE docker load ($Server)..." -ForegroundColor Cyan
        $cmd = "docker load -i '$remotePath'"
        $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd -TimeOut 3600
        $r.Output | ForEach-Object { Write-Host $_ }
        if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
        if ($r.ExitStatus -ne 0) { throw "docker load basarisiz (exit $($r.ExitStatus))" }
        Write-Host "OK docker load" -ForegroundColor Green
    } finally {
        Remove-SSHSession -SessionId $session.SessionId | Out-Null
    }
}

function Invoke-OdakDockerBaseImagePrefetch {
    param([switch]$IncludeThirdParty)

    Assert-OdakDockerAvailable
    foreach ($img in $script:OdakDockerBaseImages) {
        Write-Host "PULL $img" -ForegroundColor Cyan
        docker pull $img
        if ($LASTEXITCODE -ne 0) { throw "docker pull basarisiz: $img" }
    }
    if ($IncludeThirdParty) {
        foreach ($img in $script:OdakDockerThirdPartyImages) {
            Write-Host "PULL $img" -ForegroundColor Cyan
            docker pull $img
            if ($LASTEXITCODE -ne 0) { throw "docker pull basarisiz: $img" }
        }
    }
    Write-Host "Base image prefetch tamam." -ForegroundColor Green
}
