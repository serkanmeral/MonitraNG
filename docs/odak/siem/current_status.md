# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 30 Temmuz 2026 (Discovery mimari kilit + Domain LDAP; SIEM settings/discovery UI)  
**Ortam notu:** Odak production `odak@192.168.20.8`; merkezi Mng.Ui local `npm run dev`; UI prod deploy sadece istekte.  
**Canlı pilot:** `MngLogs.Agent` → collector `http://192.168.20.8:5091`; **Türkçe Nuxt UI** `http://127.0.0.1:5092/`; hostId=`TERMINAL-pilot`. Event Log: admin bekleniyor.

## Çalışma kuralı (bu oturumdan itibaren)

Her implementasyon adımından **önce**:
1. Ne yapılacağı (kısa kapsam)
2. Ne kazanılacağı (somut fayda)
3. Kullanıcı onayı → sonra kod / değişiklik

Onaysız büyük adım yok. Bu dosya **yapılan / yapılacak** listesinin güncel kaynağıdır.

**Park:** MngLogs P5 Event Log parser — kodlama yok; sonra dönülecek.

---

## Son çalışılan konu

**Network Discovery mimari + Domain `directoryLdap` + SIEM Center settings/discovery UI.**  
Önceki kapanış: MngLogs Faz 1 metrik toplama ✓.

---

## Bu oturumda tamamlananlar

### SIEM Center — Settings MVP ✓

- Route: `/apps/siem-center/settings` · menü: **SIEM ayarları**
- Sekmeler: Catalog (live collector `GET /api/v1/policy/eventlog-packages`) · Sources · Scenarios · Dictionary
- Nuxt BFF: `server/api/logcollector/[...path].ts` → `:5091`
- `/reference` → `settings?tab=dictionary`
- Eski SIEM security paneli (**dokunulmaz**)

### SIEM Center — Discovery UI (mock + coverage) ✓

- Route: `/apps/siem-center/discovery`
- Topoloji görünümleri: Tree · Split · Tiers · Graph (`localStorage`)
- Coverage: `host.up` (15 dk stale → Online/Offline); Live agents dalı
- KPI kartları hâlâ mock görüntü (bilinçli)
- Plan ref: `network_discovery_bf70a6d6.plan.md` (MVP kararları kilitli)

### Domain LDAP (`directoryLdap`) ✓

- Keeper: `DomainSettings.directoryLdap` (host, port, useSsl, baseDn, bindUsername, bindPassword — düz metin)
- `PUT /api/domain/{id}` → `UpdateDomainRequest` + **merge** (privileges/ldap silinmez; boş parola → eski kalır)
- UI: Domain sayfası **Dizin / LDAP (AD)** kartı
- Odak prod Keeper deploy ✓; Mongo `odak` için LDAP + privileges onarıldı:
  - Bind: `monitra@odak.local` @ `192.168.20.3:389` · Base `DC=odak,DC=local`
  - Privileges: admin `MonitraNG Users`, manager `MonitraNG Admins`
- Keycloak federation bind ile aynı hesap

### Discovery mimari — kilit (konuşma, henüz backend kod yok)

| Katman | Karar |
|--------|--------|
| Credential | Keeper `settings.directoryLdap` |
| İş + store + UI API | **MngLogCollector** (sürekli ayakta) |
| Periyodik tetik | **MngScheduler** → Collector HTTP |
| Coverage (C) | Agent + `host.up` (mevcut) |
| MngAdmin | **Kullanılmayacak** (episodik ops; Discovery evi değil) |
| Yeni mikroservis | Şimdilik yok |

**İlk dilim (MVP-A1) — sıradaki implementasyon:** AD computer pull → Mongo `discovery_hosts` → liste/özet API → UI mock→live. DHCP/ICMP sonra.

---

## Yapılanlar (önceki — özet)

### Planlama — Master + alt planlar 1–11 (üst seviye kilitli)

Discovery (#2): AD/DHCP + sınırlı tarama; coverage = metrik.

### G0–G3 + MngLogs Faz 1 ✓

Collector `mnglogcollector:5091`; ajan Event Log + metrik + service watch; yerel Durum UI; üst süreç ship. Detay: git history / önceki sürümler.

---

## Bilinçli erteleme / sonra

- Discovery A1 implementasyonu (Collector + Scheduler + UI live)
- DHCP / sınırlı ICMP (katman B)
- Yeni SIEM security panel (greenfield Detect hub; eski panel frozen)
- Discovery KPI → live `dashboard-summary`
- SNMP topoloji
- MngLogs P5 parser (park)
- Catalog CRUD
- G2b MngAdmin OpenSearch snapshot; G4 cutover
- UI prod deploy (istekte)
- MSI+GPO saha (kısmen ilerledi — ayrı dal)

---

## Sıradaki adım (yeni chat)

1. **Discovery A1 şema + API sözleşmesi** (chat’te kilitle) → Collector implement
2. Scheduler system job: periyodik `POST .../discovery/sync`
3. UI Discovery: mock → live `discovery_hosts` + coverage birleşimi

Onay kapısı: implement öncesi kapsam/kazanım.

---

## Nerede kalmıştık (handoff özeti)

- LDAP credential domain’de hazır (Odak prod Mongo + Keeper merge fix deploy).
- Discovery **yeri kilitli:** Collector + Scheduler; MngAdmin değil.
- UI Discovery mock ayakta; backend AD pull **henüz yok**.
- MngLogs park; eski SIEM panel freeze.

---

## Plan dosyaları

`C:\Users\monitra\.cursor\plans\`

- `siem_master_plan_1d443b06.plan.md`
- `mnglogs_siem_vizyonu_2d47d54f.plan.md`
- `network_discovery_bf70a6d6.plan.md`
- `opensearch_siem_store_a1b2c3d4.plan.md`
- `normalize_map_catalog_e7f8a9b0.plan.md`
- `detect_correlation_c1d2e3f4.plan.md`
- `siem_ui_a4b5c6d7.plan.md`
- `rsyslog_reuse_b8c9d0e1.plan.md`
- `trtest_gap_c3d4e5f6.plan.md`
- `siem_gecis_cutover_d4e5f6a7.plan.md`
- `siem_lisans_olcum_e5f6a7b8.plan.md`
- `siem_managed_telemetry_f6a7b8c9.plan.md`
