# Faz 3.1: FortiGate deny-only IT checklist (Musteri IT ekibi icin)
# Kod degisikligi yapmaz; operasyon adimlarini listeler.
param(
    [switch]$RunVolumeAnalysis
)

$ErrorActionPreference = 'Stop'

Write-Host '=== FortiGate Deny-Only IT Checklist (Faz 3.1) ===' -ForegroundColor Cyan
Write-Host @'

Amac: SIEM ingest hacmini dusurmek (allowed_flow ~252K/24s prod gozlemi).
Parser: firewall.vendor.v1 | Syslog UDP :541/542 (IT relay)

ADIMLAR (FortiGate GUI):
  1. Log & Report > Log Settings
     - Traffic: "Allow" loglari kapat VEYA sampling (or. 1/100)
     - Traffic: "Deny" loglari acik birak
  2. Log & Report > Forwarding > Syslog (SIEM relay)
     - Yalnizca security / traffic deny profillerini gonder
  3. FortiAnalyzer varsa: ayni filtre politikasini senkronize et
  4. Degisiklik sonrasi 1 saat bekle; asagidaki analiz scriptini tekrar calistir

Beklenen kazanim:
  - EPS 10-100x dusus potansiyeli (allow log kapali ise)
  - dashboard-summary DB yuku azalir
  - sec_events disk/indeks buyumesi yavaslar

Dogrulama (MonitraNG):
  pwsh -File .\scripts\odak\analyze-prod-sec-events-volume.ps1 -RangeHours 24

Rollback:
  - Allow loglari tekrar ac (or. denetim donemi)
  - Relay filtrelerini geri al

Not: MngEngine edge filtresi (Faz 3.2) IT degisikligi yapilamazsa yedek katmandir.

'@ -ForegroundColor White

if ($RunVolumeAnalysis) {
    Write-Host "`nHacim analizi calistiriliyor..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'analyze-prod-sec-events-volume.ps1')
}
