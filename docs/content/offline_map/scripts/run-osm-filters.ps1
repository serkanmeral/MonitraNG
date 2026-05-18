# OSM Filtre Script — osm-filters.json'dan okur, osmium ile PBF + GeoJSON üretir
# Faz 1.3: railways, stations, places çıktıları (Faz 1.4 PostGIS import için hazır)
# Kullanım:
#   Yerel osmium ile: .\run-osm-filters.ps1 [-ConfigPath "path"] [-ProjectRoot "path"]
#   Docker ile (osmium kurulumu gerekmez): .\run-osm-filters.ps1 -UseDocker [-ProjectRoot "path"]
# ProjectRoot: source_pbf ve output_dir bu dizine göre (varsayılan: çalıştırdığınız dizin)

param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "..\osm-filters.json"),
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$UseDocker
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ConfigPath)) {
    Write-Error "Config dosyası bulunamadı: $ConfigPath"
}

$config = Get-Content $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$projectRootAbs = (Resolve-Path $ProjectRoot).Path
$pbfPath = Join-Path $projectRootAbs $config.source_pbf
$exportPath = Join-Path $projectRootAbs $config.output_dir

if (-not (Test-Path $pbfPath)) {
    Write-Error "PBF dosyası bulunamadı: $pbfPath. Geofabrik'ten turkey-latest.osm.pbf indirip bu konuma koyun."
}

New-Item -ItemType Directory -Force -Path $exportPath | Out-Null

# Container içi yollar: ProjectRoot -> /work (Unix path)
$workSrc = "/work/" + $config.source_pbf.Replace("\", "/").TrimStart("/")
$workOutDir = "/work/" + $config.output_dir.Replace("\", "/").TrimStart("/")

if ($UseDocker) {
    $dockerImage = "stefda/osmium-tool:latest"
    Write-Host "Docker kullanılıyor: $dockerImage (osmium host'ta kurulu olmak zorunda değil)"
    $workMount = "${projectRootAbs}:/work"
    foreach ($f in $config.filters) {
        $valuesStr = ($f.values -join ",")
        $filterArg = "$($f.type)/$($f.tag)=$valuesStr"
        $workOutPbf = "$workOutDir/$($f.output)"
        Write-Host "Filtre (PBF): $($f.name) -> $filterArg"
        docker run --rm -v $workMount -w /work $dockerImage osmium tags-filter $workSrc $filterArg -o $workOutPbf
        if ($LASTEXITCODE -ne 0) { throw "osmium tags-filter hatası: $($f.name)" }
        if ($f.geojson) {
            $workOutGeo = "$workOutDir/$($f.geojson)"
            Write-Host "  GeoJSON: $($f.geojson)"
            docker run --rm -v $workMount -w /work $dockerImage osmium export $workOutPbf -f geojson -o $workOutGeo
            if ($LASTEXITCODE -ne 0) { throw "osmium export hatası: $($f.name)" }
        }
    }
} else {
    $osmium = Get-Command osmium -ErrorAction SilentlyContinue
    if (-not $osmium) {
        Write-Error "osmium bulunamadı. PATH'e ekleyin, 'choco install osmium-tool' deneyin veya bu scripti -UseDocker ile çalıştırın."
    }
    foreach ($f in $config.filters) {
        $valuesStr = ($f.values -join ",")
        $filterArg = "$($f.type)/$($f.tag)=$valuesStr"
        $outPbf = Join-Path $exportPath $f.output
        Write-Host "Filtre (PBF): $($f.name) -> $filterArg"
        & osmium tags-filter $pbfPath $filterArg -o $outPbf
        if ($LASTEXITCODE -ne 0) { throw "osmium tags-filter hatası: $($f.name)" }
        if ($f.geojson) {
            $outGeo = Join-Path $exportPath $f.geojson
            Write-Host "  GeoJSON: $($f.geojson)"
            & osmium export $outPbf -f geojson -o $outGeo
            if ($LASTEXITCODE -ne 0) { throw "osmium export hatası: $($f.name)" }
        }
    }
}

Write-Host "Tamamlandı. PBF ve GeoJSON çıktıları: $exportPath"
