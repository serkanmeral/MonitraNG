# Chat Room — Backend güncellemeleri ve Docker denemesi

**Amaç:** F2 sırasında hangi servisi ne zaman durdurup yeniden yayınlayacağınızı tek yerde toplamak. Ürün kararları ve şema için bkz. [CHAT_ROOM_ROADMAP.md](CHAT_ROOM_ROADMAP.md).

---

## 1. Servis sırası (önerilen)

| Sıra | Servis | Tipik değişiklik | Neden bu sırada |
|------|--------|------------------|-----------------|
| 1 | **MngDataGateway** | `cht_*` validation, HTTP validation (Keeper), event/publish ince ayarı | Veri ve RMQ kaynağı burada. |
| 2 | **MngHub** | `MessageRouter` / Hub method / routing key dinleme | DG → RMQ → SignalR hattının canlı kısmı. |
| 3 | **MngGateway** | `ocelot.json` (yeni upstream route gerekirse) | İstemci dışarıdan Gateway üzerinden gelir. |
| 4 | **MngNotifier** | Mention → mail/event consumer | MVP’de mention push; DG/Hub’dan sonra veya paralel. |
| 5 | **MngKeeper** | Çoğunlukla **değişiklik yok**; yalnız yeni API ihtiyacı çıkarsa | Grup üyeliği kaynağı zaten Keeper. |

**Not:** Yalnızca dokümantasyon veya script değiştiyse ilgili container’ı yeniden build etmeniz gerekmez.

---

## 2. Compose konumu

Yerel / birleşik stack genelde:

`ApplicationResources/mng_apps/docker-compose.yml` (veya `docker-compose.production.yml`)

Servis adları (örnek): `mngdatagateway`, `mnghub`, `mnggateway`, `mngnotifier` — compose dosyanızda `container_name` ile doğrulayın.

---

## 3. Adım adım: tek servisi güncelleme

Aşağıdaki komutlar **örnek**tir; çalışma dizini olarak `mng_apps` klasörünü kullanın (`docker-compose.yml`’in olduğu yer).

### 3.1 Sadece MngDataGateway

```powershell
cd ApplicationResources/mng_apps
docker compose stop mngdatagateway
# Projede publish/build (CI veya dotnet publish / docker build - sizin akışınız)
docker compose build mngdatagateway
docker compose up -d mngdatagateway
```

**Doğrulama:** Gateway üzerinden `GET /data/api/v1/datasets/cht_messages` (veya doğrudan DG health).

### 3.2 Sadece MngHub

```powershell
docker compose stop mnghub
docker compose build mnghub
docker compose up -d mnghub
```

**Bağımlılık:** RabbitMQ ayakta olmalı; DG değişmediyse Hub tek başına yeniden yeterli.

### 3.3 MngGateway (route değiştiyse)

```powershell
docker compose stop mnggateway
docker compose build mnggateway
docker compose up -d mnggateway
```

**Not:** Gateway genelde Hub ve DG’ye proxy eder; route eklemediyseniz Hub/DG yenilemesi yeterli olabilir.

### 3.4 MngNotifier (mention hattı)

```powershell
docker compose stop mngnotifier
docker compose build mngnotifier
docker compose up -d mngnotifier
```

---

## 4. İlgili servis + doküman eşlemesi

| Servis | Roadmap / not (güncellenir) |
|--------|-----------------------------|
| MngHub | `docs/content/MngHub/main/ROADMAP.md`, repo `MngHub/ROADMAP.md` |
| MngDataGateway | `docs/content/MngDataGateway/main/ROADMAP.md` |
| MngGateway | `docs/content/MngGateway/main/ROADMAP.md` |
| MngNotifier | `docs/content/MngNotifier/main/ROADMAP.md` |
| Genel sohbet ürünü | [CHAT_ROOM_ROADMAP.md](CHAT_ROOM_ROADMAP.md) |

Kod veya davranış değiştikçe hem **bu dosyadaki adımlar** hem de ilgili satırın **servis roadmap’i** güncellenmelidir.

---

## 5. Hızlı sağlık kontrolü

- **MngHub:** `GET http://mnghub:5020/health` (iç ağ) veya Gateway üzerinden tanımlı health route.
- **MngDataGateway:** Dataset listesi / `cht_*` varlığı.
- **SignalR:** UI veya test istemcisi ile Gateway `hub` yolu üzerinden bağlantı.

---

*Son güncelleme: 29 Nisan 2026 — Chat Room F2 backend çalışması ile uyumlu.*
