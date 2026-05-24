# Odak — mng_common minimal kurulum (GitLab + Nginx yok)

**Hedef sunucu:** `192.168.20.20` (`monitrang`)  
**Dosya:** `ApplicationResources/mng_common/docker-compose.odak.yml` (ana `docker-compose.yml` üzerine override)  
**Durum:** Kuruldu ve doğrulandı (22 Mayıs 2026) — sunucu: `/home/odak/mng_common`

---

## Dahil servisler

| Servis | Host portu (özet) | Not |
|--------|-------------------|-----|
| postgres | — | Keycloak DB |
| keycloak | **8080** | `http://192.168.20.20:8080/keycloak` — Admin: `.../admin/master/console/` |
| mongo | 27017 | |
| mongo-express | 8081 | Basic Auth kapalı (compose’ta) |
| redis | 6379 | |
| redis-commander | 8001 | |
| rabbitmq | 5672, 15672 | Management UI :15672 |
| mosquitto | 1883, 9001 | |
| minio | 9090 (API), 9091 (konsol) | |
| portainer | 9000 | |
| seq | 5341 | |
| nodered | 1880 | |

## Hariç tutulanlar (`odak-disabled` profili)

- gitlab, gitlab-postgres, gitlab-redis, gitlab-runner  
- nginx  
- mkdocs  

Ana `docker-compose.yml` değiştirilmez; production sunucular etkilenmez.

---

## Sunucuda adımlar

### 1. Proje dizini

```bash
# Örnek: repoyu sunucuya klonladıktan sonra
cd /path/to/MonitraNG/ApplicationResources/mng_common
```

### 2. Ortam dosyası

```bash
cp env.example .env
nano .env   # tüm CHANGE_ME alanlarını doldurun
```

Zorunlu alanlar: `POSTGRES_PASSWORD`, `KEYCLOAK_ADMIN_PASSWORD`, `MONGO_ROOT_PASSWORD`, `REDIS_PASSWORD`, `RABBITMQ_DEFAULT_PASS`, `MINIO_ROOT_PASSWORD`, `SEQ_ADMIN_PASSWORD`, `NODE_RED_PASSWORD`.

Mosquitto: `.env` içindeki `MQTT_*` ile `mosquitto/config/passwd` uyumlu olmalı ([MOSQUITTO_CREDENTIALS](../../content/infrastructure/MOSQUITTO_CREDENTIALS.md)).

İsteğe bağlı (Keycloak hostname):

```bash
# .env içine
ODAK_KEYCLOAK_HOSTNAME=192.168.20.20
```

### 3. Mongo parolası

`mongo-init/init.js` varsayılan olarak `admin123` kullanır. `.env` içinde farklı `MONGO_ROOT_PASSWORD` kullanacaksanız init script’i güncelleyin veya ilk kurulumda `admin123` ile hizalayın; aksi halde mongo-express bağlanamayabilir.

### 4. Compose başlatma

```bash
docker compose -f docker-compose.yml -f docker-compose.odak.yml pull
docker compose -f docker-compose.yml -f docker-compose.odak.yml up -d
```

Sadece belirli servisler:

```bash
docker compose -f docker-compose.yml -f docker-compose.odak.yml up -d \
  postgres mongo redis rabbitmq minio mosquitto keycloak
```

### 5. Kontrol

```bash
docker compose -f docker-compose.yml -f docker-compose.odak.yml ps
docker compose -f docker-compose.yml -f docker-compose.odak.yml logs -f keycloak
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8080/keycloak/
```

Keycloak admin: `.env` → `KEYCLOAK_ADMIN_USERNAME` / `KEYCLOAK_ADMIN_PASSWORD`

### 6. Durdurma

```bash
docker compose -f docker-compose.yml -f docker-compose.odak.yml down
# Veriyi silmek için: ... down -v  (dikkatli kullanın)
```

---

## Sonraki adım: mng_apps

`mng_common_mng_network` hazır. Devam: [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md).

```bash
cd ~/MonitraNG/ApplicationResources/mng_apps
cp .env.odak.example .env
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d --build
```

---

## Kaynak

| Kaynak | Tahmini |
|--------|---------|
| RAM | ~8–10 GiB (GitLab’sız altyapı) |
| Sunucu | 15 GiB — yeterli (GitLab olmadan) |

---

## İlgili dosyalar

- [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md) — müşteri IT: port, kullanıcı, parola
- [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md) — uygulama kurulumu (sıradaki)
- [KURULUM.md](./KURULUM.md) — sunucu SSH / Docker
- [KEYCLOAK_VE_AG_NOTLARI.md](../../../ApplicationResources/mng_common/KEYCLOAK_VE_AG_NOTLARI.md) — ağ ve Keycloak DB şifresi
