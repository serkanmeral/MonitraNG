# Fatih Home — Veritabanı (Odak dump → restore)

**Durum:** Dump operasyonu hazırlanıyor (online model)  
**Son güncelleme:** 2026-08-06  

## Amaç

Online (`monitrang.com`) aktarımındaki gibi **slim** `mng_odak` dump alıp Fatih Home’a restore etmek. Kimlik katmanı (`@users` / `@groups`) dump’ta **yok** — DomainUI + online user/group adımları korunur.

| | Online (referans) | Fatih Home |
|--|-------------------|------------|
| Kaynak | Lokal Docker (`odak`, anonimizasyon sonrası) | **Odak VPN** → varsayılan **test** `192.168.20.20` |
| Hedef | `monitrang-server` | `10.0.1.65` |
| Exclude | `@users`, `@groups` + ağır koleksiyonlar | **Aynı liste** |
| Restore sonrası | domainId + Keycloak rebind | Aynı |

Online notlar: `docs/monitrang/deploy/mngonline/USERS_AND_DATA.md`  
Lokal RP02 (benzer exclude): `docs/monitrang/deploy/local/remote_prompts/RP02_mongo_dump_mng_odak_test.md`

---

## Kararlar

| Konu | Karar |
|------|--------|
| Yöntem | mongodump (archive gzip) → scp → mongorestore |
| DB | `mng_odak` |
| Kaynak host (varsayılan) | **Test** `192.168.20.20` — production `192.168.20.8` yalnızca bilinçli `-Server` |
| `@users` / `@groups` | **Hariç** |
| Ek hariç (online ile aynı) | `sec_events`, `@workflow_instances`, `@workflow_node_executions`, `mon_metrics`, `@job_executions` |
| MinIO | Dump’ta yok → [SEED.md](./SEED.md) |
| Git | Dump **commit edilmez** → `docs/fatih_home/artifacts/` (`artifacts/` gitignore) |

---

## Exclude listesi (zorunlu)

```text
@users
@groups
sec_events
@workflow_instances
@workflow_node_executions
mon_metrics
@job_executions
```

---

## Sıra (online ile hizalı)

```text
1. (Fatih) Domain + user/group hazır          → DOMAIN.md / USERS_AND_DATA.md
2. Odak VPN + dump (slim)                     → bu dosya + script / remote prompt
3. Paketi Fatih’e taşı (veya lokal artifacts)
4. Fatih Mongo restore (users/groups ezilmez)
5. domainId düzeltmesi + Keycloak rebind
6. Smoke + SEED.md
```

> Not: Fatih CPU engeli kalkmadan restore yapılamaz; **dump paketi şimdiden alınabilir** ve `artifacts/` altında bekletilir.

---

## Operasyon A — Script (VPN + Odak SSH)

Repo kökünden:

```powershell
# Önizleme
.\scripts\fatih_home\dump-mng-odak-slim.ps1 -WhatIf

# Test (192.168.20.20) — varsayılan
.\scripts\fatih_home\dump-mng-odak-slim.ps1

# Production (yalnızca bilinçli)
.\scripts\fatih_home\dump-mng-odak-slim.ps1 -Server 192.168.20.8
```

Önkoşul: VPN, Posh-SSH, `.env.odak.local` / `ODAK_SSH_PASSWORD` (bkz. `scripts/odak/OdakSshCommon.ps1`).

Çıktı örneği:

```text
docs/fatih_home/artifacts/odak-mongo-mng_odak-YYYYMMDD_HHMMSS/
  mng_odak_slim.archive.gz
  manifest.json
```

---

## Operasyon B — Remote Cursor prompt

VPN yok / terminal Cursor ile Odak’ta çalışılacaksa:

→ [remote_prompts/RP01_mongo_dump_mng_odak.md](./remote_prompts/RP01_mongo_dump_mng_odak.md)

---

## Kaynak komut (referans — sunucuda)

```bash
OUT=/tmp/mng_odak_slim_$(date +%Y%m%d_%H%M%S)
# MONGO_ROOT_* : mng_common .env (chat’e yazma)

docker exec mongo mongodump \
  -u admin -p "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --db mng_odak \
  --excludeCollection=@users \
  --excludeCollection=@groups \
  --excludeCollection=sec_events \
  --excludeCollection=@workflow_instances \
  --excludeCollection=@workflow_node_executions \
  --excludeCollection=mon_metrics \
  --excludeCollection=@job_executions \
  --gzip --archive=/tmp/mng_odak_slim.archive.gz

docker cp mongo:/tmp/mng_odak_slim.archive.gz "$OUT/"
# + manifest.json
```

---

## Restore (Fatih Home — CPU + Mongo ayaktayken)

```bash
# Archive'ı sunucuya kopyala, sonra:
docker exec -i mongo mongorestore \
  -u admin -p "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin \
  --nsExclude='mng_odak.@users' \
  --nsExclude='mng_odak.@groups' \
  --gzip --archive < mng_odak_slim.archive.gz
```

`--drop` kullanırken dikkat: yalnızca restore edilen koleksiyonlar düşer; dump’ta olmayan `@users`/`@groups` silinmez. Yine de `--nsExclude` ile güvencele.

### Restore sonrası

1. `domainId` / meta uyumu (gerekirse)
2. Keycloak `odak` realm rebind (online adım 4)
3. Smoke: korunan hesaplar
4. → [SEED.md](./SEED.md)

---

## persons / personGroups

| Alan | Saklanan ID |
|------|-------------|
| `persons` | Keeper `@users.__dataId` |
| `personGroups` | Keeper `@groups.__dataId` |

Dump iş verisi Odak id taşır. Online modelde id koruma veya remap gerekir — kimlik kurulumu netleşince uygulanır.

---

## Doğrulama (dump sonrası)

- [ ] `manifest.json` var (host, db, exclude, boyut, tarih)
- [ ] Archive boyutu makul (> birkaç MB tipik)
- [ ] Exclude listesi manifest’te online ile aynı
- [ ] Paket `artifacts/` altında; git status temiz (untracked ignore)
