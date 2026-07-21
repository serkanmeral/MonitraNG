# RP02 — Odak test: `mng_odak` Mongo dump (users/groups hariç)

**Kullanım:** **PROMPT** bölümünü müşteri terminal Cursor’a yapıştırın.  
**Ortam:** Yalnızca **test** `192.168.20.20` — production yok.  
**Neden exclude:** Lokal’de Adım 2 ile kullanıcı/grup Create edildi (yeni id + Keycloak Local + `Sm123!?`). Dump’taki `@users`/`@groups` bunları ezer ve Directory/stale Keycloak getirir.

İş akışı: [../REMOTE_CURSOR_WORKFLOW.md](../REMOTE_CURSOR_WORKFLOW.md) · Veri planı: [../DATABASE.md](../DATABASE.md)

---

## PROMPT (aşağıyı kopyala)

```
MonitraNG / Odak TEST sunucusuna erişimin var (192.168.20.20). Görev: mng_odak veritabanının mongodump’ını al; @users ve @groups KOLEKSİYONLARINI HARİÇ TUT. Restore / lokal işlem YAPMA — sadece dump paketi.

## Ortam
- Host: 192.168.20.20 (test)
- Mongo: genelde docker container `mongo` — mng_common yığını
- Database: mng_odak
- Production 192.168.20.8 KULLANMA
- Parolayı chat’e yazma; sunucudaki .env / bilinen lokal infra credential kullan

## Exclude (zorunlu)
mongodump sırasında şu koleksiyonları EXCLUDE et:
  --excludeCollection=@users
  --excludeCollection=@groups

İsteğe bağlı (önerilir, meta çakışmasın diye düşünülebilir — zorunlu değil):
  (yok — domain meta mngkeeper’da; tenant DB’de kalsın)

## Çıktı
Klasör (repo DIŞI veya exports; commit etme):
  C:\Users\monitra\Dev\exports\odak-mongo-mng_odak-YYYYMMDD\
veya /home/odak/exports/odak-mongo-mng_odak-YYYYMMDD/

İçerik:
  - dump/          (mongodump BSON çıktısı: mng_odak/...)
  - manifest.json  (tarih, host, db, exclude list, yaklaşık boyut, komut)

## Örnek komut (sunucuda docker exec ile — ortama göre uyarla)

# 1) Mongo root user/pass: /home/odak/mng_common/.env içinden MONGO_ROOT_* oku (chat’e yazma)
# 2) Dump:

OUT=/home/odak/exports/odak-mongo-mng_odak-$(date +%Y%m%d)
mkdir -p "$OUT"
docker exec mongo mongodump \
  -u admin -p "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --db mng_odak \
  --excludeCollection=@users \
  --excludeCollection=@groups \
  --out /tmp/mng_odak_dump

docker cp mongo:/tmp/mng_odak_dump "$OUT/dump"
# Windows terminal ise eşdeğer: docker exec + docker cp; OUT’u C:\Users\...\exports\... yap

manifest.json yaz:
{
  "exportedAt": "<ISO>",
  "sourceHost": "192.168.20.20",
  "database": "mng_odak",
  "excludedCollections": ["@users", "@groups"],
  "reason": "Local Keeper users/groups already provisioned via JSON import; preserve local Keycloak bindings",
  "outputDir": "<path>",
  "notes": "MinIO binaries NOT included. DI templates = separate Step 4 API pack."
}

## Doğrulama
- dump/mng_odak/ altında .bson dosyaları var
- @users.bson / @groups.bson YOK
- du -sh ile boyut; chat’te path + boyut + koleksiyon sayısı özeti

## Başarı kriteri
- Exclude uygulanmış dump paketi hazır
- manifest.json mevcut
- Chat’te taşıma için net klasör yolu
```

---

## Lokal restore (bu PC — dump geldikten sonra)

1. Paketi `docs/odak/exports/` altına koy (gitignore’da).  
2. `mongorestore --db mng_odak --drop` **dikkat:** `--drop` tüm tenant koleksiyonlarını siler; `@users`/`@groups` dump’ta yoksa restore onları silmez (drop sadece restore edilen koleksiyonlarda).  
   - Güvenli: koleksiyon bazlı restore veya `--nsExclude='mng_odak.@users' --nsExclude='mng_odak.@groups'`.  
3. Person alanları hâlâ **eski** Odak userId tutar → username map ile remap (ayrı adım) veya geçici bozuk expand.

Detay: [../DATABASE.md](../DATABASE.md)
