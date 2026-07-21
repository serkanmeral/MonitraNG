# monitrang.com — Kullanıcı / grup / veri (odak)

**Durum:** Uygulama  
**Son güncelleme:** 20 Temmuz 2026  
**Kaynak:** Lokal Docker Desktop (`odak` domain, Keeper + `mng_odak`)  
**Hedef:** Online `monitrang-server` / domain `odak`

---

## Kararlar

| Konu | Karar |
|------|--------|
| Domain | `odak` (online’da oluşturuldu) |
| Korunan kullanıcılar | `serkan.meral`, `odak_admin` |
| `serkan.meral` email | `sermeral@gmail.com` |
| Diğer kullanıcılar | Gerçekçi Türkçe ad-soyad + username; email `*.@example.local`; **`__dataId` korunur** |
| Anlamsız / AD grupları | Taşınır, **`isActive=false`** |
| Anlamlı iş / rol grupları | Taşınır, **aktif** |
| Yöntem | Yerinde Update (CreateUser yok) → sonra Mongo dump/restore |

---

## Anlamlı gruplar (aktif kalır)

`admins`, `managers`, `users`, `guests`, `developers`, `testers`, `viewers`,  
`IK Users`, `Kalite Users`, `Kalite Yonetici Group`, `Planlama Users`, `Satin Alma Users`,  
`Depo Users`, `Erp Users`, `BT Users`, `DBA Users`, `Idare Users`, `Talasli Users`,  
`Tasarım Users`, `Yonetim Users`, `MonitraNG Admins`, `MonitraNG Users`, `RDP_Yetkili`

Diğer tüm gruplar → `isActive=false`.

---

## Script’ler

| Script | Amaç |
|--------|------|
| `scripts/mngonline/anonymize-odak-local-for-online.ps1` | Lokal Keeper: user anonimizasyon + grup pasifleştirme |
| (sonraki) dump/restore + Keycloak rebind | ID koruyarak online’a aktarım |

```powershell
# Önizleme
.\scripts\mngonline\anonymize-odak-local-for-online.ps1 -WhatIf

# Uygula (lokal)
.\scripts\mngonline\anonymize-odak-local-for-online.ps1
```

Rapor: `docs/monitrang/deploy/mngonline/artifacts/` (gitignore önerilir) veya `%TEMP%`.

---

## Sıra

1. [x] Kararlar
2. [x] Lokal anonimizasyon + grup pasif (`anonymize-odak-local-for-online.ps1`)
3. [x] Slim `mng_odak` mongodump → online restore (**yenilendi 20 Temmuz 2026 16:46** — `@users`/`@groups` hariç)
4. [x] Online `domainId` düzeltmesi + Keycloak `odak` realm rebind
5. [x] Smoke: `serkan.meral` / `odak_admin` token OK; 179 user

### Login (online)

Korunan: `odak_admin`, `serkan.meral` (email `sermeral@gmail.com`).  
Anonim kullanıcılar ortak varsayılan şifre ile Keycloak’a bağlandı. Parolalar chat/operasyon notunda; bu dosyaya yazılmaz.

### Notlar

- Anlamlı gruplarda Mongo’da `isActive` yoktu → `true` set edildi (API filtresi için).
- Dump hariç tutulanlar: `sec_events`, `@workflow_instances`, `@workflow_node_executions`, `mon_metrics`, `@job_executions`, **`@users`**, **`@groups`**.
- Rapor/map: `docs/monitrang/deploy/mngonline/artifacts/` (gitignore `artifacts/`).

Secret’lar bu dosyaya yazılmaz.
