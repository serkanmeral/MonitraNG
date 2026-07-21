# Workflow — Çalışma Alanı

**Amaç:** Platform Workflow (MngWorkflow) için **planlama dokümanları**, **seed script’leri** ve **geçmiş karar özetleri**nin tek kökü.  
**Başlangıç:** 16 Temmuz 2026  
**Kod:** `MngWorkflow/` · **UI:** `/apps/automation-center/workflows` (Otomasyon Merkezi)

> Bu klasör, workflow oturumlarının çalışma alanıdır. Eski dokümanlar taşınmaz; buradan **indekslenir**.  
> **Not (16 Tem 2026):** E-arşiv → AI → HTTP senaryosunda önce **DI AI** konuşuluyor → [docs/ai/](../ai/README.md). Workflow orkestrasyonu sonraki adım.

---

## Klasör yapısı

```text
docs/workflow/
├── README.md              ← Bu dosya (indeks)
├── planning/              ← Yeni / güncel planlama dokümanları
├── seeds/                 ← Seed / örnek workflow / dataset seed script’leri
└── history/               ← Geçmiş konuşma ve karar toparlamaları
```

| Klasör | Ne koyulur | Ne koyulmaz |
|--------|------------|-------------|
| `planning/` | Bugünkü ve sonraki planlar, checklist, karar notları | Pazarlama metinleri, CI “workflow” dosyaları |
| `seeds/` | Lokal/lab seed PowerShell (veya JSON örnekleri) | Production secret, gerçek token |
| `history/` | Chat/oturum özetleri, evrim notları | Canlı implementasyon planı (o `planning/`) |

---

## Mevcut doküman haritası (taşınmadı)

### Erken planlama (Şubat 2026) — IFTTT / MngRules çizgisi

| Dosya | Rol |
|-------|-----|
| [docs/content/workflow/WORKFLOW_PLANNING.md](../content/workflow/WORKFLOW_PLANNING.md) | İlk yaklaşım karşılaştırması, MngRules kararı, DG hook’ları |
| [docs/content/workflow/FAZ1_IMPLEMENTATION_PLAN.md](../content/workflow/FAZ1_IMPLEMENTATION_PLAN.md) | Erken Faz 1 (validation API, `@wf_*` dataset’ler) |
| [docs/content/workflow/CHECKPOINT_2026_02_25.md](../content/workflow/CHECKPOINT_2026_02_25.md) | Checkpoint |
| [docs/content/workflow/TM_ISSUES_WORKFLOW_WIRING.md](../content/workflow/TM_ISSUES_WORKFLOW_WIRING.md) | Task Manager ↔ validation wiring |

### Odak / motor implementasyonu (2026) — node-based engine

| Dosya | Rol |
|-------|-----|
| [docs/odak/workflow/DEVAM.md](../odak/workflow/DEVAM.md) | **Son durum / handoff** (Faz 0–6+, UI W1) |
| [docs/odak/workflow/Workflow Backend Implementation Plan v1.md](../odak/workflow/Workflow%20Backend%20Implementation%20Plan%20v1.md) | Backend uygulama planı |
| [docs/odak/workflow/planing.md](../odak/workflow/planing.md) | Engine planı v1.1 |
| [docs/odak/workflow/InternalDesign.md](../odak/workflow/InternalDesign.md) | Runtime internal design |
| [docs/odak/workflow/MonitraNG Workflow Runtime Internal Design v1_1.md](../odak/workflow/MonitraNG%20Workflow%20Runtime%20Internal%20Design%20v1_1.md) | Runtime v1.1 |
| [docs/odak/workflow/ODAK_MO_VS_WORKFLOW_SCENARIOS.md](../odak/workflow/ODAK_MO_VS_WORKFLOW_SCENARIOS.md) | MO vs Workflow karar matrisi |
| [docs/odak/workflow/AI_NODE_EXTENSION_SPEC.md](../odak/workflow/AI_NODE_EXTENSION_SPEC.md) | AI node genişletmesi |

### Ürün / pazarlama envanteri

| Dosya | Rol |
|-------|-----|
| [docs/monitrang/pazarlama/Docs/modul-workflow.md](../monitrang/pazarlama/Docs/modul-workflow.md) | Modül fonksiyon envanteri |
| [docs/monitrang/pazarlama/brosur/moduller/06-workflow.md](../monitrang/pazarlama/brosur/moduller/06-workflow.md) | Broşür özeti |

### İlgili (workflow dışı ama seam)

| Dosya | Rol |
|-------|-----|
| [docs/content/monitoring_plans/MONITORING_WORKFLOW.md](../content/monitoring_plans/MONITORING_WORKFLOW.md) | Monitoring ↔ workflow (tespit/aksiyon ayrımı) |
| [docs/odak/monitoring/SIEM_WORKFLOW_SEAM.md](../odak/monitoring/SIEM_WORKFLOW_SEAM.md) | SIEM seam |

---

## Evrim (kısa)

1. **Şubat 2026:** IFTTT vs mini Node-RED tartışması → **MngRules** adı; DG validation + RabbitMQ event aksiyonları.  
2. **Sonrası (Odak):** Hibrit kararın “ileriye Node-RED tarzı” kısmı **asıl motor** oldu: per-node execution, Api+Worker, approval/delay/OC/alarm.  
3. **Bugün:** Bu kök (`docs/workflow/`) altında platform geneli devam; seed’ler ve yeni planlar burada.

Detaylı konuşma özeti: [history/KONUSMA_OZETI.md](./history/KONUSMA_OZETI.md)

---

## Çalışma kuralları (bu oturumlar)

- Backend değişiklikleri → lokal Docker Desktop’a deploy edilebilir.  
- UI deploy → kullanıcı talebi olmadan yapılmaz (`npm run dev` kullanıcıda).  
- Yeni plan / seed → mümkünse bu klasöre yazılır; eski path’lere sadece referans verilir.
