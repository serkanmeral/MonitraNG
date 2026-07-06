# Mng.Ui — Welcome Home (DEVAM)

**Son güncelleme:** 7 Temmuz 2026  
**Durum:** ✅ **Faz 1–4 tamam** — MVP canlıya alınmaya hazır (yalnızca `mngui` deploy)

> **⭐ KALDIĞIMIZ YER (7 Tem 2026):** Welcome page MVP tamamlandı. Hero + “Bugün” widget’ları + side menu senkron modül kartları + **Devam et** (localStorage + menü/i18n başlık çözümü) çalışıyor. **Sıradaki (isteğe bağlı — Faz 5):** MngLLM prompt bar · TM atanan sayısı için hafif backend aggregate · `zh`/`fr`/`ar` locale hizalama.

---

## Tamamlanan (Faz 1–4)

### Faz 1 — Modül registry + side menu
- `welcomeModuleRegistry.ts` — 10 modül tanımı
- `useWelcomePage.ts` — menü yetkisine göre filtre + fallback kart
- `WelcomeHero`, `WelcomeModuleGrid`, `WelcomeModuleCard`, ince `WelcomeHomePage`

### Faz 2 — Aksiyon şeridi (temel)
- `WelcomeActionStrip.vue` — “Bugün” bölümü
- Bekleyen onaylar (Manager+) — workflow API
- Devam et — plugin + sidebar click + localStorage

### Faz 3–4 — Widget’lar + hero
- Aktif alarmlar, bana atanan görevler, lisans uyarısı (Admin)
- Hero: son giriş (`lastLoginAt`)
- Rol bazlı widget sırası
- `welcomeMenuUtils.ts`, `useWelcomeMenuAccess.ts`

### Devam et düzeltmeleri (7 Tem)
- `welcomeRecentPagesStorage.ts` — tek storage mantığı, kullanıcı/domain key
- `resolveSideMenuItemTitle.ts` + `useWelcomePageTitle.ts` — NavItem ile aynı i18n (`menu.{pageCode}`, kök key)
- Plugin artık path segmenti ile başlığı ezmiyor
- `WelcomeRecentPages.vue` — render anında başlık yeniden çözümü

### Locale
- `tr.json` / `en.json` — `welcome.*` (modüller, gruplar, aksiyon metinleri)

---

## Deploy notu

| Ortam | Komut | Backend |
|-------|--------|---------|
| Prod `192.168.20.8` | `sync-odak-prod.ps1 -Paths Mng.Ui` + `deploy-odak-prod.ps1 -Services mngui -NoCache` | Gerekmez |
| Odak `192.168.20.20` | `sync-odak-source.ps1 -Paths Mng.Ui` + `deploy-odak-apps.ps1 -Services mngui -NoCache` | Gerekmez |

---

## Sıradaki (Faz 5 — opsiyonel)

| # | Konu | Not |
|---|------|-----|
| 1 | MngLLM prompt bar | Hero altı hızlı soru / yönlendirme |
| 2 | TM atanan sayaç | DG aggregate endpoint; şu an 200 kayıt client filtre |
| 3 | Locale | `zh` / `fr` / `ar` eski mock `welcome.quickStats` |
| 4 | UX ince ayar | Widget boş/hata durumları, mobil grid |

---

## Test checklist

- [ ] `/` — hero alanları (admin / manager / user)
- [ ] Modül kartları yalnızca yetkili modüller
- [ ] Manager+: onay + alarm widget’ları
- [ ] Admin: lisans widget’ı
- [ ] TM menüsü varsa: atanan görev sayısı
- [ ] 3–5 sayfa gez → Devam et dolu, **Türkçe menü adları**
- [ ] Dil değiştir → Devam et başlıkları güncellenir

---

## Referans

- [WELCOME_HOME.md](./WELCOME_HOME.md) — mimari ve dosya haritası
- [README.md](./README.md) — Odak UI indeksi
