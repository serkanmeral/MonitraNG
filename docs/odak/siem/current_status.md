# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 6 Ağustos 2026 (Scenario Studio Flow Lab · Palette grupları · Debug output)  
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

**Scenario Studio / Flow Lab — çıktı node planı + Debug MVP + palette grupları.**  
Basit olay kaynağı (EventLog/Metrik) önceki dilimde tamamdı. Bu dilimde palette Olaylar/Fonksiyonlar/Çıktılar gruplandı; **Debug** node (sim-only) eklendi. Bildirim (mail) ve OC WI planlandı ama kodlanmadı. **Prod deploy yapılmadı.**

---

## Tamamlananlar (bu dilim — 6 Ağu)

### Palette gruplama ✓

- Node paleti: **Olaylar** · **Fonksiyonlar** · **Çıktılar**
- Grup başlıkları collapse/expand
- Dosya: `Mng.Ui/components/apps/alarm-center/AcFlowLab.vue`

### Debug output (`debug-output`) ✓

- Palette Çıktılar altında; terminal node (çıkış portu yok)
- Config: `mode` (`complete` | `path`), `path`, `active`
- Path: `value` / `key` / `kind` / `timestamp` veya `dimensions.*` (düz alan adı da dimensions’a düşer)
- Complete: observation özeti (`kind`, `key`, `value`, `timestamp`, `dimensions`)
- **Yalnızca simulate/preview** — prod eval yan etki/log yok
- `graph.output.required` için sayılmaz (Alarm veya Stop şart)
- Preview API: `debugLines[]` (kronolojik)
- UI: simülasyon panelinde debug listesi
- Test: `ScenarioGraphV3Tests` (debug emit + yalnız-debug reject)

### Çıktı node planı (konuşuldu, kod yok) 📋

| Node | Karar | Durum |
|------|--------|--------|
| Alarm | Mevcut; bağımsız (diğer çıktılar Alarm’sız da çalışabilir) | ✓ var |
| Stop | Ayrı terminal; yan etkisiz | ✓ var |
| Debug | Sim-only (Node-RED benzeri) | ✓ bu dilim |
| **Bildirim** | MVP: **yalnız mail**; Telegram/inApp sonra | ⏸ sıradaki |
| **OC Work Item** | Workspace + özelliklerle WI; ayrı dilimde konuşulacak | ⏸ sonra |
| Bağımsızlık | Bildirim/WI Alarm’a bağlı değil | ✅ karar |

---

## Önceki dilim (5 Ağu) — basit olay kaynağı ✓

- Platform / kanal / EventLog (Win+Linux) / Metrik / host
- Managed: `__scope-os`, `__scope-eventcode`, `__scope-host`, `__scope-metric`
- Backend: `matchKeys`, `in`, V3 `$or`
- Commit: `7f3f3855`

Detay: [../alarm/SCENARIO_STUDIO_SIMPLE_SOURCE.md](../alarm/SCENARIO_STUDIO_SIMPLE_SOURCE.md)

---

## Deploy durumu

| Bileşen | Durum |
|---------|--------|
| `mngalarm` (matchKeys + **debug-output**) | Kod commit’lenecek; **prod deploy yok** |
| `Mng.Ui` Flow Lab (palette + debug + basit kaynak) | Lokal · **prod deploy yok** |
| `mnglogcollector` / `mngreactor` | Değişmedi |

> Debug satırları için Flow Lab simulate, güncel **MngAlarm** API ister.

---

## Sıradaki adım (buradan devam)

1. **Bildirim node (mail MVP)** — yapı konuşulup implement (Notifier mail API; Alarm’dan bağımsız).
2. Alarm / Stop basit UX (opsiyonel, paralel).
3. **App/Servis** kanalı basit UX (preset → live/staleness).
4. OC Work Item çıktısı — ayrı planlama dilimi.
5. Kontrollü `Mng.Ui` + `MngAlarm` prod deploy (açık talepte).

---

## Nerede kalmıştık

Flow Lab: palette grupları + **Debug** tamam.  
**Kaldığımız nokta:** Bildirim (mail MVP) node yapısını konuşup geliştirmek; WI ve Telegram sonra. Deploy yok.
