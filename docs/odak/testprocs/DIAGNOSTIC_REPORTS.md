# UI Diagnostic Raporları

**Son güncelleme:** 9 Haziran 2026  
**İlgili:** [../diagnostic/DIAGNOSTIC_PLAN.md](../diagnostic/DIAGNOSTIC_PLAN.md) (backend) · [E2E_TOOLING.md](./E2E_TOOLING.md)

---

## 1. Amaç

UI test oturumu sonunda **aksiyon alınabilir** bir rapor:

- Hangi sayfalar geçti / kaldı
- Fail detayı (persona, route, console, network)
- Backend diagnostic ile birleşik görünüm (opsiyonel)
- Triage için net öneri

Backend diagnostic raporları (`docs/odak/diagnostic/DIAGNOSTIC_REPORT_*.md`) **API performansına** odaklanır. Bu doküman **UI davranış ve fonksiyonel smoke/flow** sonuçlarını kapsar.

---

## 2. Rapor türleri

| Tür | Kaynak | Sıklık |
|-----|--------|--------|
| **Smoke özet** | Playwright JSON + HTML | Her PR / nightly |
| **Modül diagnostic** | Playwright + checklist mapping | Modül sprint sonu |
| **Birleşik oturum** | UI smoke + backend `diagnostic-*.ps1` | Release öncesi |
| **Agent triage notu** | Cursor agent markdown | Fail sonrası ad hoc |

---

## 3. Çıktı konumları

```
docs/odak/testprocs/reports/
  ui_smoke_20260609_153000.json      # Playwright JSON (kopya)
  ui_smoke_20260609_153000.md        # İnsan okunur özet

docs/odak/diagnostic/reports/        # Mevcut — backend JSON (değişmez)
  oc_pages_20260609_144342.json
```

**Git:** `reports/` klasörü `.gitignore`'a eklenmesi önerilir (diagnostic ile aynı model). Kalıcı özetler `DIAGNOSTIC_REPORT_YYYY-MM-DD-ui.md` olarak commit edilebilir.

---

## 4. Markdown rapor şablonu

Dosya adı: `DIAGNOSTIC_REPORT_YYYY-MM-DD-ui.md` (veya modül suffix: `-widgets.md`)

```markdown
# Mng.Ui Diagnostic Raporu — {tarih}

**Ortam:** Odak test (`192.168.20.20`) · **Persona:** admin  
**Playwright:** {versiyon} · **Commit:** {sha kısa}  
**Backend paketi:** {varsa diagnostic script adı + JSON dosyası}

---

## Özet

| Metrik | Değer |
|--------|-------|
| Smoke geçen | {pass}/{total} |
| Flow geçen | {pass}/{total} |
| Console error | {count} |
| Network 5xx | {count} |
| Süre | {duration} |

**Sonuç:** ✅ Geçti / ⚠️ Kısmi / ❌ Fail

---

## Geçen sayfalar (smoke)

| Route | Süre (ms) | Not |
|-------|-----------|-----|
| /apps/widgets | 1240 | — |

---

## Fail detayları

### FAIL-1: /apps/operation-core/boards/{boardId}

| Alan | Değer |
|------|-------|
| Persona | admin |
| Beklenen | Kanban board görünür |
| Gözlemlenen | Boş state + GET 504 |
| Console | `AxiosError: timeout` |
| Network | `GET .../operations/boards/...` → 504 |
| Trace | `test-results/.../trace.zip` |
| Screenshot | `test-results/.../screenshot.png` |

**Kök neden (tahmin):** MngOperations gateway timeout — backend diagnostic ile doğrula.

**Önerilen aksiyon:**
1. `diagnostic-operation-pages.ps1` board_kanban_open paketi
2. MO log OC_PERF

---

## Backend cross-check (opsiyonel)

| Sayfa paketi | Warm P95 (ms) | SLA (ms) | Durum |
|--------------|---------------|----------|-------|
| board_kanban_open | 4100 | 3500 | ❌ |

Kaynak: `diagnostic/reports/oc_pages_{timestamp}.json`

---

## Açık aksiyonlar

- [ ] FAIL-1: MO board cold path
- [ ] Widgets: arama debounce flaky — data-testid ekle

---

## Ekler

- Playwright HTML: `Mng.Ui/playwright-report/index.html`
- page-catalog.yml smoke coverage: %{coverage}
```

---

## 5. JSON özet şeması (UI smoke)

Playwright `results.json` üzerine ince bir wrapper (ileride script):

```json
{
  "generatedAt": "2026-06-09T15:30:00Z",
  "environment": "odak_test",
  "persona": "admin",
  "commit": "abc1234",
  "summary": {
    "passed": 12,
    "failed": 1,
    "skipped": 0,
    "durationMs": 145000
  },
  "failures": [
    {
      "route": "/apps/operation-core/boards/b-001",
      "testTitle": "board kanban loads",
      "consoleErrors": ["AxiosError: timeout"],
      "network5xx": ["GET /operations/boards/b-001/list"],
      "artifactPaths": {
        "trace": "test-results/.../trace.zip",
        "screenshot": "test-results/.../png"
      }
    }
  ],
  "backendDiagnosticRef": "docs/odak/diagnostic/reports/oc_pages_20260609.json"
}
```

---

## 6. Birleşik oturum script'i (plan)

**Dosya (henüz yazılmadı):** `docs/odak/testprocs/scripts/run-ui-diagnostic-session.ps1`

```powershell
# Taslak akış
# 1. seed-ui-test-env.ps1 (gerekirse)
# 2. cd Mng.Ui; npm run test:e2e:smoke
# 3. diagnostic-operation-pages.ps1 -OutputJson ...
# 4. Merge → reports/ui_session_{timestamp}.md
```

---

## 7. Müşteri / Document Intelligence entegrasyonu (opsiyonel)

Mevcut sistem diagnostic raporu:

- Kaynak: `docs/odak/document_intelligence/system/diagnostic-raporu.md`
- Seed: `seed-system-diagnostic-report.ps1`

UI test özetleri ileride bu rapora **"UI Smoke Durumu"** bölümü olarak eklenebilir (Faz 5).

---

## 8. Triage önceliği

| Severity | Koşul | Aksiyon süresi |
|----------|-------|----------------|
| **S1** | P0 route smoke fail | Aynı gün |
| **S2** | Flow fail, smoke geçer | Sprint içi |
| **S3** | Flaky (2/3 pass) | Stabilize et, retry azalt |
| **S4** | P2/P3 only | Backlog |

---

## 9. İlk rapor hedefi

Faz 2 tamamlandığında:

- [ ] `DIAGNOSTIC_REPORT_YYYY-MM-DD-ui-widgets.md` (pilot modül)
- [ ] En az 1 fail senaryosu için trace + kök neden şablonu doldurulmuş örnek
