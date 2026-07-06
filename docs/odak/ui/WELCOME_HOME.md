# Ana sayfa (Welcome / Home)

## Amaç

Giriş sonrası varsayılan sayfa **`/`** (home). Dashboard değil; **kişiselleştirilmiş operasyon lobisi**:

- Kim olduğunuz (selamlama, domain, rol, son giriş)
- Bugün yapılacaklar (rol bazlı aksiyon widget’ları)
- Yetkili modül kısayolları (side menu ile senkron)

Eski MaterialPro analitik dashboard bu rolden çıkarıldı; analitik şablonlar menüden `/dashboards/*` ile erişilebilir.

## Sayfa yapısı

```
┌─────────────────────────────────────────────────────────┐
│ A. HERO — selamlama, domain logosu, rol, son giriş      │
├─────────────────────────────────────────────────────────┤
│ B. AKSİYON ŞERİDİ — rol bazlı “Bugün” widget’ları       │
│    [Lisans] [Onaylar] [Alarmlar] [Görevler] [Devam et]  │
├─────────────────────────────────────────────────────────┤
│ C. MODÜLLER — side menu yetkisine göre gruplu kart grid │
└─────────────────────────────────────────────────────────┘
```

## Dosyalar

| Dosya | Rol |
|-------|-----|
| `Mng.Ui/pages/index.vue` | Route `/` — `WelcomeHomePage` |
| `Mng.Ui/pages/welcome.vue` | Eski URL → `/` yönlendirmesi |
| `Mng.Ui/components/welcome/WelcomeHomePage.vue` | Orchestrator |
| `Mng.Ui/components/welcome/WelcomeHero.vue` | Hero bandı |
| `Mng.Ui/components/welcome/WelcomeActionStrip.vue` | “Bugün” widget satırı |
| `Mng.Ui/components/welcome/WelcomeModuleGrid.vue` | Modül kart grid |
| `Mng.Ui/components/welcome/WelcomeModuleCard.vue` | Tek modül kartı |
| `Mng.Ui/components/welcome/actions/*.vue` | Aksiyon widget’ları |
| `Mng.Ui/utils/welcomeModuleRegistry.ts` | Statik modül metadata |
| `Mng.Ui/composables/useWelcomePage.ts` | Menü yetkisi + kart filtreleme |
| `Mng.Ui/utils/welcomeMenuUtils.ts` | Menü flatten / prefix erişim |
| `Mng.Ui/composables/useWelcomeMenuAccess.ts` | Widget görünürlük yardımcıları |
| `Mng.Ui/utils/welcomeRecentPagesStorage.ts` | Devam et localStorage |
| `Mng.Ui/composables/useRecentPages.ts` | Reaktif liste + kayıt API |
| `Mng.Ui/composables/useWelcomePageTitle.ts` | Path → menü/i18n başlık |
| `Mng.Ui/utils/resolveSideMenuItemTitle.ts` | NavItem ile aynı başlık çözümü |
| `Mng.Ui/plugins/z-welcome-recent-pages.client.ts` | `router.afterEach` takibi |
| `Mng.Ui/components/lc/Full/vertical-sidebar/NavItem/index.vue` | Menü tıklamasında kayıt |

## Hero (A)

Metinler: `utils/locales/tr.json` / `en.json` → `welcome.*`

Gösterilen bilgiler (JWT / auth store / Keeper profil):

- Ad soyad veya kullanıcı adı
- Domain adı ve logosu (varsa)
- Rol (yönetici / kullanıcı)
- Son giriş (`lastLoginAt`, varsa)
- Uygulama sürümü (`runtimeConfig.public.appVersion`)

## Aksiyon şeridi (B)

Rol bazlı widget sırası (`WelcomeActionStrip.vue`):

| Rol | Sıra |
|-----|------|
| Admin | Lisans → Onaylar → Alarmlar → Görevler → Devam et |
| Manager | Onaylar → Alarmlar → Görevler → Devam et |
| User | Görevler → Devam et |

| Widget | Dosya | Veri kaynağı |
|--------|-------|--------------|
| Lisans durumu | `WelcomeLicenseStatus.vue` | Keeper `license/{domain}` + user-count |
| Bekleyen onaylar | `WelcomePendingApprovals.vue` | `workflowListApprovals('Pending')` |
| Aktif alarmlar | `WelcomeActiveAlarms.vue` | `alarmDashboardSnapshot` → `openTotal` |
| Bana atanan görevler | `WelcomeAssignedTasks.vue` | DG `tm_issues` (limit 200, client filtre) |
| Devam et | `WelcomeRecentPages.vue` | localStorage, son 5 `/apps/*` |

Widget’lar menü yüklenene kadar bekler (`sideMenu.loadMenuItems`).

## Devam et — localStorage

- **Key:** `welcome_recent_{userKey}_{domainKey}` (JWT sub / domain)
- **Kayıt:** sidebar `@click` + `router.afterEach` (SPA `ssr:false`)
- **Başlık:** `resolveWelcomePageTitle(path)` — side menu `pageCode` + `menu.{pageCode}` / kök i18n; NavItem ile aynı mantık
- Gösterimde eski kayıtlar da render anında yeniden çözülür (dil / menü değişince güncellenir)

## Modül kartları (C)

Registry: `utils/welcomeModuleRegistry.ts` — OC, TM, Monitoring, SIEM, Alarm, DI, Odak Eğitim/Sipariş, Automation, Datasets.

`useWelcomePage.ts`:

1. Side menu’de erişilebilir route prefix’lerine göre filtreler
2. Registry’de olmayan menü dalları için **fallback kart** üretir
3. Kartlar 4 grupta: Operasyon · İzleme · Platform · Uygulamalar

Yeni modül eklemek:

1. `welcomeModuleRegistry.ts`’e kayıt ekleyin (`id`, `routePrefix`, `titleKey`, `links`, …)
2. `tr.json` / `en.json` → `welcome.modules.<id>.*`
3. Side menu patch script’i ile menü kaydı (ayrı oturum)

## Routing ve izinler

- Giriş: `LoginForm` → `/`
- Logo: `NuxtLink to="/"`
- `menu-permission.global.ts`: `/` ve `/welcome` menü izni kontrolünden muaf

## i18n notu

Legacy vue-i18n modunda bileşenlerde **`useAppI18n()`** kullanın; `useI18n()` güvenilir değil.

## Geliştirme

```powershell
cd Mng.Ui
cp .env.example .env   # GATEWAY_URL=http://192.168.20.20:5040
npm run dev
```

Tarayıcı: http://localhost:3000/

**Smoke:** Giriş → birkaç `/apps/*` sayfası gez → ana sayfada “Devam et” dolmalı; başlıklar menü adlarıyla eşleşmeli (path segmenti veya `pageCode` değil).

## Deploy

Yalnızca UI değişikliği — backend deploy gerekmez.

```powershell
# Prod (192.168.20.8)
.\scripts\odak\sync-odak-prod.ps1 -Paths Mng.Ui
.\scripts\odak\deploy-odak-prod.ps1 -Services mngui -NoCache
```

Odak test (192.168.20.20):

```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui
.\scripts\odak\deploy-odak-apps.ps1 -Services mngui -NoCache
```

Deploy sonrası tarayıcıda hard refresh (statik SPA önbelleği).

## Durum ve devam

Bkz. [DEVAM.md](./DEVAM.md).
