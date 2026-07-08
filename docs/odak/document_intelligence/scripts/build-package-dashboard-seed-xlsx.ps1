# İş paketi kontrol paneli XLSX seed şablonu üretir.
#
# Çıktı: docs/odak/document_intelligence/sample/ODK-PACKAGE-DASHBOARD-template-seed.xlsx
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\build-package-dashboard-seed-xlsx.ps1
#   .\docs\odak\document_intelligence\scripts\build-package-dashboard-seed-xlsx.ps1 -OutputPath "...\sample\v2\ODK-PACKAGE-DASHBOARD-template-seed-v2.xlsx"

param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$outputXlsx = if ($OutputPath) {
    if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
} else {
    Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-PACKAGE-DASHBOARD-template-seed.xlsx"
}
$infraProj = Join-Path $repoRoot "MngDocument/Infrastructure/MngDocument.Infrastructure/MngDocument.Infrastructure.csproj"

if (-not (Test-Path $infraProj)) {
    throw "Proje bulunamadi: $infraProj"
}

$buildDir = Join-Path $repoRoot ".tmp-build-package-dashboard-xlsx"
$runnerDir = Join-Path $buildDir "runner"
$runnerProj = Join-Path $runnerDir "PackageDashboardXlsxSeedRunner.csproj"

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

var bytes = PackageDashboardTemplateXlsxFactory.Create();
var outPath = args.Length > 0 ? args[0] : "ODK-PACKAGE-DASHBOARD-template-seed.xlsx";
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
