# DEVAM — SIEM-Hafif Planlama (Kaldığımız Yer)

**Son güncelleme:** 3 Haziran 2026
**Durum:** ▶️ Planlama devam — **Faz 1 spike MngEngine/MngReactor'da başlatılabilir** (workflow seam ✅)

---

## 1. Tek cümlede durum

Faz 0 planlama bitti. **Workflow SIEM seam hazır** (P4-A/B Odak E2E). **Implementasyon:** harici repolarda Faz 1 spike; MonitraNG tarafında fixture + kural taslağı + seam dokümanı tamamlandı. Yoğun veri: [SIEM_THROUGHPUT_AND_QUEUES.md](./SIEM_THROUGHPUT_AND_QUEUES.md). **AI:** implementasyon ⏸️ — [AI_PLANNING_DECISION.md](../AI_PLANNING_DECISION.md).

---

## 2. Dosya haritası

| Dosya | Rol |
|-------|-----|
| [SIEM_PLANNING.md](./SIEM_PLANNING.md) | Ana plan (§1–23) |
| [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md) | Parser/normalizer |
| [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) | Faz 1 implementasyon |
| [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md) | MngEngine/MngReactor handoff |
| [SIEM_WORKFLOW_SEAM.md](./SIEM_WORKFLOW_SEAM.md) | Workflow × SIEM seam değerlendirmesi ✅ |
| [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) | Faz 2 observation eşlemesi |
| [SIEM_THROUGHPUT_AND_QUEUES.md](./SIEM_THROUGHPUT_AND_QUEUES.md) | Kuyruk, paralellik, yoğun veri |
| [SIEM_PERFORMANCE_PLAN.md](./SIEM_PERFORMANCE_PLAN.md) | Performans — **§2 öneriler**, SLO, benchmark |
| [SIEM_VERTICAL_FINANCE.md](./SIEM_VERTICAL_FINANCE.md) | Dijital banka kapsam (sonra gözden geçirilecek) |
| [benchmarks/README.md](./benchmarks/README.md) | Benchmark çıktı klasörü (boş) |
| [README.md](./README.md) | İndeks |

---

## 3. Faz 1 spike — ▶️ başlatılabilir

Detay: [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) · Handoff: [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md)

**Workflow kapısı:** ✅ [SIEM_WORKFLOW_SEAM.md](./SIEM_WORKFLOW_SEAM.md)

---

## 4. Paralel (planlama / veri)

| İş | Durum |
|----|-------|
| Örnek log fixture (4625, 4740, deny, unparseable) | ✅ `tests/fixtures/siem/` |
| U1/U2/U4 → `mon_alarm_rules` JSON taslağı | ✅ `tests/fixtures/siem/alarm_rules/` |
| `sec_events` → observation map | ✅ [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) |
| Workflow SIEM seam değerlendirmesi | ✅ [SIEM_WORKFLOW_SEAM.md](./SIEM_WORKFLOW_SEAM.md) |
| Performans baseline (P0/P1) | ⬜ spike sonrası → `benchmarks/` |
| Finans dikeyi gözden geçirme | ⬜ isteğe bağlı |

---

## 5. Spike kararları (onaylı — handoff ile uyumlu)

| # | Karar | Değer |
|---|-------|-------|
| D1 | Ingest discriminator | `kind=sec_event` ✅ |
| D2 | Windows yolu | Fixture first (B); WEC Odak paralel |
| D3 | Syslog | UDP MVP |
| D4 | DB | Mongo `mng_{domain}` |
| D5 | Retention spike | TTL yok veya 30 gün test |

---

## 6. Sıradaki adımlar (öncelik)

1. **MngReactor/MngEngine:** [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md) S1–S4 implementasyonu
2. Spike bitince: `SEC_EVENT_OBSERVATION_MAP` publish + U1 kural Odak E2E
3. `test-siem-faz1-e2e.ps1` + `benchmarks/` doldurma
4. U2 `sequence` kural tipi — Alarm Faz 2+ backlog
5. Finans dikeyi gözden geçirme — istenirse

---

## 7. İlgili dokümanlar

- [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md)
- [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md)
- [workflow/DEVAM.md §P4](../workflow/DEVAM.md)
- [alarm/DEVAM.md](../alarm/DEVAM.md)
- `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md`
