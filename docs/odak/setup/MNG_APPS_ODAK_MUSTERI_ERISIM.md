# Odak — mng_apps erişim bilgileri (müşteri IT)

**Sunucu:** `192.168.20.20`  
**Ön koşul:** mng_common altyapısı çalışıyor ([MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md))  
**Dizin:** `/home/odak/MonitraNG/ApplicationResources/mng_apps`  
**Tarih:** 22 Mayıs 2026

> Uygulama parolaları (Mongo, Keycloak, Redis vb.) altyapı dokümanındaki değerlerle aynıdır. Bu dosya **uygulama portları ve URL’leri** içindir.

---

## 1. Kullanıcı arayüzleri

| Uygulama | URL | Not |
|----------|-----|-----|
| **MngUI** (ana UI) | http://192.168.20.20:3000 | Tarayıcı girişi |
| **MngDomainUI** | http://192.168.20.20:3001/domain/ | Domain yönetimi |
| **API Gateway** | http://192.168.20.20:5040 | REST API / Swagger |
| **Keycloak** (kimlik) | http://192.168.20.20:8080/keycloak | Altyapı dokümanına bakın |

---

## 2. API ve backend servisleri (doğrudan port)

Nginx olmadığı için servisler host portlarıyla erişilir (debug / entegrasyon).

| Servis | Port | Health / Swagger (örnek) |
|--------|------|---------------------------|
| MngGateway | 5040 | http://192.168.20.20:5040/health |
| MngKeeper | 5001 | http://192.168.20.20:5001/health |
| MngDataGateway | 5010 | http://192.168.20.20:5010/health |
| ~~MngReactor~~ | — | Odak’ta kapalı (kaynak repoda yok) |
| MngHub | 5020 | http://192.168.20.20:5020/health |
| ~~MngLLM~~ | — | Odak’ta kapalı (Ollama yok) |
| MngScheduler | 5090 | http://192.168.20.20:5090/health |
| MngWorkflow | 5085 | http://192.168.20.20:5085/health |
| MngAdmin | 5080 | http://192.168.20.20:5080/health |
| MngNotifier | 5070 | http://192.168.20.20:5070/health |

---

## 3. Altyapı bağlantıları (container içi adlar)

Uygulama konteynerleri Docker ağı üzerinden şu host adlarını kullanır:

| Bileşen | Adres (container içi) |
|---------|------------------------|
| MongoDB | `mongo:27017` |
| Redis | `redis:6379` |
| RabbitMQ | `rabbitmq:5672` |
| Mosquitto | `mosquitto:1883` |
| MinIO | `minio:9000` |
| Keycloak | `http://keycloak:8080/keycloak` |

Detaylı kullanıcı/parola: [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md).

---

## 4. Ortam dosyası (.env) — özet

| Değişken | Odak değeri (özet) |
|----------|---------------------|
| `OPENAPI_SERVER_PATH` | `http://192.168.20.20:5040` |
| `GATEWAY_URL` | `http://192.168.20.20:5040` |
| `CORS_ALLOWED_ORIGIN_1` | `http://192.168.20.20:3000` |
| `MNG_KEEPER_UI_BASE_URL` | `http://192.168.20.20:3000` |
| `KEYCLOAK_CLIENT_SECRET` | Keycloak’ta client oluşturulduktan sonra `.env` içine yazılır |
| `MNGKEEPER_LICENSE_MASTER_KEY` | En az 32 karakter; `.env` içinde (repoda yok) |

Tam şablon: `ApplicationResources/mng_apps/.env.odak.example`

---

## 5. Dahil değil (Odak)

| Bileşen | Neden |
|---------|--------|
| **Ollama** | RAM; kapalı |
| **MngLLM** | Ollama’ya bağlı; kapalı |
| **Nginx** | Odak’ta reverse proxy yok |

---

## 6. Operasyon

```bash
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env ps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env logs -f mnggateway
```

---

## 7. İlgili dokümanlar

- [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md) — kurulum adımları ve Keycloak ön koşulları
- [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md) — altyapı port/parola
