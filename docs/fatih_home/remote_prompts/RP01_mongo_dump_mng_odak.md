# RP01 — Odak: slim `mng_odak` Mongo dump (Fatih Home / online model)

**Kullanım:** Aşağıdaki **PROMPT** bloğunun tamamını Odak tarafındaki Cursor sohbetine yapıştır.  
**Ortam:** Yalnızca **test** `192.168.20.20` — production `192.168.20.8` yok.  
**Amaç:** Fatih Home için slim dump paketi; restore burada yapılmaz.

Exclude = online (`docs/monitrang/deploy/mngonline/USERS_AND_DATA.md`).  
Plan: [../DATABASE.md](../DATABASE.md)

---

## PROMPT (kopyala → Odak Cursor)

```
Görev: Odak TEST (192.168.20.20) üzerinde mng_odak için SLIM mongodump al.
Restore, Fatih Home, production (192.168.20.8) veya MinIO işlemi YAPMA.
Sadece dump archive + manifest.json üret; iş bitince chat’te path, boyut ve doğrulama özeti ver.
Parolaları chat’e yazma.

## Ortam
- Host: 192.168.20.20 (test)
- Mongo container adı: mongo
- Database: mng_odak
- Mongo kullanıcı/şifre: ApplicationResources/mng_common/.env içinden MONGO_ROOT_USERNAME / MONGO_ROOT_PASSWORD (yoksa mng_apps .env MONGO_* — chat’e yazma)
- Repo yolu sunucuda genelde /home/odak/MonitraNG — yoksa bul

## Exclude (zorunlu — hepsini uygula)
--excludeCollection=@users
--excludeCollection=@groups
--excludeCollection=sec_events
--excludeCollection=@workflow_instances
--excludeCollection=@workflow_node_executions
--excludeCollection=mon_metrics
--excludeCollection=@job_executions

## Adımlar
1) Mongo container’ın çalıştığını doğrula: docker ps | grep mongo
2) STAMP=$(date +%Y%m%d_%H%M%S)
   OUT=/home/odak/exports/odak-mongo-mng_odak-$STAMP
   mkdir -p "$OUT"
3) .env’den şifreyi oku (export et; echo ile chat’e basma)
4) Dump:

docker exec mongo mongodump \
  -u "$MONGO_ROOT_USERNAME" -p "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --db mng_odak \
  --excludeCollection=@users \
  --excludeCollection=@groups \
  --excludeCollection=sec_events \
  --excludeCollection=@workflow_instances \
  --excludeCollection=@workflow_node_executions \
  --excludeCollection=mon_metrics \
  --excludeCollection=@job_executions \
  --gzip --archive=/tmp/mng_odak_slim.archive.gz

5) docker cp mongo:/tmp/mng_odak_slim.archive.gz "$OUT/mng_odak_slim.archive.gz"
6) Container içi geçici dosyayı sil: docker exec mongo rm -f /tmp/mng_odak_slim.archive.gz
7) "$OUT/manifest.json" yaz:

{
  "exportedAt": "<ISO-8601 UTC>",
  "sourceHost": "192.168.20.20",
  "database": "mng_odak",
  "format": "mongodump --gzip --archive",
  "excludedCollections": [
    "@users", "@groups", "sec_events",
    "@workflow_instances", "@workflow_node_executions",
    "mon_metrics", "@job_executions"
  ],
  "reason": "Fatih Home / online model: preserve domain users-groups; skip heavy/runtime collections",
  "archiveBytes": <number>,
  "outputDir": "<OUT path>",
  "notes": "MinIO not included. DI templates = separate seed. For Serkan / Fatih Home restore later."
}

8) Doğrula:
- ls -lh "$OUT/mng_odak_slim.archive.gz"  (boyut > 0)
- manifest.json mevcut
- mongodump log’unda exclude’ların geçtiğini kontrol et
- İsteğe bağlı: archive içinde @users.bson / @groups.bson olmadığını teyit et (gzip archive ise gunzip -c | mongorestore --dryRun veya tar/list yoksa log yeterli)

## Başarı kriteri
- $OUT altında mng_odak_slim.archive.gz + manifest.json
- Chat özeti: tam klasör yolu, archive MB, excludedCollections listesi, kısa not
- Başka sunucuya restore etme; paketi bırak (Serkan taşıyacak: docs/fatih_home/artifacts/)
```

---

## Bu PC’ye alma (dump bittikten sonra)

1. `$OUT` klasörünü buraya kopyala: `docs/fatih_home/artifacts/odak-mongo-mng_odak-<stamp>/`  
2. Fatih Mongo ayaktayken restore: [../DATABASE.md](../DATABASE.md)
