# MonitraNG — Odak ortamı

**Test sunucu (günlük deploy / POC):** `192.168.20.20` (`monitrang`)  
**Production sunucu:** `192.168.20.8` — test’ten **tamamen bağımsız** kendi mng_common + uygulama yığını: [proddeploy/INDEPENDENCE.md](./proddeploy/INDEPENDENCE.md), [proddeploy/README.md](./proddeploy/README.md)  
**Durum:** Kurulum ✅ · LDAP K1–K5 + G1 POC ✅ **(duraklatıldı)** · Yeni geliştirme: bu rehber + ürün chat’i

---

## Başlangıç noktası

Tüm kurulum ve günlük çalışma akışı tek dokümanda:

| Doküman | İçerik |
|---------|--------|
| **[ODAK_FULL_SETUP.md](./ODAK_FULL_SETUP.md)** | Tam kurulum özeti: sunucu, mng_common, mng_apps, domain, initial data, yerel dev, deploy, bilinen sorunlar |
| **[operationcore/README.md](./operationcore/README.md)** | Operation Core (OC) / MngOperations — planlama ve spec |
| **[workflow/DEVAM.md](./workflow/DEVAM.md)** | MngWorkflow — planlama, Faz 0/1, OC entegrasyon |
| **[diagnostic/README.md](./diagnostic/README.md)** | Performans ölçümü — Faz 1+1B UI deploy tamam; Faz 2 backend bekliyor |
| **[deploy/README.md](./deploy/README.md)** | **Test deploy (Windows)** — `192.168.20.20`, pwsh, UI/backend |
| **[proddeploy/DEVAM.md](./proddeploy/DEVAM.md)** | **Production deploy — kaldığımız yer** (IT / Docker bekliyor) |
| **[proddeploy/README.md](./proddeploy/README.md)** | Production deploy indeks — `192.168.20.8` |
| **[PLATFORM_CHECKPOINT.md](./PLATFORM_CHECKPOINT.md)** | **SIEM öncesi checkpoint** — C1–C7 ✅ |
| **[PLATFORM_HANDOFF.md](./PLATFORM_HANDOFF.md)** | **Platform UI kaldığımız yer** — Operasyon / Alarm / Otomasyon modülleri |
| **[monitoring/SIEM_FAZ1_HANDOFF.md](./monitoring/SIEM_FAZ1_HANDOFF.md)** | **SIEM Faz 1 implementasyon** (ayrı chat) |
| **[monitoring/README.md](./monitoring/README.md)** | Güvenlik odaklı izleme / SIEM-hafif — planlama |
| **[AI_PLANNING_DECISION.md](./AI_PLANNING_DECISION.md)** | **Yapay zeka zamanlama kararı** — çerçeve şimdi, implementasyon çekirdek hat sonrası |
| **[compliance/DEVAM.md](./compliance/DEVAM.md)** | Standart uyumluluğu — ISO 27001 + AS9100 checkpoint (müşteri özeti + Faz C1) |
| **[notifications/DEVAM.md](./notifications/DEVAM.md)** | MngNotifier e-posta — ⏸️ planlama duraklatildi; `send-template` sirada |
| **[widgets/README.md](./widgets/README.md)** | **Widget & Dashboard designer** — planlama tamam; [DEVAM.md](./widgets/DEVAM.md) |
| **[dynamicforms/README.md](./dynamicforms/README.md)** | **Dinamik formlar** — Tedarikçiler AF CRUD POC; yarın: [DEVAM.md](./dynamicforms/DEVAM.md) |

Yeni bir chat’te geliştirmeye geçerken önce **ODAK_FULL_SETUP** okuyun; ayrıntı için alt bölümlerdeki linklere inin. OC geliştirmesi için **operationcore** klasörüne; bildirim planlaması için **notifications** klasörüne; widget/dashboard mimarisi için **widgets** klasörüne; dinamik form planlaması için **dynamicforms** klasörüne bakın.

---

## Doküman ağacı

```
docs/odak/
├── ODAK_FULL_SETUP.md          ← ana rehber (bu oturumun özeti)
├── README.md                   ← bu dosya
├── AI_PLANNING_DECISION.md     ← AI zamanlama kararı (✅ kilitli)
├── PLATFORM_CHECKPOINT.md      ← C1–C7 SIEM-ready
├── PLATFORM_HANDOFF.md         ← Platform UI handoff (Operasyon / Alarm / Otomasyon)
├── deploy/
│   └── README.md               ← Test deploy (192.168.20.20)
├── alarm/
│   ├── README.md
│   ├── DEVAM.md
│   └── scripts/                ← patch-alarm-center-side-menu.ps1
├── automation/
│   ├── README.md
│   └── scripts/                ← patch-automation-side-menu.ps1
├── proddeploy/
│   ├── DEVAM.md                ← Production checkpoint (IT sonrası devam)
│   ├── README.md               ← Production deploy (192.168.20.8)
│   ├── DEPLOY_PRODUCTION.md
│   └── AGENT_PRODUCTION_DEPLOY.md
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
│   ├── DEVAM.md                  ← LDAP durum + duraklatma (25 May 2026)
│   ├── HANDOFF_UI.md             ← UI tamamlandı (arşiv)
│   ├── HANDOFF_MNGSCHEDULER.md   ← K3 tamamlandı
│   ├── ODAK_HTTP_AND_GATEWAY.md← HTTP POC (HTTPS ertelendi)
│   └── README.md
├── operationcore/
│   ├── README.md
│   ├── major_plan.md
│   ├── operationcore_phase1.md
│   ├── OPERATION_CORE_IMPLEMENTATION_PLAN.md
│   └── datasets/
│       ├── README.md
│       └── operationcore_datasets_phase1_*.json
├── workflow/
│   ├── DEVAM.md                ← kaldığımız yer (Faz 0/1 + OC entegrasyon planı)
│   ├── Workflow Backend Implementation Plan v1.md  ← §13 Operation Core
│   └── … (InternalDesign, planing.md, …)
├── monitoring/
│   ├── README.md               ← Güvenlik odaklı izleme / SIEM-hafif (index)
│   ├── SIEM_PLANNING.md        ← Ana plan: gap, şema, toplama, U1–U7, fazlar (Faz 0 ✅)
│   ├── SIEM_PARSER_PLAN.md     ← Parser/normalizer (Faz 1)
│   ├── SIEM_FAZ1_SPIKE.md      ← Faz 1 teknik spike (workflow sonrası)
│   ├── SIEM_THROUGHPUT_AND_QUEUES.md ← Kuyruk / yoğun veri
│   ├── SIEM_PERFORMANCE_PLAN.md    ← Performans / SLO / benchmark
│   ├── SIEM_VERTICAL_FINANCE.md ← Finans/dijital banka dikey kapsam
│   └── DEVAM.md                ← kaldığımız yer
├── compliance/
│   ├── README.md               ← ISO 27001 + AS9100 uyum planı (index)
│   ├── ISO27001_PLAN.md        ← ISO/IEC 27001:2022 kontrol eşleme + boşluk
│   ├── AS9100_PLAN.md          ← AS9100D gereksinim eşleme + boşluk
│   └── COMPLIANCE_ROADMAP.md   ← birleşik fazlı yol haritası + izlenebilirlik matrisi
├── notifications/
│   ├── README.md               ← bildirim planlama index + doküman haritası
│   ├── MAIL_ARCHITECTURE.md    ← push-only HTTP; Notifier event dinlemez
│   ├── MAIL_TEMPLATES.md       ← Notifier template ozeti
│   ├── datasets/               ← @mail_templates, @mail_layouts sema + seed
│   ├── MEVCUT_DURUM.md         ← kod gerçeği (MngNotifier, MO, Keeper, chat)
│   ├── DEVAM.md                ← ⏸️ kaldigimiz yer (send-template sirada)
├── widgets/
│   ├── README.md
│   ├── ARCHITECTURE.md
│   ├── MANIFEST_SCHEMA.md
│   ├── DATA_CATALOG.md
│   ├── KATALOG_V1.md
│   ├── DESIGNER_UX.md
│   ├── PRESENTATION_PRESETS.md
│   ├── INTERACTIVITY_MODEL.md
│   ├── DOMAIN_*.md (alarm, siem, operation-core, document-intelligence)
│   ├── DEVAM.md
│   ├── schemas/widget-manifest-v1.schema.json
│   └── datasets/
├── document_intelligence/
│   ├── DEVAM.md                ← Faz 1 ✅; Faz 2 OC entegrasyon
│   └── …                       ← widget: ../widgets/DOMAIN_DOCUMENT_INTELLIGENCE.md
├── dynamicforms/
│   ├── README.md               ← Dinamik form oluşturma planlama (index)
│   └── DEVAM.md                ← kaldığımız yer
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
