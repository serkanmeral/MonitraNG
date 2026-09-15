# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 15 Eylül 2026  
**Konu:** Portföy timeout düzeltmesi, paket demo seed’leri, SEED-PMO Gantt zenginleştirme; doküman + commit + UI deploy  
**Ortam:** Odak test `192.168.20.20` · backend + UI deploy serbest  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.36.0**

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Omurga hazır. Liste portföyü hızlı. 7 paket için `SEED-*` demo projeler var; `SEED-PMO` Gantt tarih/FS/baseline ile zengin. Adım adım UI doğrulama devam eder.

---

## Son çalışılan konu (15 Eylül 2026)

1. **0.35.1 sertleştirme** — kapatma yolunda DI fail-closed (`DOC_LOOKUP_FAILED`); kanıt kilidi yaprak-only; status bayrakları yalnız açık işlerde; boş `relationType` varsayılan `reference` değil.
2. **Portföy timeout** — `GET /projects/portfolio` her proje için ağır status paketi çekiyordu → gateway 503. Hafif portföy (WBS yüzde / sapma / bağsız yaprak) ile ~1–2 sn.
3. **Demo seed** — her iş paketi için bir proje: `SEED-PMO` … `SEED-ACCEPTANCE` (`seed-pack-demo-projects.ps1`).
4. **SEED-PMO Gantt** — tarihler, FS zinciri, ilerleme, baseline (`enrich-seed-pmo.ps1`).
5. Smoke F214 kanıt bağı ile uyumlu hale getirildi.

---

## Tamamlanan işler (bu hat)

| Dilim | Özet |
|--------|------|
| F1-0 … F1-9 | Kurulum, DI tür/ilişki, görsel kanıt, `pm_*`, Gantt, WBS–OC, iz/durum, karar, PMO+kalite tohumu |
| F2-1 … F2-13 | İç katalog + kapı, RAID, kapasite, bütçe, okundu, yükümlülük, denetim, toplantı, paydaş, portföy, süreç |
| **F2-14 … F2-16** | Kapı / kanıt / onay kapatma kilitleri |
| F3-1 … F3-5 | Sektör rafları |
| F4-1 … F4-7 | Paket OC iskeleti + yaprak/özet iş + kanıt + plan (`reference`) bağı |
| F5-1 | Paket kökeni / SHA-256 |
| **0.35.1 / 0.36.0** | Close-path sertleştirme + hafif portföy + demo seed’ler |

**Katalog (7, sürüm 1.1.0):** `pmo` · `quality` · `architecture` · `proposal` · `eco` · `onboarding` · `acceptance`

**Odak demo projeler:** `SEED-ACCEPTANCE`, `SEED-ARCHITECTURE`, `SEED-ECO`, `SEED-ONBOARDING`, `SEED-PMO` (Gantt zengin), `SEED-PROPOSAL`, `SEED-QUALITY`

---

## Kararlar (hatırla)

- Generic teslimat omurgası. AnkaraBT DOCX **örnek**; NLP / şartname parser yok.
- Kapatma sırası: kapı → kanıt (yaprak) → bağlı belgeler yayınlı.
- Kanıt: `evidence`/`output`; plan: `reference`. Paketten otomatik bağ yok.
- Portföy listesi hafif; detaylı durum proje **Durum** sekmesinde.
- Token: `docs/odak/operationcore/scripts/get-operationcore-token.ps1`. Smoke: `pwsh`.
- Dataset’ler: `pm_*` (DG / `GET …/data/pm_projects`).

---

## Scriptler

| Script | Amaç |
|--------|------|
| `docs/odak/project_management/scripts/seed-pack-demo-projects.ps1` | Her paket için `SEED-*` proje |
| `docs/odak/project_management/scripts/enrich-seed-pmo.ps1` | PMO Gantt tarih/FS/baseline |
| `scripts/tests/MngOperations/smoke-f*.ps1` | Dilim smoke’ları |

Deploy: `sync-odak-source.ps1` → `deploy-odak-apps.ps1 -Services mngoperations,mngui -NoCache`

---

## Bilinçli ertelenen

- App Store / üçüncü taraf paket / ücret
- RAID Monte Carlo; CPM / kaynak dengeleme; bütçe ERP
- Paketten otomatik plan/kanıt önerisi; sponsor export
- Portföyde tam attention bayrakları (kapı/risk vb. — şimdilik hafif)

---

## Sonraki adımlar

Adım adım UI doğrulama (Gantt, plan/kanıt bağlama, kapatma kilitleri). Sonraki dilim ayrı onay.
