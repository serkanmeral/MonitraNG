# Odak — Production deploy

**Amaç:** Test (`192.168.20.20`) ve production (`192.168.20.8`) **tamamen bağımsız** iki sistemdir — her biri kendi `mng_common` (Mongo, Keycloak, Redis, …) ve `mng_apps` yığınını **kendi sunucusunda** çalıştırır. Bu klasör yalnızca production deploy içindir.

**İlke (zorunlu okuma):** [INDEPENDENCE.md](./INDEPENDENCE.md)

**Ne zaman buraya bakılır:** “Production deploy yap”, “prod’a at”, “192.168.20.8’e deploy” gibi açık isteklerde — önce bu indeks, ardından [AGENT_PRODUCTION_DEPLOY.md](./AGENT_PRODUCTION_DEPLOY.md) veya [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md).

**Kaldığımız yer / IT sonrası devam:** **[DEVAM.md](./DEVAM.md)** (4 Haziran 2026 — mng_common dosyaları hazır, Docker bekliyor)

---

## Ortam özeti

| Rol | IP | SSH kullanıcı | Günlük geliştirme / test deploy |
|-----|-----|---------------|----------------------------------|
| **Test (Odak POC)** | `192.168.20.20` | `odak` | ✅ Varsayılan — [../deploy/README.md](../deploy/README.md) |
| **Production** | `192.168.20.8` | `odak` | ❌ Yalnızca bilinçli prod deploy |

Ayrıntılı karşılaştırma: [ENVIRONMENTS.md](./ENVIRONMENTS.md).

---

## Dokümanlar

| Dosya | İçerik |
|-------|--------|
| **[DEVAM.md](./DEVAM.md)** | **Checkpoint** — sıradaki adımlar, IT listesi |
| **[INDEPENDENCE.md](./INDEPENDENCE.md)** | **Tam bağımsızlık** — paylaşılan altyapı/veri yok |
| [ENVIRONMENTS.md](./ENVIRONMENTS.md) | Test vs production; URL/port matrisi |
| [SERVER_ACCESS.md](./SERVER_ACCESS.md) | SSH, kimlik bilgisi (repoda parola yok) |
| [INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md) | Production sunucuda **ilk kez** kurulum checklist |
| [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md) | Günlük / kısmi / tam production deploy komutları |
| [AGENT_PRODUCTION_DEPLOY.md](./AGENT_PRODUCTION_DEPLOY.md) | Cursor / agent için kısa talimat seti |
| [env.prod.server.example](./env.prod.server.example) | Sunucu `mng_apps/.env` için IP şablonu (`20.8`) |
| [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md) | Canlı sunucu kontrol listesi ve engeller |

---

## Hızlı akış (production)

```
① Yerel: .env.odak.prod.local (bir kez, gitignore)
② sync-odak-prod.ps1
③ setup-mng-common-odak-prod.ps1  (ilk kurulum, Docker gerekir)
④ deploy-odak-prod.ps1
⑤ Doğrulama: http://192.168.20.8:3000/ , :5040/health
```

**Sunucu durumu:** [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md) · **Devam:** [DEVAM.md](./DEVAM.md)  
Komut ayrıntıları: [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md).

---

## İlk production kurulumu

Production makinesi henüz `mng_common` + `MonitraNG` içermiyorsa sırayla:

1. [INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md)
2. Test sunucudaki deneyimle aynı Keycloak / domain / secret adımları — URL’ler `192.168.20.8`
3. İlk tam deploy: `sync -Full` + `deploy` (production `-Server` ile)

Test sunucusundaki kurulum rehberi (referans): [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md), [../setup/MNG_APPS_ODAK_DEPLOY.md](../setup/MNG_APPS_ODAK_DEPLOY.md).

---

## Yerel kimlik bilgisi (geliştirme PC)

| Dosya | Ortam |
|-------|--------|
| `.env.odak.local` | Test sunucu `192.168.20.20` (mevcut) |
| `.env.odak.prod.local` | Production `192.168.20.8` (yeni) |

Şablon: repo kökünde `.env.odak.prod.local.example` → kopyala `.env.odak.prod.local` ve müşteri SSH parolasını doldurun. **Parolayı repoya commit etmeyin.**

---

## İlgili (test ortamı)

| Doküman | İçerik |
|---------|--------|
| [../deploy/README.md](../deploy/README.md) | Test sunucu günlük deploy |
| [../setup/MNG_APPS_ODAK_DEPLOY.md](../setup/MNG_APPS_ODAK_DEPLOY.md) | Deploy stratejisi (genel) |
| [../../../scripts/odak/sync-odak-source.ps1](../../../scripts/odak/sync-odak-source.ps1) | Kaynak senkron |
| [../../../scripts/odak/deploy-odak-apps.ps1](../../../scripts/odak/deploy-odak-apps.ps1) | Uzaktan compose build/up |

---

**Oluşturulma:** 4 Haziran 2026 — müşteri kararı: `192.168.20.20` test, `192.168.20.8` production.
