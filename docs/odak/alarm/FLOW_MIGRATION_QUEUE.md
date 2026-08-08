# SIEM / Alarm → Flow Lab migration queue (Odak)
# Update as each step is approved.

**Son güncelleme:** 8 Ağustos 2026  
**İlke:** Alarm üretimi yalnız **onaylanmış + Açık** flow’lardan. Yayınla ≠ Aç.  
**Durum:** Tüm üretim durdurulur; mevcut alarmlar temizlenir; U1…U10 teker teker açılır.

---

## Operasyon sırası

| # | Adım | Script / iş | Durum |
|---|------|-------------|--------|
| 0 | Tüm kural + flow alarm üretimini durdur | `scripts/odak/stop-all-alarm-production.ps1 -Apply` | ✓ test `.20` + **prod `.8`** (7 kural OFF) |
| 1 | Açık alarmları kapat / temizle | resolve (test) · Mongo wipe (prod ~157k) | ✓ test + **prod `@mon_alarms` 157717 silindi** |
| 1b | (Opsiyonel) Mongo hard wipe | `purge-open-alarms.ps1 -Apply -HardDeleteMongo` | ⏸ talepte |
| 2 | Publish ≠ Aç + MngAlarm deploy | Publish `enabled=false`; `mngalarm` + worker | ✓ 6–8 Ağu 2026 (`*.event.#`, merge) |
| 3 | U1 v3 flow (login_failed / brute force) | Fixture `siem-ops-v3` + seed → review → **Aç** | ✓ yayınlı **Kapalı** (`ffb70bb1…`); ⏳ onay |
| 4 | U2 sequence | aynı | ⏸ |
| 5 | U3–U7 (mvp pack) | aynı | ⏸ |
| 6 | U8–U10 | aynı | ⏸ |
| 7 | Scheduled / metric CRUD | Flow’a taşı | ⏸ |
| 8 | Rules UI SIEM freeze | Yeni kural → Flow Lab yönlendirme | ⏸ |
| 9 | Legacy evaluator sunset | feature-flag | ⏸ |

---

## Onay modeli

1. Flow taslak → doğrula → **yayınla** (**Kapalı** kalır).  
2. Review / E2E.  
3. Operatör **Aç** (`enabled=true`).  
4. Legacy eşdeğer kural `enabled=false` (silme sonra).

---

## Referans

- Checkpoint: [../siem/current_status.md](../siem/current_status.md)  
- Studio: [SCENARIO_STUDIO_SIMPLE_SOURCE.md](./SCENARIO_STUDIO_SIMPLE_SOURCE.md)
