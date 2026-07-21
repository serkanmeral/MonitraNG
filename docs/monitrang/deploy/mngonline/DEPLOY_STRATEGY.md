# monitrang.com — Deploy stratejisi

**Ortam:** Production / online (`monitrang.com`)  
**Son güncelleme:** 20 Temmuz 2026  
**Durum:** Kabul edilen hedef model (eski GitLab Runner deploy hattı birincil değil)

---

## Karar özeti

| | Eski (yorucu) | Yeni (hedef) |
|--|---------------|--------------|
| Ad | GitLab CI `deploy-services` | **PC-driven sync + compose** (Odak modeli) |
| Tetikleyici | Pipeline Play (manuel) | Lokal PowerShell script |
| Kod aktarımı | Sunucuda `git reset --hard origin/main` | **tar/scp sync** (commit şart değil) |
| Build | Runner + sunucu compose | Yalnızca sunucuda `docker compose build` |
| Kapsam | Genelde tüm stack rolling | **Seçici servis** (UI-only vb.) |
| Kimlik | CI `DEPLOY_*` variables | Mevcut SSH key (`monitrang-server`) |

**Neden:** Tek operatör / tek production sunucusu için CI deploy hattı fazla sürtünme üretiyor (pipeline bekleme, runner, reset riski, uzun tam stack). Odak’ta doğrulanan sync→build→up akışı günlük iş için daha hızlı ve öngörülebilir.

**CI/CD rolü (korunur, daraltılır):** GitLab pipeline **build/test/docs** için kalabilir. Production’a kod çıkarma birincil yolu **değildir**. `deploy-services` job’ı **legacy** kabul edilir; acil yedek veya özel durumlar dışında kullanılmaz.

---

## Hedef akış

```
Lokal workspace
    │
    ├─① sync-mngonline-source.ps1   (seçili path’ler → tar → scp → sunucuda extract)
    │
    └─② deploy-mngonline-apps.ps1   (SSH → compose build + up -d, seçili servisler)
            │
            └─ monitrang-server:/root/MonitraNG
                 ApplicationResources/mng_apps/docker-compose.production.yml
```

İsteğe bağlı üçüncü yol (audit / “zaten push ettim”):

```
Lokal: git push → deploy-mngonline-apps.ps1 -FromGit
  → sunucuda git fetch + reset --hard origin/main → compose build/up
```

`-FromGit` sync’i atlar; sunucu remote’unun (`origin`) güncel olduğundan emin olun (bkz. ACCESS.md git remote tablosu).

---

## İlkeler

1. **Seçici deploy varsayılan.** Tüm stack’i her seferinde yenileme.
2. **`.env` sunucuda kalır.** Sync, `ApplicationResources/mng_apps/.env` dosyasını ezmez (tar’a alınmaz / extract öncesi korunur).
3. **Altyapı (`mng_common`) ayrı.** Apps deploy’u Mongo/Redis/Nginx host stack’ini yeniden kurmaz. Host nginx (80/443) apps’tan bağımsızdır.
4. **Pre-deploy backup isteğe bağlı.** Tam stack veya kritik backend öncesi `-Backup` kullanın.
5. **Health fail deploy’u soft-uyarır** (eski CI gibi); kritik serviste elle doğrulayın.
6. **Commit önerilir, zorunlu değil.** Production’a çıkan kodun local’de commit’li olması operasyon disiplini; sync modeli bunu teknik olarak dayatmaz.

---

## Servis sırası (çoklu / tam deploy)

Bağımlılık için önerilen sıra (Odak/CI ile uyumlu):

1. mngkeeper  
2. mngdatagateway  
3. mnghub  
4. mngllm  
5. mngscheduler  
6. mngadmin  
7. mngnotifier  
8. mnggateway  
9. mngui  
10. mngdomainui  

Diğer compose servisleri (`mngdocument`, `mngoperations`, …) ihtiyaç halinde açıkça `-Services` ile verilir.

---

## Eski hat ile ilişki

| Bileşen | Yeni durum |
|---------|------------|
| `.gitlab-ci.yml` → `deploy-services` | Legacy; birincil yol değil |
| `docs/content/cicd/*` | Tarihsel / CI referansı; günlük online deploy için buraya bakılmamalı |
| Bu klasör (`docs/monitrang/deploy/mngonline/`) | **Online deploy için tek giriş noktası** |
| Odak `scripts/odak/*` | Müşteri ortamı; monitrang.com için kopyalanmaz, aynı *desen* kullanılır |

---

## Bilinçli trade-off’lar

| Artı | Eksi |
|------|------|
| Hızlı, seçici, CI beklemez | Audit trail CI Play kadar merkezi değil |
| Commit’siz hotfix mümkün | Disiplin yoksa “ne deploy edildi?” bulanıklaşır |
| SSH key zaten hazır | Script’ler geliştirme PC’sine bağımlı |
| Odak ile zihinsel model ortak | İki ortam (Odak / online) script seti ayrı tutulmalı |

---

## GitLab Runner

PC-driven deploy sonrası Runner zorunlu değil. Idle maliyeti düşük (~54 MiB); asıl ağırlık job anında ve GitLab uygulamasında (~5 GiB). Ölçüm ve stop/kaldır seçenekleri: [RUNNER_RESOURCES.md](./RUNNER_RESOURCES.md).

## Sonraki adımlar (uygulama)

- [x] Strateji dokümanı (bu dosya)
- [x] Günlük komutlar: [DEPLOY.md](./DEPLOY.md)
- [x] Script’ler: `scripts/mngonline/`
- [x] Runner kaynak envanteri: [RUNNER_RESOURCES.md](./RUNNER_RESOURCES.md)
- [ ] Runner stop / kaldırma kararı
- [ ] İlk gerçek deploy ile doğrulama (UI veya tek backend)
- [ ] İstenirse `.gitlab-ci.yml` içinde `deploy-services`’e legacy notu
