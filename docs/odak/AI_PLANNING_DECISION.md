# Yapay Zeka — Planlama Kararı (Odak)

**Durum:** ✅ **Kilitli karar** — 3 Haziran 2026  
**Kaynak:** Major Plan [§8](./operationcore/major_plan.md) · Alarm [§9](./alarm/ALARM_RULE_ENGINE_PLAN.md) · On-prem [§3.4](./operationcore/major_plan.md)

---

## 1. Karar (tek cümle)

**AI mimari çerçevesi ve AI-ready veri izleri şimdi sabitlenir; AI ürün geliştirmesi (scorer, RAG, workflow AI node, güvenlik özeti) çekirdek hat canlı olana kadar başlamaz.**

---

## 2. Üç katman — ne zaman?

| Katman | Kapsam | Zaman | Durum |
|--------|--------|-------|-------|
| **A — Çerçeve** | Scorer vs copilot, yerleşim, on-prem ilkesi, ingest’e AI yok | **Şimdi** | ✅ Bu doküman + major §8 + alarm §9 |
| **B — AI-ready** | `kind=signal`, alarm `context`, audit/timeline, DI text alanları | **Paralel** (inference yok) | ▶️ checklist §4 |
| **C — Implementasyon** | Scorer, MngLLM güvenlik özeti, workflow AI node, RAG | **Önkoşul sonrası** | ⏸️ §5 |

SIEM ile aynı disiplin: **plan şimdi, kod workflow + SIEM çekirdeği + alarm korelasyon sonrası.**

---

## 3. Kilitli mimari ilkeler

| # | İlke |
|---|------|
| K1 | **İki şerit:** (A) Scorer → `kind=signal` → Alarm Engine; (B) Copilot (MngLLM / DI RAG) → insan / workflow |
| K2 | AI **korelasyon motorunun içinde değil**; U1/U2/U4 kural tabanlı kalır |
| K3 | AI **ingest sıcak yolunu bloklamaz** — batch/async worker, rate limit |
| K4 | Scorer çıktısı **doğrudan aksiyon tetiklemez**; alarm → workflow → onay |
| K5 | **On-prem / offline** model önceliği (major §3.4); cloud-only bağımlılık yok |
| K6 | **MngLLM çeviri** ayrı track (aktif); güvenlik/SIEM AI aynı önkoşul kapısından geçer |

### Yerleşim özeti

```text
Engine → Reactor → sec_events / mon_metrics
                         │
         ┌───────────────┼────────────────┐
         ▼               ▼                ▼
   [AI Scorer]     MngAlarm           [MngLLM / DI]
   batch/async     kural/korelasyon    istek bazlı
         │               │
         └─ signal ──────┘ → alarm → Workflow (AI node: sonra)
```

---

## 4. AI-ready checklist (şimdi — inference yok)

Çekirdek geliştirmeler sürerken aşağıdakiler **tasarım/kod review** ile doğrulanır:

| # | Modül | AI-ready madde |
|---|-------|----------------|
| R1 | **Reactor / SIEM** | Normalize `sec_events`; `rawPreview` + hash; tam raw MVP’de zorunlu değil |
| R2 | **Observation zarfı** | `kind=signal` şeması alarm §9 ile uyumlu (Faz 1.1) |
| R3 | **MngAlarm** | `type: anomaly` / `predictive` kural şablonu tanımlı; scorer bağlantısı boş seam |
| R4 | **Alarm event** | `context` alanı özet/AI için yeterli bağlam taşır (IP, asset, rule, sample olay id) |
| R5 | **Workflow** | Execution context zengin event payload; AI node **implementasyonu yok**, extension: [workflow/AI_NODE_EXTENSION_SPEC.md](./workflow/AI_NODE_EXTENSION_SPEC.md) |
| R6 | **OC** | WorkItem audit + timeline; alarm→WorkItem bağlamı korunur |
| R7 | **Document Intelligence** | Text extraction / summary alanları veri modelinde (Faz 3 öncesi hazırlık) |
| R8 | **Performans** | Scorer/LLM ayrı worker; SecEventQueue metrikten izole ([SIEM_PERFORMANCE §2](./monitoring/SIEM_PERFORMANCE_PLAN.md)) |
| R9 | **Yetki** | DI RAG ve güvenlik özeti: yalnızca kullanıcının görebildiği veri |
| R10 | **Benchmark** | AI öncesi P1 baseline (SIEM/alarm) — scorer kalibrasyonu için zemin |

---

## 5. AI implementasyon önkoşulları (hepsi gerekli)

Aşağıdakiler **canlı ve en az bir E2E senaryoda doğrulanmadan** AI Faz 1 implementasyonuna geçilmez:

| # | Önkoşul | Referans |
|---|---------|----------|
| P1 | Workflow Event Trigger + onaylı aksiyon seam (SIEM §8) | [workflow/DEVAM.md](./workflow/DEVAM.md) |
| P2 | MngAlarm Faz 1–2 (threshold + SIEM U1/U2/U4 korelasyon) | [alarm/DEVAM.md](./alarm/DEVAM.md) |
| P3 | SIEM Faz 1 spike geçmiş (`sec_events` ingest) | [monitoring/SIEM_FAZ1_SPIKE.md](./monitoring/SIEM_FAZ1_SPIKE.md) |
| P4 | Alarm → Workflow uçtan uca (üretim benzeri senaryo) | alarm + workflow DEVAM |
| P5 | Performans P1 baseline JSON | [monitoring/SIEM_PERFORMANCE_PLAN.md](./monitoring/SIEM_PERFORMANCE_PLAN.md) §8 |

---

## 6. AI fazları (implementasyon sırası)

| AI faz | İçerik | Alarm / platform faz | Risk |
|--------|--------|----------------------|------|
| **AI-0** | Bu karar + checklist R1–R10 | Şimdi | — |
| **AI-1** | Alarm sonrası özet; onay öncesi briefing (MngLLM) | Workflow güvenlik otomasyonu sonrası | Düşük |
| **AI-2** | DI runbook RAG (yetkili); alarm→SOP eşleştirme | DI text extraction hazır | Düşük |
| **AI-3** | Workflow Summarize / Classify node | Workflow plan Faz 5 | Orta |
| **AI-4** | Scorer → signal → anomaly kural | **Alarm Faz 4** | Yüksek — baseline şart |
| **AI-5** | Threat hunting NLQ, UEBA, gelişmiş öneri | SIEM olgun + hacim kanıtı | Yüksek |

**Erken yapılmayacaklar:** ingest içi LLM · AI ile korelasyon · scorer→doğrudan firewall blok · her olay için embedding · ayrı AI ürün takımı seam’siz entegrasyon.

---

## 7. Odak önceliği (bugün)

```text
1. Workflow (kalan E2E / native observation)
2. MngAlarm SIEM korelasyon (U1/U2/U4)
3. SIEM Faz 1 spike (workflow sonrası)
4. AI-ready checklist (R1–R10) — code review maddesi
─── kapı ───
5. AI-1 (assistive özet)
6. AI-4 (scorer)
```

MngLLM **çeviri** bu sıranın dışında devam eder.

---

## 8. Referanslar

| Konu | Doküman |
|------|---------|
| Major AI vizyonu | [operationcore/major_plan.md §8](./operationcore/major_plan.md) |
| Scorer modeli | [alarm/ALARM_RULE_ENGINE_PLAN.md §9](./alarm/ALARM_RULE_ENGINE_PLAN.md) |
| SIEM performans | [monitoring/SIEM_PERFORMANCE_PLAN.md §2](./monitoring/SIEM_PERFORMANCE_PLAN.md) |
| Workflow AI node (sonra) | [workflow/AI_NODE_EXTENSION_SPEC.md](./workflow/AI_NODE_EXTENSION_SPEC.md) · [planing.md §19, Faz 5](./workflow/planing.md) |
| Document Intelligence AI | [document_intelligence/MonitraNG_Document_Intelligence_Planning.md §4.10](./document_intelligence/MonitraNG_Document_Intelligence_Planning.md) |
| MngLLM roadmap | [content/MngLLM/support/guides/ROADMAP.md](../content/MngLLM/support/guides/ROADMAP.md) |
