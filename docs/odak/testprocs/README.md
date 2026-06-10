# Test Prosedürleri (Odak) — Mng.Ui

**Amaç:** Mng.Ui sayfalarının amaca uygun çalıştığını doğrulamak; test verisi üretmek; hataları raporlamak; tekrarlanabilir otomasyon ve diagnostic çıktıları üretmek.

**Durum:** Planlama aşaması (henüz Playwright kurulumu yok)  
**Devam noktası:** [DEVAM.md](./DEVAM.md)  
**Ana plan:** [TESTPROCS_PLAN.md](./TESTPROCS_PLAN.md)

---

## İlişkili Odak alanları

| Alan | Kapsam | İlişki |
|------|--------|--------|
| [../diagnostic/](../diagnostic/) | Backend response time, API wall-clock | **Tamamlayıcı** — UI E2E burada planlanıyor |
| [../operationcore/](../operationcore/) | OC dataset, seed, UI plan | Modül bazlı test senaryoları |
| [../../content/Mng.Ui/support/](../../content/Mng.Ui/support/) | Manuel test rehberleri (Widget vb.) | Checklist → otomatik spec kaynağı |

---

## Doküman indeksi

| Doküman | Konu |
|---------|------|
| [TESTPROCS_PLAN.md](./TESTPROCS_PLAN.md) | Strateji, mimari, fazlar, roller (agent vs CI) |
| [PAGE_CATALOG.md](./PAGE_CATALOG.md) | Kritik sayfa envanteri, öncelik, persona |
| [page-catalog.yml](./page-catalog.yml) | Makine okunur sayfa kataloğu (Playwright / rapor) |
| [TEST_DATA.md](./TEST_DATA.md) | Persona, seed, fixture, ortam |
| [E2E_TOOLING.md](./E2E_TOOLING.md) | Playwright, smoke/flow testleri, CI |
| [DIAGNOSTIC_REPORTS.md](./DIAGNOSTIC_REPORTS.md) | UI diagnostic rapor şablonu ve çıktı formatı |
| [DEVAM.md](./DEVAM.md) | Checkpoint — yeni chat başlangıcı |

---

## Kilitli kararlar (taslak)

| Konu | Karar |
|------|-------|
| UI test aracı | **Playwright** (Nuxt 3 uyumu, trace, CI) |
| Kapsam | Önce **`/apps/*` iş modülleri**; MaterialPro demo sayfaları hariç |
| Persona | **admin**, **manager**, **user** — ayrı test suite |
| Ortam | Odak test: `192.168.20.20` (diagnostic ile hizalı) |
| Backend diagnostic | Mevcut `docs/odak/diagnostic/scripts/*` korunur; UI testleri ek katman |
| Agent rolü | Cursor agent: kurulum, triage, rapor; kalıcı koşum **CI + Playwright** |

---

## Hedef klasör yapısı (uygulama sonrası)

```
Mng.Ui/
  e2e/
    fixtures/          # auth storageState, persona
    smoke/             # route açılır, kritik element
    modules/           # modül akış testleri
  playwright.config.ts

docs/odak/testprocs/
  scripts/             # seed-ui-test-env.ps1 (planlı)
  reports/             # UI diagnostic JSON/markdown (gitignore önerilir)
```

---

## Hızlı başlangıç (plan onaylandıktan sonra)

```powershell
# 1. Test ortamı + seed (henüz yazılacak)
# .\docs\odak\testprocs\scripts\seed-ui-test-env.ps1

# 2. Playwright (Mng.Ui içinde)
# cd Mng.Ui
# npx playwright install
# npm run test:e2e

# 3. Backend diagnostic (mevcut — UI ile birlikte kullan)
.\docs\odak\diagnostic\scripts\diagnostic-operation-pages.ps1
```
