# Fatih Home — Domain (DomainUI)

## Amaç

Keeper’da tek tenant: backend adı **`odak`**. Görünen / yönetici kimlik alanları **Fatih** ortamına göre doldurulur (karışıklık olmasın).

Oluşturma aracı: **MngDomainUI** (elle; bu planda API script birincil yol değil).

---

## Kararlar

| Konu | Karar |
|------|--------|
| Domain sayısı | Tek |
| Domain Name (backend) | `odak` |
| Mongo DB | `mng_odak` |
| Keycloak realm | `odak` |
| MinIO bucket | `mng-odak` |
| LDAP / AD | Yok |
| Display Name | Fatih’e göre (ör. `Fatih` / `Fatih Home` — DomainUI anında netleştir) |
| Admin Email / Password | Fatih’e göre (sunucu secret; bu dosyaya yazılmaz) |
| Initial Data Template | Boş tercih (iş verisi dump + seed ile) |

Referans form kuralları: `docs/odak/domain/DOMAIN_OLUSTURMA.md`  
Online örnek: `docs/monitrang/deploy/mngonline/`

---

## Önkoşul

- `mng_common` + Keycloak + Mongo + MinIO ayakta
- `mngkeeper` + **mngdomainui** ayakta — [DOCKER.md](./DOCKER.md)
- Erişim: http://10.0.1.100:3001 (DomainUI); UI http://10.0.1.100:3000

| Servis | Not |
|--------|-----|
| MngDomainUI | Keycloak **master** admin ile giriş |
| MngKeeper | Domain pipeline sonrası Active |
| Keycloak | Realm `odak` oluşmalı |

---

## Adımlar

### 1 — Temizlik (ilk kurulumda gerekirse)

DomainUI → **Clear All Domains** (varsa):

- Siler: Keycloak realm’leri (`master` hariç), MinIO `mng-*` bucket’ları
- **Silmez:** Mongo `mng_*` DB’leri ve Keeper `domains` meta → gerekirse manuel

Mongo manuel temizlik örneği (root/docker; parolalar compose `.env`’den):

```bash
docker exec -it mongo mongosh -u admin -p '<MONGO_PASSWORD>' --authenticationDatabase admin --eval '
  db.getSiblingDB("mngkeeper").domains.deleteMany({});
  db.adminCommand({ listDatabases: 1 }).databases
    .map(d => d.name)
    .filter(n => n.startsWith("mng_") && n !== "mngkeeper" && n !== "mng_templates")
    .forEach(n => { print("Dropping " + n); db.getSiblingDB(n).dropDatabase(); });
'
```

`mngkeeper` meta DB’sini drop etmeyin.

### 2 — Create Domain

1. DomainUI — master admin ile giriş
2. **Create Domain**
3. Alanlar:

| Alan | Değer |
|------|--------|
| Domain Name | `odak` |
| Display Name | Fatih’e göre |
| Admin Email | Fatih’e göre |
| Admin Password | güvenli parola (gitignore / operasyon notu) |
| Initial Data Template | boş / yok |

4. Pipeline bitince doğrula:

| Kontrol | Beklenen |
|---------|----------|
| DomainUI listesi | `odak` **Active** |
| Keycloak | realm `odak` |
| Mongo | DB `mng_odak` |
| MinIO | bucket `mng-odak` |

### 3 — Sonraki adım

→ [USERS_AND_DATA.md](./USERS_AND_DATA.md) (online user/group modeli)

---

## Notlar

- Backend her yerde `odak` kalır; UI’da Fatih display görünür.
- URL şimdilik IP tabanlı; CORS / public URL compose `.env` içinde `10.0.1.65` ile hizalanmalı — [DOCKER.md](./DOCKER.md).
