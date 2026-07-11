# Odak rapor belge XLSX seed dosyalarını üretir (Eğitim + Zimmet).
#
# Çıktı:
#   docs/odak/document_intelligence/sample/ODK-RPT-EGITIM-*-template-seed.xlsx|.docx
#   docs/odak/document_intelligence/sample/ODK-RPT-ZIMMET-*-template-seed.xlsx
#
# Kullanım:
#   .\docs\odak\reporting_services\scripts\build-reporting-document-templates-xlsx.ps1

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$sampleDir = Join-Path $repoRoot "docs/odak/document_intelligence/sample"
$infraProj = Join-Path $repoRoot "MngDocument/Infrastructure/MngDocument.Infrastructure/MngDocument.Infrastructure.csproj"

if (-not (Test-Path $infraProj)) {
    throw "Proje bulunamadi: $infraProj"
}

New-Item -ItemType Directory -Path $sampleDir -Force | Out-Null

$buildDir = Join-Path $repoRoot ".tmp-build-reporting-xlsx"
$runnerDir = Join-Path $buildDir "runner"
$runnerProj = Join-Path $runnerDir "ReportingXlsxSeedRunner.csproj"

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

var outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);

var listPath = Path.Combine(outDir, "ODK-RPT-EGITIM-LIST-template-seed.xlsx");
var personPath = Path.Combine(outDir, "ODK-RPT-EGITIM-PERSON-template-seed.xlsx");
var rowPath = Path.Combine(outDir, "ODK-RPT-EGITIM-ROW-template-seed.xlsx");
var certPath = Path.Combine(outDir, "ODK-RPT-EGITIM-CERT-template-seed.docx");
var zimPersonPath = Path.Combine(outDir, "ODK-RPT-ZIMMET-PERSONEL-template-seed.xlsx");
var zimTeslimPath = Path.Combine(outDir, "ODK-RPT-ZIMMET-TESLIM-template-seed.docx");
var zimIadePath = Path.Combine(outDir, "ODK-RPT-ZIMMET-IADE-template-seed.docx");

File.WriteAllBytes(listPath, ReportingOdakEgitimTemplateXlsxFactory.CreateTrainingsList());
File.WriteAllBytes(personPath, ReportingOdakEgitimTemplateXlsxFactory.CreatePersonTrainings());
File.WriteAllBytes(rowPath, ReportingOdakEgitimTemplateXlsxFactory.CreateTrainingDetail());
File.WriteAllBytes(certPath, ReportingOdakEgitimCertificateDocxFactory.Create());
File.WriteAllBytes(zimPersonPath, ReportingZimmetTemplateXlsxFactory.CreatePersonelZimmetDokumu());
File.WriteAllBytes(zimTeslimPath, ReportingZimmetTutanakDocxFactory.CreateTeslim());
File.WriteAllBytes(zimIadePath, ReportingZimmetTutanakDocxFactory.CreateIade());

Console.WriteLine($"OK {listPath} ({new FileInfo(listPath).Length} bytes)");
Console.WriteLine($"OK {personPath} ({new FileInfo(personPath).Length} bytes)");
Console.WriteLine($"OK {rowPath} ({new FileInfo(rowPath).Length} bytes)");
Console.WriteLine($"OK {certPath} ({new FileInfo(certPath).Length} bytes)");
Console.WriteLine($"OK {zimPersonPath} ({new FileInfo(zimPersonPath).Length} bytes)");
Console.WriteLine($"OK {zimTeslimPath} ({new FileInfo(zimTeslimPath).Length} bytes)");
Console.WriteLine($"OK {zimIadePath} ({new FileInfo(zimIadePath).Length} bytes)");
'@ | Set-Content -Path (Join-Path $runnerDir "Program.cs") -Encoding UTF8

Push-Location $runnerDir
try {
    dotnet run --project $runnerProj -c Release -- $sampleDir
    if (-not (Test-Path (Join-Path $sampleDir "ODK-RPT-ZIMMET-PERSONEL-template-seed.xlsx"))) {
        throw "Zimmet XLSX uretilemedi"
    }
    Write-Host "Seed XLSX hazir: $sampleDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
