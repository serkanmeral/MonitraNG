# DEVAM — Workflow + Alarm Engine Planlama (Kaldığımız Yer)

**Son güncelleme:** 2 Haziran 2026, ~00:30
**Durum:** ▶️ Planlama ilerliyor — implementasyona henüz geçilmedi (sadece doküman)

---

## 1. Tek cümlede durum

Workflow Engine'in backend implementasyon planı tamamlandı; ardından bunun monitoring/SIEM ile kesişimi çözüldü ve tespit ihtiyacının workflow'a ait olmadığı görülerek platform geneli **Alarm & Rule Engine** taslağı çıkarıldı. Sıradaki adım: açık kararları kapatıp **Faz 0/1 implementasyonuna** geçmek (workflow veya alarm).

---

## 2. Bu oturumda üretilen / güncellenen dökümanlar

| Dosya | Durum | İçerik |
|-------|-------|--------|
| `docs/odak/workflow/Workflow Backend Implementation Plan v1.md` | **YENİ** | Workflow Engine backend planı (§0–§12) |
| `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md` | **YENİ** | Platform geneli Alarm & Rule Engine planı (§0–§16) |
| `docs/odak/alarm/README.md` | **YENİ** | Alarm odak klasörü indeksi |
| `docs/odak/monitoring/SIEM_PLANNING.md` | Güncellendi | §5, §8, §12.1 → korelasyon = Alarm Engine kararı |
| `docs/odak/monitoring/DEVAM.md` | Güncellendi | Bağımlılık çözüldü; tespit motoru genelleştirildi |
| `docs/content/monitoring_plans/MONITORING_WORKFLOW.md` | Güncellendi | Başa SUPERSEDED notu (IFTTT bölündü) |

> Orijinal tasarım taslakları (`InternalDesign.md`, `...v1_1.md`, `planing.md`) bilinçli olarak değiştirilmedi — kullanıcının düşünce/karar geçmişi.

---

## 3. Kilitli kararlar — Workflow Engine

| # | Konu | Karar |
|---|------|-------|
| 1 | Persistence | Hibrit: Worker → doğrudan Mongo; Definition/Version CRUD (Api) → doğrudan Mongo |
| 2 | Delay/Schedule | MngScheduler (Quartz) uzun delay+schedule; kısa delay (<~1dk) motor-içi bucket kuyrukları |
| 3 | Execution granularity | Per-node (her node ayrı mesaj, context her adımda persist); inline opt. ileriye |
| 4 | Multi-tenancy | Domain-scoped; routing key `{domainId}.*`; instance domainId ile mühürlenir |
| 5 | Servisler | `MngWorkflow.Api` + yeni `MngWorkflow.Worker` (stateless) |
| 6 | Expression engine | Jint (sandbox: timeout+limit, read-only context) |
| 7 | Validation pipeline | Mevcut `ValidationPipelineService` ile birleştirme YOK; ayrı bounded context; Jint paylaşılır |
| 8 | Trigger binding | Version içinde `triggers[]` + indeksli `@workflow_triggers`; many-to-many |
| 9 | Retry | Sabit delay-bucket kuyrukları (5s/30s/2m/10m) + DLX; ≤15dk üst sınır |
| 10 | Webhook auth | Domain-scoped opak key + HMAC imza (`@workflow_secrets`) |
| 11 | Yetki | MngKeeper izinleri + IPermissionEvaluator; worker service identity (IMngKeeperAuthClient deseni) |
| 12 | NextEdges | Tekil `NextEdgeType` → çoğul `NextEdges` (If/Switch/Parallel uyumu) |

İlk teknik hedef: **Manual → If → HTTP → Log** uçtan uca (Faz 1).

---

## 4. Kilitli kararlar — Alarm & Rule Engine

| # | Konu | Karar |
|---|------|-------|
| 1 | Konumlandırma | Platform geneli Alarm & Rule Engine (major §4.2); SIEM korelasyonu = bir kural ailesi |
| 2 | Sınır | Tespit/alarm üretir; aksiyon/orkestrasyon Workflow'a ait (`planing.md` §2) |
| 3 | AI | Ayrı scorer servis(ler); çıktı = sinyal event (kind=signal) → motoru besler |
| 4 | IFTTT | `MONITORING_WORKFLOW.md` bölündü: tespit→Alarm Engine, aksiyon→Workflow; superseded |
| 5 | Runtime | Stream + state (workflow'dan farklı); partition'lı tüketim + Mongo checkpoint |
| 6 | Seam | Alarm event → `mng.alarms` exchange → Workflow Event Trigger tüketir |

---

## 5. Açık kararlar (kapatılacak)

**Workflow:** Büyük karar kalmadı; implementasyona hazır.

**Alarm Engine (PLAN §15):**
1. Servis adı: `MngAlarm` mı `MngCorrelator` mı? (öneri: `MngAlarm`)
2. State store: Mongo checkpoint yeterli mi, yüksek hacim için OpenSearch mı? (SIEM §12.4 ile birleşik)
3. `mon_alarms` erişimi: DG dataset mi, doğrudan Mongo mu?
4. Reactor publish kapsamı: sadece metrik mi, sec_events + signal de mi?
5. Scheduled validation: MngScheduler'a mı yaslanacak?
6. Partitioning: RabbitMQ consistent-hash exchange mi, uygulama seviyesinde mi?

---

## 6. Sonraki adım seçenekleri

1. **Workflow Faz 0/1 implementasyonu:** `MngWorkflow.Worker` host + domain modeli + 4 node (Manual/If/HTTP/Log) + per-node engine → Manual→If→HTTP→Log çalıştır.
2. **Alarm Engine açık kararlarını kapat** (§15) ve Faz 1 (threshold→alarm→workflow) implementasyon planına in.
3. **Monitoring/SIEM'e dön:** SIEM Faz 2 senaryolarını (U1/U2/U4) Alarm Engine kural modeliyle somutlaştır.

---

## 7. Mimari özet (üç katman)

```text
Engine/Reactor (topla+normalize)  →  Alarm & Rule Engine (tespit→alarm)  →  Workflow Engine (orkestrasyon)
                                          ↑ AI scorer (sinyal)
```
Seam'ler RabbitMQ üzerinden gevşek bağlı. Tespit ↔ orkestrasyon ayrımı hem `planing.md` §2 hem major §4.2 ile uyumlu.

---

## 8. İlgili dökümanlar

- Workflow planı: [Workflow Backend Implementation Plan v1](./Workflow%20Backend%20Implementation%20Plan%20v1.md)
- Alarm planı: `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md`
- SIEM: `docs/odak/monitoring/SIEM_PLANNING.md`, `docs/odak/monitoring/DEVAM.md`
- Major vizyon: `docs/odak/operationcore/major_plan.md` §4.2
- Orijinal workflow taslakları: `InternalDesign.md`, `MonitraNG Workflow Runtime Internal Design v1_1.md`, `planing.md`
