# DEVAM — SIEM-Hafif Planlama (Kaldığımız Yer)



**Son güncelleme:** 3 Haziran 2026  

**Durum:** ▶️ **SIEM-ready checkpoint ✅** · SIEM Faz 1 implementasyon sırada  

**Handoff (yeni chat):** [HANDOFF.md](./HANDOFF.md)



---



## 1. Tek cümlede durum



Faz 0 planlama ✅. **C6/C7 ✅.** UI modül ayrımı (`6c4ecbf`). **SIEM `sec_events` implementasyonu ayrı chat'te** — bu dosya plan + handoff. Workflow `mqtt/publish` Reactor'da eksik.



---



## 2. Dosya haritası



| Dosya | Rol |

|-------|-----|

| [HANDOFF.md](./HANDOFF.md) | **Yeni chat prompt + oturum özeti** |

| [SIEM_PLANNING.md](./SIEM_PLANNING.md) | Ana plan (§1–23) |

| [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md) | Parser/normalizer |

| [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) | Faz 1 implementasyon |

| [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md) | MngEngine + MngReactor kabul kriterleri |

| [MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md](./MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md) | Dosya ağacı + PR-1…PR-6 |

| [MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md) | Odak stub → gerçek deploy |

| [SIEM_WORKFLOW_SEAM.md](./SIEM_WORKFLOW_SEAM.md) | Workflow × SIEM ✅ |

| [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) | Faz 2 observation eşlemesi |

| [SIEM_THROUGHPUT_AND_QUEUES.md](./SIEM_THROUGHPUT_AND_QUEUES.md) | Kuyruk, yoğun veri |

| [SIEM_PERFORMANCE_PLAN.md](./SIEM_PERFORMANCE_PLAN.md) | SLO, benchmark |

| [benchmarks/README.md](./benchmarks/README.md) | Benchmark klasörü (boş) |

| [README.md](./README.md) | İndeks |



---



## 3. MngReactor durumu (3 Haz 2026)



| Konu | Durum |

|------|--------|

| Kaynak | ✅ `MngReactor/` monorepo (submodule değil) |

| Monitoring metrik ingest | ✅ kodda var |

| SIEM `sec_events` | ⬜ PR planı hazır, kod yok |

| Odak deploy | ✅ `mngreactor:latest` — [checklist](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md) |

| Workflow `mqtt/publish` | ⬜ endpoint eksik; Odak `DevLogOnly=true` |

| Observation native publish | ✅ C6 — [REACTOR_NATIVE_PUBLISH_HANDOFF](../alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md) |

| Yeniden inşa? | ❌ Gerek yok |



---



## 4. Paralel planlama



| İş | Durum |

|----|-------|

| Fixture (4625, 4624, deny, unparseable) | ✅ `tests/fixtures/siem/` |

| U1/U2/U4 alarm kural taslağı | ✅ `tests/fixtures/siem/alarm_rules/` |

| Workflow SIEM seam | ✅ |

| Observation map | ✅ |

| Performans baseline | ⬜ spike sonrası |

| Finans dikeyi | ⬜ isteğe bağlı |



---



## 5. Spike kararları (D1–D5)



| # | Karar |

|---|--------|

| D1 | `kind=sec_event` (Faz 1.1 birleşik batch; Faz 1 route: `/ingest/sec-events`) |

| D2 | Windows fixture first |

| D3 | Syslog UDP MVP |

| D4 | Mongo `sec_events` |

| D5 | Retention test: TTL yok veya 30 gün |



---



## 6. Sıradaki adımlar



1. ~~Odak MngReactor deploy~~ ✅ — [MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md)

2. Reactor `POST /api/v1/mqtt/publish` (workflow gerçek block_ip)

3. ~~PR-O1…O3 observation publish (Alarm C6)~~ ✅

4. SIEM PR-1…PR-6 — [MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md](./MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md)

5. MngEngine syslog S3 — [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md)

6. Spike sonrası: `test-siem-faz1-e2e.ps1`, benchmarks, U1 E2E



---



## 7. İlgili DEVAM dosyaları



- [workflow/DEVAM.md](../workflow/DEVAM.md) — P4 engine.command, parallel.fork

- [alarm/DEVAM.md](../alarm/DEVAM.md) — Faz 2 correlation, C6 native observation

- [../deploy/README.md](../deploy/README.md)

