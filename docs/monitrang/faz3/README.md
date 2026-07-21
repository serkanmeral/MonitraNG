# MonitraNG Faz 3 — Odak Kompozit Teklif Geliştirme

> **Yeni chat:** Agent’a şunu ver → [`AGENT_START.md`](./AGENT_START.md)  
> (Kurallar: docs, migration, commit/push, **Docker backend vs UI npm run dev**, terminal inceleme.)

**Amaç:** [Odak Kompozit fiyat teklifi](../../odak/commercial/Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md) maddelerinin geliştirme planı, iş takibi ve müşteri ortamına taşıma (migration) rehberi.

**Teklif No:** ODK-FT-2026-001  
**Son güncelleme:** 13 Temmuz 2026

---

## Paketler

| Klasör | Teklif | Öncelik | Not |
|:---|:---|:---:|:---|
| [ai_platform/](./ai_platform/) | Çapraz — AI omurgası | P1 | MngLLM, Ollama, extract/embed, Mongo vektör MVP |
| [document_intelligence/](./document_intelligence/) | §4.1 Döküman Zekası | **P1 — başlangıç** | Olgun baseline; gap + AI tüketici |
| [reporting/](./reporting/) | §4.2 Raporlama | P1 | MNG / HTTP / DB kaynakları |
| [monitoring/](./monitoring/) | §4.3 İzleme | P1 | SIEM yok; metrik + anomaly (AI → ai_platform) |
| [production_operations/](./production_operations/) | §4.4 Üretim Operasyonu | P1 | OC workspace; Monitoring köprüsü |
| [package_module/](./package_module/) | §4.6 İş Paketleri sürekli güncelleme | P1 | Backlog + dilimler |
| [survey_portal/](./survey_portal/) | §4.5 Dış Katılım (Anket) | **P3 — en son** | Host A/B henüz kararlı değil; şimdilik doküman |

Her pakette:

| Dosya | Rol |
|:---|:---|
| `Roadmap.md` | Major plan: fazlar, kapsam, bağımlılık, kabul |
| `work.md` | Yapılanlar, kalan işler, blocker, commit |

---

## Ortak dosyalar

| Dosya | Rol |
|:---|:---|
| [AGENT_START.md](./AGENT_START.md) | **Yeni chat bootstrap** — agent kuralları |
| [MIGRATION.md](./MIGRATION.md) | Dataset / seed / deploy sırası — test & prod checklist (tek kaynak) |

---

## Bağımlılık (özet)

```text
ai_platform (MngLLM / Ollama / embed) ──► DI · Monitoring · Production AI
document_intelligence ──┐
reporting ──────────────┼──► müşteri ortamı (pull + MIGRATION)
monitoring ─────────────┤
production_operations ──┘    (Monitoring asset’lerine yaslanır)
package_module               (sürekli dilim; DI/rapor ile ilişkili olabilir)
survey_portal                (en son; barındırma kararı bekleniyor)
```

---

## Önerilen geliştirme sırası

**Başlangıç paketi: Document Intelligence.** Teklifte en büyük görünür; ürün tarafında büyük kısmı zaten canlı (`docs/odak/document_intelligence/` — Faz P, D, D-BR, Managed Office, G0–G5, medya paketi, etiket kataloğu vb.). Faz 3 DI işi çoğunlukla **gap kapatma + bildirim + AI tüketimi**, sıfırdan çekirdek değil.

```text
Dalga 0   DI-0 gap + **DI-T (T-0/T-1 yetki suite)** paralel
    │
Dalga 1   ★ DI (ana hat) — kalan non-AI boşluklar (D-N, inject cilası, …)
    │         + ai_platform AI-0…2 (paralel omurga)
    │         + T-2…T-4 (miras, generate yetki, smoke birleşimi)
    │
    ├─ Reporting (paralel / DI dilimi boşalınca)
    └─ Monitoring çekirdek (paralel)
              │
              ▼
Dalga 2   Production (Monitoring’e yaslanır)
Dalga 3   DI/Mon/Prod AI tüketimi (ai_platform AI-3…5 hazır olunca)
Dalga 4   package_module (sürekli; DI ile sık kesişir)
Dalga 5   survey_portal (en son; host A/B şart)
```

| Sıra | Paket | Gerekçe |
|:---:|:---|:---|
| **1 (ana)** | **document_intelligence** | Başlangıç; gap + AI + **DI-T yetki testleri** |
| 1 (paralel) | **ai_platform** AI-0…2 | DI AI (DI-3+) için omurga; CPU-first |
| 2 (paralel) | **reporting** | Olgun; DI ana hat ilerlerken dilimlenebilir |
| 2 (paralel) | **monitoring** çekirdek | Production önkoşulu |
| 3 | **production_operations** | Monitoring hazır olunca |
| 3–4 | **ai_platform** AI-3…5 + DI/Mon AI | Embed, RAG async-first, anomaly |
| sürekli | **package_module** | DI generation / medya ile ilişkili dilimler |
| son | **survey_portal** | Host A/B netleşmeden kod yok |

**Çalışma hatları**

1. **Hat A — DI (öncelikli):** DI-0 gap → non-AI → AI tüketici  
2. **Hat A2 — DI-T (zorunlu paralel):** T-0 → T-1 (yetki gate) → T-2…T-5 — detay: [document_intelligence/Roadmap.md](./document_intelligence/Roadmap.md) §5  
3. **Hat B — AI omurga:** AI-0 → AI-1 → AI-2 → AI-3 → AI-5  
4. **Hat C — Diğer P1:** Reporting / Monitoring / Production  
5. **Hat D — İş paketi:** Aralara dilim  

**İlk dilimler:** (1) **DI-0** gap tablosu · (2) **T-0/T-1** yetki otomasyonu · (3) AI-0 CPU smoke · (4) Reporting/Monitoring paralel  

Kaynak: [DI_PRODUCT_ROADMAP.md](../../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) · [DEVAM.md](../../odak/document_intelligence/DEVAM.md)  
AI altyapı: [ai_platform/Roadmap.md](./ai_platform/Roadmap.md) §5 (CPU-first, 16 GB prod min).  
DI test: [document_intelligence/Roadmap.md](./document_intelligence/Roadmap.md) §5 DI-T.

---

## Ortam akışı (GitHub → müşteri)

1. Geliştirme → commit / push (`main` veya ilgili branch)  
2. Müşteri sunucusunda `git pull`  
3. Etkilenen servislerin Docker rebuild / deploy  
4. [MIGRATION.md](./MIGRATION.md) sırasıyla dataset + seed + patch  
5. Smoke test (test ortamı)  
6. Aynı sıra ile prod (test yeşil olduktan sonra)

---

## Ticari / iç referans

- Müşteri teklifi: `docs/odak/commercial/Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md`  
- İç çalışma notları: `docs/odak/commercial/Odak_Kompozit_Teklif_IC_CALISMA_NOTLARI.md`
