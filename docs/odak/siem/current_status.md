# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 8 Ağustos 2026  
**Ortam:** Günlük çalışma = **Odak production** (`192.168.20.8`).  
**Detay:** [AGENT_OBSERVATION_AND_FLOW_LAB.md](../alarm/AGENT_OBSERVATION_AND_FLOW_LAB.md) · [SCENARIO_STUDIO_SIMPLE_SOURCE.md](../alarm/SCENARIO_STUDIO_SIMPLE_SOURCE.md)

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- Host Analytics L3 / genel Analytics dönüşü  
- Ajansız host aksiyonları (Discovery)  
- Firewall parse kurallarının katalog seed’e taşınması (hâlâ C# `firewall.vendor.v1`)  
- UTM/VPN parse · `new_flow` baseline · allowed-flow volume policy  
- Periyodik discovery scan · Hard publish · Host paket ataması (E3)  
- UI’den parametreli agent indir  
- Parser dictionary redesign  
- Semantik Event ID → observation key kataloğu (opsiyonel; yayın yolu paket key)  
**Freeze:** Eski SIEM security paneli · NXLog / Linux rsyslog host ingest · intent-only filter dialog  

---

## Son çalışılan konu

**Agent EventLog → Alarm + Flow Lab işletimi.**  
Collector observation key = paket id (RDP semantik opsiyonel). Flow: Açık kilit / Kapalı düzenle; alarm birleştirme + gruplama. PowerShell Alerts v3 Odak’ta açık (`powershell-engine` + host `TERMINAL`).

---

## Tamamlananlar (7–8 Ağu 2026)

### Collector → `monitra.observations` ✓

- `AgentObservationPublisher` + mapper; allowlist `*`
- RDP: `21/23/24/25` → `rdp.*`; diğer paketler paket id (örn. `powershell-engine`)
- `sourceHost` = kısa hostname (`TERMINAL`)
- Event ID `dimensions.eventCode`
- Odak: `mnglogcollector` deploy

### MngAlarm runtime ✓

- Queue bind `*.event.#` (noktalı key)
- `in` filtresi: JSON dizi korunur
- `ScenarioDedup.mergeEnabled` + graph executor
- Severity: version ↔ alarm-output node senkron
- Odak: `mngalarm` + `mngalarm-worker` deploy

### Flow Lab UX ✓

- Sözlük: Taslak / Yayında · Açık|Kapalı / Arşiv
- Yayınla = Kapalı kalır; Aç/Kapat ayrı
- Açık flow düzenlenemez
- Alarm node inspector: birleştir + kapsam (tümü / host / kullanıcı / özel)
- Global toaster stack (kart alert kalktı)
- UI: `mngui` deploy **bekliyor** (kullanıcı onayı)

### PowerShell Alerts (prod) ✓

- v3, Açık; `matchKey=powershell-engine`; host=`TERMINAL`; Event ID 400/403/600
- Sentetik ingest → alarm raised

### Önceki (6 Ağu) ✓

- Palette + debug-output · işletim status/health · execution log (son 100)

---

## Deploy durumu

| Bileşen | Durum |
|---------|--------|
| `mnglogcollector` | ✅ Odak prod (paket key, `SourceProducts=*`) |
| `mngalarm` + worker | ✅ Odak prod |
| `mngui` | Lokal hazır · **prod deploy yok** (onayla) |

---

## Sıradaki adım

1. `mngui` prod deploy (Flow Lab / toaster / birleştirme UI)  
2. İsteğe bağlı: semantik Event ID kataloğu  
3. Bildirim node (mail MVP)  
4. Flow migration kuyruğu U1+ onayla Aç — [FLOW_MIGRATION_QUEUE.md](../alarm/FLOW_MIGRATION_QUEUE.md)

---

## Nerede kalmıştık

Collector + Alarm backend Odak’ta; PowerShell Alerts çalışıyor.  
**Kaldığımız nokta:** `mngui` deploy + yeni chat’te Flow Lab UI doğrulama / semantik katalog veya bildirim node.
