# Odak — mng_common altyapı erişim bilgileri (müşteri IT)

**Ortam:** Odak / dahili test  
**Sunucu:** `192.168.20.20` (hostname: `monitrang`)  
**Kurulum:** `docker-compose.yml` + `docker-compose.odak.yml` (GitLab, Nginx, MkDocs **yok**)  
**Dizin (sunucu):** `/home/odak/mng_common`  
**Doküman tarihi:** 22 Mayıs 2026  
**Doğrulama:** mng_common altyapısı sunucuda çalışır durumda; Keycloak Admin UI `http://192.168.20.20:8080/keycloak/admin/master/console/`

> Bu dosya müşteri IT ekibine teslim içindir. Şifreleri e-posta ile ayrı kanaldan da iletebilirsiniz. Üretimde per-servis farklı parola kullanılması önerilir.

---

## 1. Sunucu erişimi (SSH)

| Alan | Değer |
|------|--------|
| Adres | `192.168.20.20` |
| Port | `22` |
| Kullanıcı | `odak` (günlük işlemler) |
| Root SSH | Kapalı (yalnızca `su` ile yükseltme) |
| Parola | Müşteri tarafından sağlanır — **bu tabloda tutulmaz** |

Docker komutları: `odak` kullanıcısı `docker` grubundadır.

---

## 2. Servis özeti — port ve URL

| Servis | Amaç | Host portu | Erişim adresi (örnek) |
|--------|------|------------|------------------------|
| Keycloak | Kimlik / SSO | `8080` | http://192.168.20.20:8080/keycloak |
| Keycloak Admin | Yönetim konsolu | `8080` | http://192.168.20.20:8080/keycloak/admin/master/console/ |
| PostgreSQL | Keycloak veritabanı | — | Yalnızca Docker ağı (`postgres:5432`) |
| MongoDB | Uygulama verisi | `27017` | `mongodb://192.168.20.20:27017` |
| Mongo Express | Mongo web UI | `8081` | http://192.168.20.20:8081 |
| Redis | Önbellek | `6379` | `192.168.20.20:6379` |
| Redis Commander | Redis web UI | `8001` | http://192.168.20.20:8001 |
| RabbitMQ AMQP | Mesaj kuyruğu | `5672` | `192.168.20.20:5672` |
| RabbitMQ Management | Yönetim UI | `15672` | http://192.168.20.20:15672 |
| Mosquitto MQTT | MQTT | `1883` | `mqtt://192.168.20.20:1883` |
| Mosquitto WebSocket | MQTT WS | `9001` | `ws://192.168.20.20:9001` |
| MinIO API | Nesne depolama | `9090` | http://192.168.20.20:9090 |
| MinIO Console | MinIO arayüz | `9091` | http://192.168.20.20:9091 |
| Portainer | Konteyner yönetimi | `9000` | http://192.168.20.20:9000 |
| Seq | Log arama | `5341` | http://192.168.20.20:5341 |
| Node-RED | Akış / entegrasyon | `1880` | http://192.168.20.20:1880 |

**Dahil değil (Odak):** GitLab, GitLab Runner, Nginx (80/443), MkDocs.

---

## 3. Kullanıcı adı ve parolalar

Odak kurulumunda altyapı servisleri için **ortak parola seti** kullanılmıştır (test/POC). Mongo ve MQTT istisnadır.

### 3.1 Ortak altyapı parolası

| Parola (ortak) | `Odak@Infra2026!` |
|----------------|-------------------|

Aşağıdaki servislerde bu parola geçerlidir (kullanıcı adları tabloda).

### 3.2 Servis bazlı kimlik bilgileri

| Servis | Kullanıcı adı | Parola | Not |
|--------|---------------|--------|-----|
| **Keycloak Admin** | `admin` | `Odak@Infra2026!` | Admin konsolu girişi |
| **PostgreSQL (Keycloak DB)** | `keycloak` | `Odak@Infra2026!` | DB: `keycloak` |
| **MongoDB** | `admin` | `admin123` | `mongo-init` ile uyumlu; uygulama bağlantıları |
| **Mongo Express** | — | — | Basic Auth **kapalı** (yalnızca ağ güvenliği) |
| **Redis** | — | `Odak@Infra2026!` | `AUTH` ile |
| **Redis Commander** | — | (Redis parolası üzerinden) | |
| **RabbitMQ** | `admin` | `Odak@Infra2026!` | Management UI aynı bilgiler |
| **MinIO** | `admin` | `Odak@Infra2026!` | Konsol + API root |
| **Seq** | `admin` | `Odak@Infra2026!` | İlk kurulum admin |
| **Node-RED** | `admin` | `Odak@Infra2026!` | Ortam değişkeni ile |
| **Mosquitto MQTT** | `monitrang` | `!2345qawsedrf` | `mosquitto/config/passwd` ile uyumlu |
| **Portainer** | (ilk girişte oluşturulur) | — | İlk açılışta admin hesabı tanımlanır |

### 3.3 Uygulama bağlantı dizeleri (referans)

| Amaç | Değer |
|------|--------|
| MongoDB URI | `mongodb://admin:admin123@192.168.20.20:27017/?authSource=admin` |
| MongoDB (DB adı) | `mngkeeper` |
| Redis | `192.168.20.20:6379`, parola: `Odak@Infra2026!` |
| RabbitMQ | `amqp://admin:Odak@Infra2026!@192.168.20.20:5672/` |
| MinIO endpoint | `192.168.20.20:9090`, kullanıcı: `admin`, parola: `Odak@Infra2026!` |
| Keycloak (dış) | `http://192.168.20.20:8080/keycloak` |
| Keycloak (Docker içi) | `http://keycloak:8080/keycloak` |
| MQTT | Host: `192.168.20.20`, port: `1883`, user: `monitrang`, pass: `!2345qawsedrf` |

---

## 4. Ortam dosyası (.env) eşlemesi

Sunucudaki dosya: `/home/odak/mng_common/.env`

| Değişken | Değer |
|----------|--------|
| `MONGO_ROOT_USERNAME` | `admin` |
| `MONGO_ROOT_PASSWORD` | `admin123` |
| `MONGO_INITDB_DATABASE` | `mngkeeper` |
| `KEYCLOAK_ADMIN_USERNAME` | `admin` |
| `KEYCLOAK_ADMIN_PASSWORD` | `Odak@Infra2026!` |
| `POSTGRES_DB` | `keycloak` |
| `POSTGRES_USER` | `keycloak` |
| `POSTGRES_PASSWORD` | `Odak@Infra2026!` |
| `REDIS_PASSWORD` | `Odak@Infra2026!` |
| `RABBITMQ_DEFAULT_USER` | `admin` |
| `RABBITMQ_DEFAULT_PASS` | `Odak@Infra2026!` |
| `MINIO_ROOT_USER` | `admin` |
| `MINIO_ROOT_PASSWORD` | `Odak@Infra2026!` |
| `SEQ_ADMIN_PASSWORD` | `Odak@Infra2026!` |
| `NODE_RED_USERNAME` | `admin` |
| `NODE_RED_PASSWORD` | `Odak@Infra2026!` |
| `MQTT_USERNAME` | `monitrang` |
| `MQTT_PASSWORD` | `!2345qawsedrf` |
| `ODAK_KEYCLOAK_HOSTNAME` | `192.168.20.20` |
| `ODAK_KEYCLOAK_PORT` | `8080` |

---

## 5. Operasyon komutları

```bash
cd /home/odak/mng_common

# Durum
docker compose -f docker-compose.yml -f docker-compose.odak.yml ps

# Başlat / durdur
docker compose -f docker-compose.yml -f docker-compose.odak.yml up -d
docker compose -f docker-compose.yml -f docker-compose.odak.yml down

# Log
docker compose -f docker-compose.yml -f docker-compose.odak.yml logs -f keycloak
```

---

## 6. Keycloak Admin UI

**Doğru adres:** http://192.168.20.20:8080/keycloak/admin/master/console/

| Alan | Değer |
|------|--------|
| Kullanıcı | `admin` |
| Parola | `Odak@Infra2026!` |

**Yanlış adresler (çalışmaz):** `http://192.168.20.20:8080/admin`, `http://192.168.20.20/keycloak/...` (port **8080** olmadan).

Sunucu `KC_HOSTNAME_PORT=8080` ile yapılandırılmıştır; port olmadan açılırsa arayüz API çağrılarında hata verebilir.

---

## 7. Güvenlik notları (IT)

1. Servis portları dahili ağa açıktır; firewall ile kısıtlama önerilir.
2. Mongo Express’te HTTP Basic Auth kapalıdır; doğrudan internete açmayın.
3. Ortak parola yalnızca Odak POC içindir; üretimde servis başına güçlü ve farklı parola kullanın.
4. Portainer ilk girişte ayrı admin hesabı isteyecektir.
5. Parola değişikliği sonrası `docker compose ... up -d` ile ilgili servisleri yeniden başlatın.

---

## 8. İlgili dokümanlar

- [MNG_COMMON_ODAK.md](./MNG_COMMON_ODAK.md) — kurulum adımları
- [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md) — sıradaki: uygulama servisleri
- [MNG_APPS_ODAK_MUSTERI_ERISIM.md](./MNG_APPS_ODAK_MUSTERI_ERISIM.md) — uygulama portları (IT)
- [KURULUM.md](./KURULUM.md) — sunucu hazırlığı
- [MOSQUITTO_CREDENTIALS.md](../../content/infrastructure/MOSQUITTO_CREDENTIALS.md) — MQTT ayrıntıları
