# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 5 Ağustos 2026 (Scenario Studio Flow Lab · Basit olay kaynağı: EventLog Windows/Linux · Metrik eşiği)  
**Ortam:** Günlük çalışma = **Odak production** (`192.168.20.8`). Lokal Nuxt varsayılanı prod.  
**Detay:** [SCENARIO_STUDIO_SIMPLE_SOURCE.md](../alarm/SCENARIO_STUDIO_SIMPLE_SOURCE.md) · Events UI: [../monitoring/SIEM_EVENTS_UI.md](../monitoring/SIEM_EVENTS_UI.md)

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
**Freeze:** Eski SIEM security paneli · NXLog / Linux rsyslog host ingest · intent-only filter dialog  

---

## Son çalışılan konu

**Scenario Studio / Flow Lab — basit olay kaynağı UX.**  
Generic teknik alanlar “Gelişmiş” altında; kullanıcı Platform → Kanal → Olay/Metrik → Host ile flow kuruyor. Managed filtre/condition node’ları otomatik üretiliyor. **Mng.Ui production deploy yapılmadı.**

---

## Tamamlananlar (bu dilim — 5 Ağu, akşam)

### Basit olay kaynağı ✓

- Inspector: platform (Windows/Linux/Other) · kanal (EventLog/Metrik/App) · host multiselect (Hepsi + discovery).
- **Windows EventLog:** `AcEventSelectorField` modal tablo (channel dictionary + özel Event ID örn. 65002).
- **Linux EventLog:** aynı modal (journal paket + `event.action` + özel paket/action).
- **Metrik:** metrik + operatör (gt/gte/lt/lte/eq/neq) + eşik → otomatik **condition** node (`value …`).
- Managed scope: OS / eventCode / host / metric; kaynak silinince birlikte silinir.
- Backend: `ScenarioSource.matchKeys`, `in` operatörü güçlendirmesi, V3 aday sorgu `$or`.

### Önceki (aynı gün / önceki dilimler) ✓

- Scenario v3 graph backend · katalog ağacı · boş canvas · RDP TERMINAL örnek senaryo · Flow Lab layout  
- Discovery domain ayarları · FortiGate Events UX · filtre katalog  

Detay: [../alarm/SCENARIO_STUDIO_SIMPLE_SOURCE.md](../alarm/SCENARIO_STUDIO_SIMPLE_SOURCE.md)

---

## Deploy durumu

| Bileşen | Durum |
|---------|--------|
| `mngalarm` / worker | Önceki v3 dilimleri prod’da olabilir; bu akşamki UI+matchKeys değişiklikleri **commit sonrası deploy bekliyor** |
| `Mng.Ui` Flow Lab / basit kaynak | ✅ lokal · ⏸️ **prod deploy yok** |
| `mnglogcollector` / `mngreactor` | Önceki dilimler (değişmedi) |

---

## Sıradaki adım

1. **Uygulama/Servis** kanalı basit UX (veya Alarm/Stop çıktı node’ları).
2. Basit condition/aggregation sadeleştirmesi (opsiyonel).
3. UI (+ gerekirse Alarm) kontrollü prod deploy.
4. Park maddeleri (UTM/VPN, new_flow, …) ayrı dilim.

---

## Nerede kalmıştık

Flow Lab üzerinde **basit olay kaynağı** Windows EventLog + Linux journal + Metrik eşiği tamamlandı.  
**Kaldığımız nokta:** App/Servis kanalı veya çıktı node’ları (Alarm/Stop) ile devam; ardından UI deploy kararı.
