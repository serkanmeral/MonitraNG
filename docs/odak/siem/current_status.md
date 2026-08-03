# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 3 Ağustos 2026 (host telemetry cutover + RDP + prod sabitleme)  
**Ortam:** Günlük çalışma = **Odak production** (`192.168.20.8`). Lokal Nuxt ve MngLogs agent collector varsayılanı prod.  
**Detay cutover:** [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md)

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- Host Analytics L3 / genel Analytics dönüşü  
- Ajansız host aksiyonları (Discovery)  
- Firewall parse kurallarının katalog seed’e taşınması (hâlâ C# parser)  
- Periyodik discovery scan · Hard publish · Host paket ataması (E3)  
- UI’den parametreli agent indir  
- G4 kalan: alarm/Mongo köprü (gözlem)  
**Freeze:** Eski SIEM security paneli · NXLog / Linux rsyslog host ingest.

---

## Son çalışılan konu

**Host telemetrisi = yalnızca MngLogs agent** (NXLog + Linux syslog Engine/Reactor kapalı) · RDP `event.action` normalize · Güvenlik Olayları RDP filtresi · lokal UI/agent **prod** sabitleme.

---

## Tamamlananlar (bu dilim)

### Cutover ✓

- Engine/Reactor: `AcceptNxlogIngest=false`, `AcceptLinuxSyslogIngest=false`, WEC kapalı  
- Compose: NXLog/Linux UDP portları kaldırıldı; FortiGate `:541/:542` kaldı  
- Agent → LogCollector → OpenSearch; Reactor `OpenSearchReadEnabled`  
- RDP: LogCollector normalizer (21/23/24/25 → `rdp.*`); Reactor/UI prefix genişletmesi  

### SIEM Events UI ✓

- Akıllı arama / intent filtreleri (`secEventFilterIntents`, RDP → `eventActionPrefix=rdp.`)  
- `shortHostKey` tek kaynak (`siemDiscoveryHostMatch.ts`)  

### Ortam disiplini ✓

- `Mng.Ui` Nuxt `ODAK_HOST` fallback + `.env` → **192.168.20.8**  
- Windows/Linux agent scriptleri varsayılan prod Collector  
- `deploy-agent-odak-prod.ps1` · `retarget-collector-elevated.ps1`  
- Test scriptleri bilinçli test için ayrıldı (`*-odak-test.ps1`)  

### Önceki (P5) ✓

Parse Rules katalog + Windows/Linux sihirbazları + Settings Event Log IA — [PARSE_RULES_CATALOG.md](./PARSE_RULES_CATALOG.md)

---

## Sıradaki adım

1. Prod üzerinde RDP / host olaylarının SIEM Events’te doğrulanması (agent → OS → UI)  
2. İsteğe bağlı: prod `mngui` deploy (local Nuxt yeterse atlanabilir)  
3. Park: Analytics L3 · Discovery · firewall katalog · G4 alarm köprü  

---

## Nerede kalmıştık

Cutover + RDP normalize + prod sabitleme kod/doküman hazır.  
**Kaldığımız nokta:** yeni chat’te prod SIEM doğrulama ve park maddeleri; test/prod ortam karıştırmadan devam.
