# Odak — mng_apps dağıtım stratejisi (git push’suz)

**Hedef:** Geliştirme makinenizdeki güncel kodu `192.168.20.20` sunucusuna sık deploy etmek.  
**Git commit/push:** Deploy ile **bağlı değil**; istediğiniz zaman ayrı yapılır.  
**Sunucu bugün:** `/home/odak/mng_common` var; **tam MonitraNG reposu yok** (ilk uygulama deploy’unda kaynak aktarımı gerekir).

---

## Kısa cevap: Her seferinde `git clone` gerekir mi?

| Yöntem | `git clone` / `git pull` sunucuda? |
|--------|-------------------------------------|
| Bu planda önerilen (**kaynak senkron + sunucuda build**) | **Hayır** (ilk sefer kaynak kopyası, sonra güncelleme) |
| Sunucuda sürekli `git pull` | Evet — push zamanınıza bağlı; **önerilmez** |
| Image export (local build → `docker save/load`) | **Hayır** (sunucuda kaynak gerekmez) |

Kodlar zaten sizin makinede olduğu için deploy, **lokal repo → sunucu** akışıyla yapılır.

---

## Mimari özet

```
[Geliştirme PC — MonitraNG workspace]
        │
        │  ① sync (tar/scp/rsync) — commit şart değil
        ▼
[Sunucu 192.168.20.20 — /home/odak/MonitraNG]
        │
        │  ② docker compose build / up (mng_apps)
        ▼
[mng_common_mng_network] ← zaten /home/odak/mng_common
```

- **mng_common:** Ayrı dizin (`/home/odak/mng_common`). Altyapı değişince yalnızca bu klasör senkron edilir.
- **mng_apps:** Tam repo yapısı sunucuda `~/MonitraNG` altında (build context `../../MngGateway` vb. için).

---

## Sunucu dizin yapısı (hedef)

```
/home/odak/
├── mng_common/              # Altyapı (mevcut)
│   ├── docker-compose.yml
│   ├── docker-compose.odak.yml
│   └── .env
└── MonitraNG/               # Uygulama build kaynağı (ilk sync ile oluşur)
    ├── MngGateway/
    ├── MngKeeper/
    ├── …
    └── ApplicationResources/mng_apps/
        ├── docker-compose.production.yml
        ├── docker-compose.odak.yml
        └── .env              # Bir kez oluşturulur; deploy scriptleri üzerine yazmaz
```

---

## Üç deploy modu

### Mod A — Kaynak senkron + sunucuda build (önerilen, genel)

**Ne zaman:** Çoğu deploy; sunucuda Docker build cache birikir.  
**Akış:**

1. PC’den `sync-odak-source.ps1` (tüm veya değişen servis klasörleri + `mng_apps`).
2. SSH ile `deploy-odak-apps.ps1` → `docker compose ... build` + `up -d`.

**Artı:** Push gerekmez; sunucuda .NET/Docker ortamı tek yerde.  
**Eksi:** İlk ve tam build uzun sürer (8 CPU / 15 GiB RAM’de planlayın).

### Mod B — Tek servis / hızlı döngü

Aynı Mod A; script parametreleri:

```powershell
.\deploy-odak-apps.ps1 -Services mngkeeper,mngui -SkipSync  # kaynak zaten güncel
.\sync-odak-source.ps1 -Paths MngKeeper,ApplicationResources/mng_apps  # sadece değişenler
```

### Mod C — Image taşıma (sunucuda build yok)

**Ne zaman:** Sunucuda build istemiyorsanız veya PC’de zaten build ettiniz.

1. PC: `docker compose -f docker-compose.production.yml -f docker-compose.odak.yml build <servis>`
2. PC: `docker save` → `scp` → sunucu: `docker load`
3. Sunucu: `docker compose ... up -d --no-build <servis>`

Image adları: `mnggateway:latest`, `mngkeeper:latest`, … (`VERSION=latest`).

---

## İlk kurulum checklist (bir kez)

- [ ] mng_common ayakta (`/home/odak/mng_common`)
- [ ] `sync-odak-source.ps1 -Full` → `~/MonitraNG` oluştu
- [ ] `cp .env.odak.example .env` sunucuda; `KEYCLOAK_CLIENT_SECRET`, `MNGKEEPER_LICENSE_MASTER_KEY` dolduruldu
- [ ] Keycloak: `monitra` realm + `mng-keeper-admin` client
- [ ] `deploy-odak-apps.ps1 -FullBuild` (tüm servisler, uzun sürebilir)
- [ ] Health: Gateway 5040, UI 3000

---

## Günlük geliştirme döngüsü (önerilen)

1. Lokalde kod değişikliği (commit isteğe bağlı).
2. İlgili klasörleri senkron et (`-Paths` veya tam sync).
3. Deploy script (tüm veya `-Services`).
4. Gerekirse log: `docker compose ... logs -f mngkeeper`

**mng_common** değiştiyse (compose, nginx conf, env):

```powershell
.\sync-odak-source.ps1 -IncludeMngCommon
# Sunucuda:
cd ~/mng_common && docker compose -f docker-compose.yml -f docker-compose.odak.yml up -d
```

---

## Git ile ilişki

| İşlem | Zorunlu mu? |
|--------|-------------|
| Deploy öncesi `git commit` | Hayır |
| Deploy öncesi `git push` | Hayır |
| Sunucuda `git pull` | Hayır (bu stratejide kullanılmıyor) |
| Yedek / ekip paylaşımı için push | Sizin takviminizde |

İsteğe bağlı: sunucuda `~/MonitraNG` içinde `git init` + remote (sadece sizin referansınız); deploy yine **sync script** ile yapılır.

---

## .env ve gizli dosyalar

- Sunucudaki `.env` **deploy scriptleri tarafından üzerine yazılmaz**.
- İlk kurulumda `.env.odak.example` → `.env` manuel kopya.
- Yeni env değişkeni eklendiğinde şablonu güncelleyip sunucuda `.env`’e elle ekleyin.

---

## Scriptler (geliştirme PC, Windows)

| Script | Görev |
|--------|--------|
| [sync-odak-source.ps1](../../../scripts/odak/sync-odak-source.ps1) | Kaynak paketle + SCP + sunucuda aç |
| [deploy-odak-apps.ps1](../../../scripts/odak/deploy-odak-apps.ps1) | Uzaktan `compose build` / `up` |

Parametreler ve örnekler script başlığında.

---

## Kaynak ve risk notları

| Konu | Not |
|------|-----|
| RAM | Tam build sırasında mng_common + build ~15 GiB sınırda; ağır build’de diğer kullanıcıları duraklatmayı düşünün |
| mngui | `GATEWAY_URL` build-arg; UI değişince **mngui image yeniden build** şart |
| MngLLM / Ollama | Odak’ta kapalı; açmak ayrı karar |
| SMTP | Mail testi opsiyonel |

---

## İlgili dokümanlar

- [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md) — servis listesi, Keycloak
- [MNG_APPS_ODAK_MUSTERI_ERISIM.md](./MNG_APPS_ODAK_MUSTERI_ERISIM.md) — portlar (IT)
- [MNG_COMMON_ODAK.md](./MNG_COMMON_ODAK.md) — altyapı
- [README.md](./README.md) — Odak indeks
