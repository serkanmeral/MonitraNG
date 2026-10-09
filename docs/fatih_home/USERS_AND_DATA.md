# Fatih Home — Kullanıcı / grup / veri (online model)

**Durum:** Plan  
**Model:** `docs/monitrang/deploy/mngonline/USERS_AND_DATA.md` ile aynı mantık  
**Domain:** `odak` (görünen kimlik: Fatih)

---

## Kararlar

| Konu | Karar |
|------|--------|
| Yaklaşım | **Online gibi** — CreateUser ile id kırmadan / veya yerinde Update; dump’ta kimlik hariç |
| LDAP / AD | Kapalı; her şey **Local** |
| Korunan kullanıcılar | Domain admin (DomainUI’dan) + operasyon hesabı(ları) — net liste kurulumda doldurulur |
| Diğer kullanıcılar | Kaynak Odak’tan gelecekse: anonimizasyon / Fatih’e uygun isim-email politikası (online script’e benzer) |
| Anlamlı iş / rol grupları | Aktif (`admins`, `managers`, `users`, … + iş grupları) |
| Anlamsız / AD artığı gruplar | Taşınırsa `isActive=false` |
| Dump sonrası | `@users` / `@groups` **restore edilmez**; gerekirse Keycloak rebind |

Referans anlamlı grup listesi (online):

`admins`, `managers`, `users`, `guests`, `developers`, `testers`, `viewers`,  
`IK Users`, `Kalite Users`, `Kalite Yonetici Group`, `Planlama Users`, `Satin Alma Users`,  
`Depo Users`, `Erp Users`, `BT Users`, `DBA Users`, `Idare Users`, `Talasli Users`,  
`Tasarım Users`, `Yonetim Users`, `MonitraNG Admins`, `MonitraNG Users`, `RDP_Yetkili`

---

## Neden online model?

| Risk | Online önlem |
|------|----------------|
| Dump users/groups ezmesi | Restore’da **hariç tut** |
| Directory bayrakları | Local normalize / LDAP yok |
| persons / personGroups | Mümkünse `__dataId` koru; Create* ile yeni id → remap gerekir |
| Gerçek PII (Odak) | Anonimizasyon (online `anonymize-odak-local-for-online.ps1` benzeri) — Fatih home’a uyarlanır |

---

## Önerilen sıra

```text
1. DomainUI → odak (Fatih display/admin)     → DOMAIN.md
2. Domain admin + korunan hesaplar smoke
3. (İsteğe bağlı) Kaynakta anonimizasyon / grup pasif
4. Odak VPN → mongodump (users/groups hariç) → DATABASE.md
5. Fatih Home restore + domainId düzeltmesi + Keycloak rebind
6. Seed                                          → SEED.md
7. Login smoke (korunan hesaplar)
```

### Script referansları (monitrang online — uyarlanacak)

| Script | Amaç |
|--------|------|
| `scripts/mngonline/anonymize-odak-local-for-online.ps1` | User anonimizasyon + grup pasifleştirme (kaynak/lokal) |
| (Fatih Home) | Gerekirse `scripts/fatih_home/` altına kopya / parametreli uyarlama |

---

## Bileşenler

| Bileşen | Rol |
|---------|-----|
| Keycloak | Realm `odak`, local user; LDAP yok |
| MngKeeper | Domain `odak`, `@users` / `@groups` |
| Gateway | JWT |

---

## Secret’lar

Kullanıcı parolaları ve client secret’lar **bu dosyaya yazılmaz**. Sunucu `.env` / operasyon notu (gitignore).

SSH erişimi: [access.md](./access.md).
