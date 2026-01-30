# Keycloak ve Ağ Notları (mng_common)

## Published Ports boş görünmesi (Portainer'da "-")

**Normaldir.** Keycloak için port mapping compose'ta kapalı; erişim sadece Nginx üzerinden (admin.monitrang.com/keycloak). Host'a 8080 açılmıyor.

```yaml
# Port mapping removed - Access via Nginx reverse proxy only
# ports:
#   - "8080:8080"
```

## Keycloak'a erişim

- **Tarayıcı:** https://admin.monitrang.com/keycloak (Nginx → keycloak:8080)
- **MngKeeper / MngGateway vb.:** http://keycloak:8080 (aynı Docker ağı üzerinden)

Keycloak hem `mng_network` hem `mng_common_mng_network` üzerinde; böylece mng_apps servisleri (MngKeeper vb.) Keycloak'a erişebilir.

## Ağ: mng_common_mng_network

- **mng_common** bu ağı artık **kendisi oluşturuyor** (external değil). Böylece mng_common önce başlatıldığında "network mng_common_mng_network not found" hatası oluşmaz.
- **mng_apps** (docker-compose.production.yml) bu ağı **external** kullanır; ağın önceden var olması gerekir.

**Önerilen başlatma sırası:**

1. **mng_common** önce: `cd ApplicationResources/mng_common && docker compose up -d`  
   → mng_network ve mng_common_mng_network oluşturulur, Keycloak ve Nginx ayağa kalkar.
2. **mng_apps** sonra: `cd ApplicationResources/mng_apps && docker compose -f docker-compose.production.yml --env-file .env up -d`  
   → mng_common_mng_network zaten var olduğu için servisler bu ağa bağlanır.

Eğer sadece mng_apps çalıştırıyorsanız, ağı bir kez elle oluşturun:

```bash
docker network create mng_common_mng_network
```

## .env (mng_common)

Keycloak ve PostgreSQL için aşağıdakiler **mutlaka** tanımlı olmalı (yoksa Keycloak başlamaz):

- `KEYCLOAK_ADMIN_USERNAME`
- `KEYCLOAK_ADMIN_PASSWORD`
- `POSTGRES_PASSWORD`

Örnek: `env.example` dosyasını `.env` olarak kopyalayıp gerçek değerlerle doldurun; `CHANGE_ME` kalan yerleri değiştirin.

---

## Keycloak: "password authentication failed for user keycloak"

Bu hata, Keycloak'ın PostgreSQL'e bağlanırken kullandığı şifrenin, PostgreSQL'in beklediği şifreyle eşleşmediği anlamına gelir.

**Neden olur?**

- `docker-compose.yml` hem **postgres** hem **keycloak** servisi için aynı değişkenleri kullanır: `POSTGRES_USER`, `POSTGRES_PASSWORD`.
- PostgreSQL kullanıcı ve şifreyi **sadece volume ilk kez oluşturulduğunda** uygular. Sonradan `.env` içinde `POSTGRES_PASSWORD` değiştirilirse, Postgres container’ı yeniden başlasa bile **mevcut veri** eski şifreyle kalır; Keycloak ise yeni şifreyi kullanır → uyuşmazlık.

**Çözüm seçenekleri**

### 1) Mevcut .env şifresini PostgreSQL’e zorlamak (tercih edilen)

Sunucuda `.env` içindeki `POSTGRES_PASSWORD` değerini biliyorsanız ve bunu “doğru” şifre kabul ediyorsanız, Postgres’te `keycloak` kullanıcısının şifresini bu değere güncelleyin:

```bash
# Sunucuda (mng_common dizininde)
cd /path/to/ApplicationResources/mng_common   # gerçek yolu yazın

# Postgres çalışıyorsa, keycloak kullanıcısının şifresini .env'deki ile güncelle
# Aşağıdaki YOUR_POSTGRES_PASSWORD yerine .env'deki POSTGRES_PASSWORD değerini yazın
docker exec -it postgres psql -U keycloak -d keycloak -c "ALTER USER keycloak WITH PASSWORD 'YOUR_POSTGRES_PASSWORD';"
```

Şifrede özel karakter varsa tek tırnak içinde yazın; `'` karakteri varsa `''` ile escape edin. Sonra Keycloak’ı yeniden başlatın:

```bash
docker compose restart keycloak
```

### 2) .env’i mevcut (eski) Postgres şifresine göre düzeltmek

İlk kurulumda kullandığınız şifreyi biliyorsanız, sunucudaki `.env` dosyasında `POSTGRES_PASSWORD` değerini bu eski şifreyle değiştirin. Sonra:

```bash
docker compose restart keycloak
```

### 3) Keycloak veritabanını sıfırdan kurmak (veri kaybı olur)

Keycloak’taki realm/kullanıcı verileri silinir. Sadece temiz bir başlangıç gerekiyorsa:

```bash
cd /path/to/ApplicationResources/mng_common
docker compose stop keycloak postgres
docker volume rm mng_common_postgres_data    # volume adı proje prefix’e göre farklı olabilir; docker volume ls ile kontrol edin
docker compose up -d postgres
# Postgres ayağa kalktıktan birkaç saniye sonra
docker compose up -d keycloak
```

**Kontrol listesi (sunucuda)**

1. `ApplicationResources/mng_common/.env` var mı?
2. `.env` içinde `POSTGRES_USER=keycloak` ve `POSTGRES_PASSWORD=...` (boş olmayan) tanımlı mı?
3. Keycloak ile aynı compose’ta tanımlı `postgres` servisi bu `.env` ile mi başlatıldı? (Compose’u `--env-file .env` ile çalıştırıyorsanız aynı dizindeki `.env` kullanılır.)
4. Yukarıdaki 1 veya 2. çözümden birini uyguladıktan sonra `docker compose logs keycloak` ile tekrar “password authentication failed” gelmemeli.
