# Ana sayfa (Welcome / Home)

## Amaç

Giriş sonrası varsayılan sayfa **`/`** (home). Eski MaterialPro analitik dashboard içeriği bu rolden çıkarıldı; analitik şablonlar menüden `/dashboards/*` ile erişilebilir.

## Dosyalar

| Dosya | Rol |
|-------|-----|
| `Mng.Ui/pages/index.vue` | Route `/` — `WelcomeHomePage` bileşenini gösterir |
| `Mng.Ui/components/welcome/WelcomeHomePage.vue` | Banner + modül kartları |
| `Mng.Ui/pages/welcome.vue` | Eski URL; `replace` ile `/` yönlendirmesi |

## Banner

Türkçe metinler `utils/locales/tr.json` → `welcome.*`. Gösterilen bilgiler (JWT / auth store):

- Ad soyad veya kullanıcı adı
- Domain (`domain_name`)
- Rol (yönetici / kullanıcı)
- E-posta, grup özeti (varsa)
- Uygulama sürümü (`runtimeConfig.public.appVersion`)
- Tarih/saat (`tr-TR`)

Sahte istatistik ve “son aktivite” mock verileri kaldırıldı.

## Modül kartları

`WelcomeHomePage.vue` içindeki `moduleCards` dizisine tamamlanan her modül için bir kayıt eklenir:

```ts
{
  id: 'task-manager',
  titleKey: 'welcome.modules.taskManager.title',
  descriptionKey: 'welcome.modules.taskManager.description',
  icon: 'mdi-clipboard-list-outline',
  color: 'primary',
  links: [
    { labelKey: 'welcome.modules.taskManager.linkWorkspace', to: '/apps/task-manager/workspace' },
    { labelKey: 'welcome.modules.taskManager.linkHub', to: '/apps/task-manager' },
  ],
}
```

1. `moduleCards`’a kart ekleyin  
2. `tr.json` ve `en.json` altında `welcome.modules.<modulId>.*` çevirilerini tanımlayın  

## Routing ve izinler

- Giriş: `LoginForm` → `/`
- Logo: `NuxtLink to="/"`
- `menu-permission.global.ts`: `/` ve `/welcome` menü izni kontrolünden muaf (giriş yapmış kullanıcılar ana sayfayı görür)

## Geliştirme

```powershell
cd Mng.Ui
cp .env.example .env   # GATEWAY_URL=http://192.168.20.20:5040 (sunucu Keeper)
npm run dev
```

Giriş testi (dev form varsayılanı): `odak@odak_admin` / `Admin123!` — istekler `GATEWAY_URL/keeper/api/auth/token` üzerinden gider.

Tarayıcı: http://localhost:3000/
