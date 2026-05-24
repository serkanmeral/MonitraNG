# MonitraNG — Odak ortamı

**POC / müşteri test sunucusu:** `192.168.20.20` (`monitrang`)  
**Durum:** Kurulum + LDAP K1–K4 + **K3** Scheduler ✅ · Sıradaki: **UI** ([ldap/HANDOFF_UI.md](./ldap/HANDOFF_UI.md))

---

## Başlangıç noktası

Tüm kurulum ve günlük çalışma akışı tek dokümanda:

| Doküman | İçerik |
|---------|--------|
| **[ODAK_FULL_SETUP.md](./ODAK_FULL_SETUP.md)** | Tam kurulum özeti: sunucu, mng_common, mng_apps, domain, initial data, yerel dev, deploy, bilinen sorunlar |

Yeni bir chat’te geliştirmeye geçerken önce **ODAK_FULL_SETUP** okuyun; ayrıntı için alt bölümlerdeki linklere inin.

---

## Doküman ağacı

```
docs/odak/
├── ODAK_FULL_SETUP.md          ← ana rehber (bu oturumun özeti)
├── README.md                   ← bu dosya
├── setup/
│   ├── KURULUM.md              ← sunucu, Docker, SSH
│   ├── MNG_COMMON_ODAK.md
│   ├── MNG_COMMON_ODAK_MUSTERI_ERISIM.md
│   ├── MNG_APPS_ODAK.md
│   ├── MNG_APPS_ODAK_DEPLOY.md
│   ├── MNG_APPS_ODAK_MUSTERI_ERISIM.md
│   └── README.md
├── ui/
│   ├── WELCOME_HOME.md         ← ana sayfa (/) ve modül kartları
│   └── README.md
├── ldap/
│   ├── HANDOFF_UI.md             ← sonraki chat (UI)
│   ├── HANDOFF_MNGSCHEDULER.md   ← K3 tamamlandı
│   ├── DEVAM.md                ← faz durumu
│   ├── ODAK_HTTP_AND_GATEWAY.md← HTTP POC (HTTPS ertelendi)
│   └── README.md
└── domain/
    ├── DOMAIN_OLUSTURMA.md
    ├── DOMAIN_OLUSTURMA_API.md
    ├── DOMAIN_OLUSTURMA_KAYIT.md
    └── README.md
```

---

## Hızlı erişim (sunucu)

| Servis | URL |
|--------|-----|
| Ana UI | http://192.168.20.20:3000 |
| Domain UI | http://192.168.20.20:3001/domain/ |
| API Gateway | http://192.168.20.20:5040 |
| MngKeeper | http://192.168.20.20:5001 |
| Keycloak Admin | http://192.168.20.20:8080/keycloak/admin/master/console/ |

Kimlik bilgileri: [setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md)

---

## Script’ler (repo kökünden)

| Script | Amaç |
|--------|------|
| `scripts/odak/sync-odak-source.ps1` | PC → sunucu kaynak senkronu |
| `scripts/odak/deploy-odak-apps.ps1` | Sunucuda mng_apps build + up |
| `scripts/odak/import-template-to-odak.ps1` | Initial data şablonu import (MinIO + Mongo) |
