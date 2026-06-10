# Test Prosedürleri — Devam noktası (checkpoint)

> ## Yeni chat başlangıç prompt'u (kopyala-yapıştır)
>
> ```
> MonitraNG / Mng.Ui test prosedürleri konusunda çalışıyoruz.
> Repo: c:\Users\monitra\Dev\MonitraNG\MonitraNG
>
> Başlamadan önce şu checkpoint dosyasını oku ve bana kısa bir "kaldığımız yer" özeti ver:
> docs/odak/testprocs/DEVAM.md
>
> İlgili dokümanlar:
> - docs/odak/testprocs/TESTPROCS_PLAN.md
> - docs/odak/testprocs/PAGE_CATALOG.md
> - docs/odak/testprocs/E2E_TOOLING.md
>
> Yanıtlar Türkçe.
> ```

**Son güncelleme:** 9 Haziran 2026  
**Durum:** Planlama dokümanları oluşturuldu · uygulama (Playwright + seed) henüz başlamadı

> **⭐ KALDIĞIMIZ YER (9 Haz 2026):** UI test otomasyonu için `docs/odak/testprocs/` plan paketi hazır. Backend diagnostic (`docs/odak/diagnostic/`) zaten çalışıyor; UI E2E bir sonraki katman. **Sıradaki:** Faz 0 onayı → Playwright iskeleti (`Mng.Ui/e2e/`) → admin smoke + auth fixture → ilk modül (öneri: **widgets** veya **operation-core**).

**Ana kaynaklar:** [README.md](./README.md) · [TESTPROCS_PLAN.md](./TESTPROCS_PLAN.md) · Backend diagnostic: [../diagnostic/DEVAM.md](../diagnostic/DEVAM.md)

---

## Bu oturumda tamamlananlar (9 Haz 2026)

| # | Çıktı | Not |
|---|-------|-----|
| 1 | [README.md](./README.md) | İndeks ve kilitli kararlar |
| 2 | [TESTPROCS_PLAN.md](./TESTPROCS_PLAN.md) | Strateji, mimari, 5 fazlı yol haritası |
| 3 | [PAGE_CATALOG.md](./PAGE_CATALOG.md) | ~45 kritik sayfa envanteri |
| 4 | [page-catalog.yml](./page-catalog.yml) | Makine okunur katalog |
| 5 | [TEST_DATA.md](./TEST_DATA.md) | Persona, seed, ortam |
| 6 | [E2E_TOOLING.md](./E2E_TOOLING.md) | Playwright + CI planı |
| 7 | [DIAGNOSTIC_REPORTS.md](./DIAGNOSTIC_REPORTS.md) | UI rapor şablonu |

---

## Verilen kararlar (taslak — onay bekliyor)

| # | Karar | Kaynak |
|---|-------|--------|
| T1 | UI test aracı = **Playwright** | TESTPROCS_PLAN §3 |
| T2 | Kapsam = `/apps/*` iş modülleri; theme/demo hariç | PAGE_CATALOG |
| T3 | Üç persona: admin, manager, user | TEST_DATA |
| T4 | Backend diagnostic ayrı kalır; UI test ek katman | diagnostic README |
| T5 | Cursor agent = triage/kurulum; CI = kalıcı robot | TESTPROCS_PLAN §2 |
| T6 | İlk modül pilotu: **widgets** veya **operation-core** (seçilecek) | TESTPROCS_PLAN §5 |

---

## Sıradaki adımlar (önerilen sıra)

### Faz 0 — Onay (hemen)

- [ ] Plan dokümanlarını gözden geçir (ekip)
- [ ] Pilot modül seç: widgets **veya** operation-core
- [ ] Demo sayfalarının kapsam dışı olduğunu onayla

### Faz 1 — Altyapı (2–3 gün)

- [ ] `Mng.Ui` içine Playwright kurulumu ([E2E_TOOLING.md](./E2E_TOOLING.md))
- [ ] Auth fixture: admin login → `storageState`
- [ ] `seed-ui-test-env.ps1` taslağı ([TEST_DATA.md](./TEST_DATA.md))
- [ ] Smoke: 5–10 kritik route

### Faz 2 — Pilot modül (1 hafta)

- [ ] Seçilen modül için checklist → spec
- [ ] İlk UI diagnostic raporu ([DIAGNOSTIC_REPORTS.md](./DIAGNOSTIC_REPORTS.md))
- [ ] CI'ya smoke adımı (`.github/workflows/ci.yml`)

### Faz 3 — Genişletme (devam eden)

- [ ] Modül modül PAGE_CATALOG genişletme
- [ ] manager/user persona suite
- [ ] Nightly full smoke (develop branch)

---

## Açık sorular

| # | Soru | Öneri |
|---|------|-------|
| Q1 | Pilot modül widgets mi OC mi? | Widgets daha izole; OC daha kritik iş değeri |
| Q2 | Test domain ayrı mı (`test-ui-*`) yoksa mevcut Odak demo mu? | Ayrı domain — seed idempotent |
| Q3 | `reports/` gitignore? | Evet (diagnostic ile aynı model) |
