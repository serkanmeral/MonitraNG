# Mng.Ui — Oturum durumu (Welcome Home)

## Son Çalışılan Konu

Giriş sonrası ana sayfa (`/`) — kişiselleştirilmiş welcome page: hero, “Bugün” aksiyon widget’ları, side menu senkron modül kartları, Devam et listesi.

## Tamamlanan İşler

- **Faz 1:** `welcomeModuleRegistry`, `useWelcomePage`, Hero + modül grid
- **Faz 2–4:** `WelcomeActionStrip` — lisans, onaylar, alarmlar, atanan görevler, devam et
- **Devam et:** `welcomeRecentPagesStorage`, plugin + NavItem click, menü/i18n başlık çözümü (`useWelcomePageTitle`)
- **Locale:** `welcome.*` — `tr.json`, `en.json`
- **Dokümantasyon:** `docs/odak/ui/WELCOME_HOME.md`, `docs/odak/ui/DEVAM.md`

## Devam Eden İşler

- Yok (MVP tamam)

## Sonraki Adımlar

- **Deploy:** yalnızca `mngui` (backend değişikliği yok)
- **Faz 5 (opsiyonel):** MngLLM prompt bar, TM aggregate endpoint, ek locale

## Önemli Notlar

- i18n: bileşenlerde `useAppI18n()`, `useI18n()` değil
- SPA (`ssr: false`): route middleware yerine client plugin + sidebar click
- TM atanan sayısı şu an `tm_issues` limit 200 client filtre — tam sayı için backend iş gerekir

## Son Güncelleme

**7 Temmuz 2026** — Welcome page MVP tamam; commit + `mngui` prod deploy planlandı.
