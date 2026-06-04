# Monitoring → SIEM (Odak)

Müşteri ortamında **güvenlik odaklı izleme / SIEM-hafif** çözümünün planlama ve teslim dokümanları.

**Durum:** Faz 0 ✅ · **Faz 1 spike başlatılabilir** (workflow seam ✅) ▶️
**Son güncelleme:** 3 Haziran 2026

---

## Kapsam kararı (kilitli)

| Konu | Karar |
|------|-------|
| **Ürün kapsamı** | SIEM-hafif: hedefli senaryolar |
| **AI** | Implementasyon ⏸️ — [AI_PLANNING_DECISION.md](../AI_PLANNING_DECISION.md) |
| **Toplama** | Hibrit: syslog · WEF→WEC · agent ([§5](./SIEM_PLANNING.md#5-mimari-akış)) |
| **Engine syslog** | Collector/listener — tam syslog sunucusu değil |
| **Tespit** | Alarm & Rule Engine (Faz 2) |
| **Dağıtım** | On-prem |

---

## Dokümanlar

| Dosya | İçerik | Durum |
|-------|--------|--------|
| [SIEM_PLANNING.md](./SIEM_PLANNING.md) | Ana plan (gap, şema, toplama, U1–U7, fazlar) | ✅ Faz 0 |
| [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md) | Parser/normalizer pipeline | ✅ |
| [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) | **Faz 1 implementasyon planı** | ▶️ MngEngine/MngReactor |
| [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md) | Harici repo handoff | ✅ |
| [SIEM_WORKFLOW_SEAM.md](./SIEM_WORKFLOW_SEAM.md) | Workflow × SIEM seam | ✅ |
| [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) | Faz 2 observation eşlemesi | ✅ tasarım |
| [MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md](./MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md) | MngReactor dosya/PR planı | ▶️ |
| [MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md) | Odak deploy (stub→gerçek) | ✅ C6 |
| [HANDOFF.md](./HANDOFF.md) | **Yeni chat handoff + prompt** | ✅ |
| [SIEM_THROUGHPUT_AND_QUEUES.md](./SIEM_THROUGHPUT_AND_QUEUES.md) | Kuyruk, paralellik, yoğun veri | ✅ plan |
| [SIEM_PERFORMANCE_PLAN.md](./SIEM_PERFORMANCE_PLAN.md) | **§2 mimari öneriler**, SLO, profiller, benchmark, quality gates | ✅ plan |
| [SIEM_EVENTS_UI.md](./SIEM_EVENTS_UI.md) | Güvenlik olay arama UI (MVP) | ✅ |
| [SIEM_WEF_WEC_INGEST.md](./SIEM_WEF_WEC_INGEST.md) | WEF→WEC → Engine HTTP batch | ✅ S5 |
| [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) | **LogAlarm vs MonitraNG** kıyaslama; parite ayrı hedef | ✅ referans |
| [SIEM_VERTICAL_FINANCE.md](./SIEM_VERTICAL_FINANCE.md) | Dijital banka / finans dikey kapsam | Taslak |
| [DEVAM.md](./DEVAM.md) | Kaldığımız yer | ▶️ |

---

## Dikey notlar

| Dikey | Doküman |
|-------|---------|
| Savunma / OT–IT sınır | SIEM §6 + CYBERSECURITY §9 |
| Finans / dijital banka | [SIEM_VERTICAL_FINANCE.md](./SIEM_VERTICAL_FINANCE.md) |

---

## İlişkili dokümanlar

| Konu | Konum |
|------|-------|
| Siber güvenlik vizyonu | `docs/content/security/CYBERSECURITY_SOLUTION_PLANNING.md` |
| Alarm engine | `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md` |
| Monitoring mimarisi | `docs/content/monitoring_plans/` |

---

## Hızlı bağlantılar

- [../README.md](../README.md) · [../compliance/README.md](../compliance/README.md)
