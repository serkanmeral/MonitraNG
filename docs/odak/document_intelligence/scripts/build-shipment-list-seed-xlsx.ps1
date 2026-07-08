# Sevkiyat listesi XLSX seed şablonu üretir (ShipmentListTemplateXlsxFactory ile aynı içerik).
#
# Çıktı: docs/odak/document_intelligence/sample/ODK-SHIPMENT-LIST-template-seed.xlsx
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\build-shipment-list-seed-xlsx.ps1

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$outputXlsx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-SHIPMENT-LIST-template-seed.xlsx"
$infraProj = Join-Path $repoRoot "MngDocument/Infrastructure/MngDocument.Infrastructure/MngDocument.Infrastructure.csproj"

if (-not (Test-Path $infraProj)) {
    throw "Proje bulunamadi: $infraProj"
}

$buildDir = Join-Path $repoRoot ".tmp-build-shipment-xlsx"
$runnerDir = Join-Path $buildDir "runner"
$runnerProj = Join-Path $runnerDir "ShipmentXlsxSeedRunner.csproj"

if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
New-Item -ItemType Directory -Path $runnerDir -Force | Out-Null

@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\MngDocument\Infrastructure\MngDocument.Infrastructure\MngDocument.Infrastructure.csproj" />
  </ItemGroup>
</Project>
'@ | Set-Content -Path $runnerProj -Encoding UTF8

@'
using MngDocument.Infrastructure.Services;

var bytes = ShipmentListTemplateXlsxFactory.Create();
var outPath = args.Length > 0 ? args[0] : "ODK-SHIPMENT-LIST-template-seed.xlsx";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllBytes(outPath, bytes);
Console.WriteLine($"OK {outPath} ({bytes.Length} bytes)");
'@ | Set-Content -Path (Join-Path $runnerDir "Program.cs") -Encoding UTF8

Push-Location $runnerDir
try {
    dotnet run --project $runnerProj -c Release -- $outputXlsx
    if (-not (Test-Path $outputXlsx)) {
        throw "XLSX uretilemedi: $outputXlsx"
    }
    Write-Host "Seed XLSX hazir: $outputXlsx" -ForegroundColor Green
}
finally {
    Pop-Location
}
