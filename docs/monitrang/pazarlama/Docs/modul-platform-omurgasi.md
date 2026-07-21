# Platform omurgası — Kimlik, veri, bildirim

**Kod:** `platform-omurga` · **Bileşenler:** MngKeeper · MngDataGateway · MngNotifier · *(altyapı: MngGateway, RabbitMQ, MongoDB…)*

**Referanslar:** [.cursorrules](../../../.cursorrules) kısaltmaları · Servis README’leri · [WORKFLOW_PLANNING.md](../../content/workflow/WORKFLOW_PLANNING.md) (DG olayları)

> **Bu dosyanın amacı:** Tüm modüllerin dayandığı **omurga** katmanını müşteri ve pazarlama dilinde tanımlamak. Ayrı broşür modülü değil — **her modül dokümanı buraya referans verir**.

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Platform omurgası**, MonitraNG modüllerinin ortak **kimlik**, **veri** ve **bildirim** altyapısıdır — her uygulama aynı kullanıcı, aynı tenant (domain) ve aynı iletişim kanalları üzerinde çalışır.

### 1.2 Müşteri perspektifi

Kurum MonitraNG’yi modül modül alsın ya da tam platform kullansın:

- Kullanıcı **bir kez** giriş yapar (SSO / Keycloak realm)
- Veri **tenant izolasyonlu** saklanır
- E-posta, Telegram ve uygulama içi bildirim **tek servisten** gider
- Yeni modül eklendiğinde ayrı «kullanıcı listesi» ve «mail sunucusu» kurulumu tekrarlanmaz

**Broşür cümlesi:** *«Tek omurga — kimlik, veri, bildirim. Modüller bu temelin üzerinde birleşir.»*

---

## 2. Bileşenler

### 2.1 MngKeeper — Kimlik ve dizin

| Yetenek | Durum | Müşteri dili |
|---------|-------|----------------|
| Domain (tenant) yönetimi | ✅ | «Her müşteri kendi kurumsal alanı» |
| Kullanıcı, grup, rol | ✅ | «Kim neye erişir» |
| Keycloak entegrasyonu | ✅ | Kurumsal IAM |
| Token / oturum | ✅ | Tüm API’ler Bearer JWT |
| Person / dizin senkronu | 🔶 | LDAP periyodik sync (Scheduler) |
| Service account (Scheduler, Workflow…) | ✅ | Arka plan işleri güvenli |

**Modül kullanımı:** OC atama, Raporlama yetkisi, DI klasör izni — hepsi Keeper kimliği ve gruplarına dayanır.

### 2.2 MngDataGateway (DG) — Veri omurgası

| Yetenek | Durum | Müşteri dili |
|---------|-------|----------------|
| Dataset tanımı (şema, alan, relation) | ✅ | «Esnek kurumsal veri modeli» |
| CRUD + query + aggregate | ✅ | Raporlama, OC, Monitoring envanteri |
| Field / expression validation | ✅ | «Kayıt girilmeden kural kontrolü» |
| HTTP validation hook | ✅ | Harici doğrulama |
| Grup bazlı veri erişimi | ✅ | Tenant içi yetki |
| Olay yayını (RabbitMQ) | ✅ | Modül olayı → Workflow |
| MinIO object (şifreli içerik) | ✅ | DI dosya depolama |
| `@` prefix sistem dataset’leri | ✅ | `@reporting_*`, `@scheduled_jobs`… |

**Modül kullanımı:**

| Modül | DG rolü |
|-------|---------|
| Operasyon Merkezi | `op_*` metadata |
| Raporlama | Sorgu kaynağı + katalog |
| Monitoring | `mon_*` asset envanteri |
| Döküman Zekası | `dm_*` kaynak metadata |
| Workflow | Tanım/trigger dataset’leri |

### 2.3 MngNotifier — Bildirim omurgası

| Yetenek | Durum | Müşteri dili |
|---------|-------|----------------|
| E-posta gönderimi | ✅ | SMTP uygulamalarda değil — merkezi |
| Telegram | 🔶 | Kanal genişlemesi |
| In-app bildirim | ✅ | OC, SIEM, Raporlama olayları |
| Template key | ✅ | «Aynı mail şablonu birçok modülde» |

**Modül kullanımı:** OC atama maili, DI bildirimi, alarm bildirimi, rapor hazır — **Notifier**; içerik modül veya template politikasından gelir.

### 2.4 Altyapı (müşteriye kısa)

| Bileşen | Rol | Broşürde |
|---------|-----|----------|
| **MngGateway** | API yönlendirme, TLS sonlandırma | «Tek giriş adresi» |
| **MongoDB** | Domain veritabanları | Detay ops |
| **RabbitMQ** | Olay ve async iş kuyruğu | «Modüller olayla konuşur» |
| **Redis** | Cache (Keeper dizin vb.) | Detay ops |
| **MinIO** | Object storage | DI / yedek |

---

## 3. Omurga × modül haritası

```text
                    ┌─────────────────────────┐
                    │   MngGateway (API)      │
                    └───────────┬─────────────┘
                                │
         ┌──────────────────────┼──────────────────────┐
         ▼                      ▼                      ▼
   MngKeeper              MngDataGateway          MngNotifier
   (kimlik)               (veri + olay)           (bildirim)
         │                      │                      │
         └──────────────────────┼──────────────────────┘
                                │
              DI · OC · Raporlama · Monitoring · SIEM · Workflow …
```

---

## 4. Müşteriye net sınırlar

| Soru | Cevap |
|------|-------|
| «Her modül ayrı veritabanı mı?» | **Domain bazlı** izolasyon; DG dataset modeli |
| «Mail SMTP nerede?» | **Notifier** — modül başına ayrı SMTP değil |
| «LDAP var mı?» | Keeper / dizin sync — yapılandırmaya bağlı |
| «Omurga ayrı satılır mı?» | Platform **temeli** — modüller omurgaya dayanır |

---

## 5. Teknik referans (iç)

| Servis | Repo / API |
|--------|------------|
| MngKeeper | `MngKeeper/` · `/keeper/api/` |
| MngDataGateway | `MngDataGateway/` · `/api/v1/data/` |
| MngNotifier | `MngNotifier/` · `/api/v1/notifications/` |
| MngGateway | `MngGateway/` · Ocelot |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · v0.1*
