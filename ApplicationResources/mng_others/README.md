# mng_others — Opsiyonel Servisler

MonitraNG için opsiyonel / domain-spesifik servisler. **mng_common** ayağa kalkmış olmalı.

## Servisler

| Servis | Port | Açıklama |
|--------|------|----------|
| **MngSim** | 6061 | Monitoring simülatörü (HTTP/SNMP cihazlar + tren simülasyonu, sensörler, event MQTT) |
| **PostGIS** | 5433 (host) | GIS veritabanı (Railway Platform) |
| **GeoServer** | 8082 | Harita tile servisi (WMTS/WMS) |
| **pgAdmin** | 5051 | PostgreSQL/pgAdmin — PostGIS + Keycloak DB tek yerden (container: pgadmin_gis) |

## Başlatma

```bash
# Önce mng_common
cd ../mng_common && docker compose up -d

# Sonra mng_others
cd ../mng_others && docker compose up -d
```

**Sadece GIS:**
```bash
docker compose up -d postgis geoserver pgadmin
```

**Sadece MngSim:**
```bash
docker compose up -d mngsim
```

MngSim, **mng_common** ağına bağlıdır; tren haritası tile’ları için GeoServer’a (`geoserver:8080`), tren event’leri için Mosquitto’ya (`mosquitto:1883`) erişir. Önce `mng_common` (mosquitto dahil) çalışır olmalı. **URL:** http://localhost:6061

## pgAdmin — Tek kurulum (PostGIS + Keycloak)

Bu compose’daki pgAdmin, hem PostGIS hem Keycloak PostgreSQL’ine aynı ağ üzerinden erişir. **Başka bir compose’da (örn. mng_apps) pgAdmin çalışıyorsa onu kapatıp silebilirsiniz;** tüm veritabanı yönetimini buradan yapın.

- **URL:** http://localhost:5051  
- **Giriş:** E-posta `admin@example.com`, Şifre `admin`

### pgAdmin’de eklenecek sunucular

Aynı Docker ağında oldukları için **Host** olarak container adını kullanın (`localhost` değil).

| Sunucu adı (isteğe bağlı) | Host | Port | Maintenance DB | Kullanıcı | Şifre |
|---------------------------|------|------|----------------|-----------|--------|
| **PostGIS (Railway)** | `postgis` | 5432 | `gis` | `gisuser` | `gispass` |
| **Keycloak DB** | `postgres` | 5432 | `keycloak` | `keycloak` | mng_common `.env` içindeki `POSTGRES_PASSWORD` (varsayılan örnek: `CHANGE_ME`) |

Keycloak şifresini bilmiyorsanız: `ApplicationResources/mng_common/.env` dosyasındaki `POSTGRES_PASSWORD` değerine bakın.

---

## GIS (Railway Platform)

- PostGIS: `localhost:5433` (db: gis, user: gisuser, pass: gispass) — host port 5433; yerel PostgreSQL 5432 kullanıyorsa çakışma olmaz
- GeoServer: http://localhost:8082/geoserver

**Şema (railways, stations, places, alarms, vb.):**  
`init-railway-schema.sql` bir kez çalıştırılır:  
`Get-Content init-railway-schema.sql | docker exec -i postgis psql -U gisuser -d gis`

Detaylı kurulum: [railway-platform.md](../../docs/content/offline_map/railway-platform.md)
