# Production sunucu durumu

**Host:** `192.168.20.8` (`monitrang-prod`)  
**Son güncelleme:** 4 Haziran 2026  
**Kaldığımız yer (detay):** [DEVAM.md](./DEVAM.md) ← **IT sonrası buradan devam**  
**Bağımsızlık:** Kendi `mng_common` + volume’lar — test `20.20` ile paylaşım yok → [INDEPENDENCE.md](./INDEPENDENCE.md)

---

## Özet

| Aşama | Durum |
|-------|--------|
| P1a mng_common dosyaları | ✅ |
| P1b mng_common `compose up` | ✅ 5 Haziran 2026 |
| P2 Keycloak realm/secret | 👤 Kullanıcı |
| P3 mng_apps deploy | ✅ |
| P2+ mng_apps / domain | ⏳ Bekliyor |

---

## Mevcut durum

| Kontrol | Sonuç |
|---------|--------|
| SSH (`odak@192.168.20.8`) | ✅ Çalışıyor |
| OS | Debian 13 (trixie) |
| `odak` sudo | ✅ |
| `odak` docker grubu | ✅ |
| Docker | ✅ 26.1.5 (`docker.io` + Compose 2.26) |
| `/home/odak/mng_common` | ✅ Senkron + `docker-compose.odak.prod.yml` + `.env` |
| `mng_common_mng_network` | ✅ |
| Keycloak | ✅ http://192.168.20.8:8080/keycloak/ → 200 |
| `/home/odak/MonitraNG` | ⚠️ Kısmi kaynak (P3’te `-Full` sync) |
| `mng_apps/.env` | ✅ Prod şablonu (secret’lar P2’de) |
| mng_apps deploy | ⏸️ Docker + mng_common sonrası |

---

## IT’den istenenler (deploy öncesi)

1. `odak` kullanıcısını **sudoers**’a eklemek (veya root ile Docker kurulumu).
2. Docker Engine + Compose plugin kurulumu ([../setup/KURULUM.md](../setup/KURULUM.md) Faz 3).
3. `usermod -aG docker odak` ve oturum yenileme.
4. Geliştirme ağından sunucuya **22** ve uygulama portları (3000, 5040, 8080, …).

---

## IT tamamladıktan sonra (geliştirme PC)

Tam sıra ve fazlar: **[DEVAM.md §6](./DEVAM.md)**.

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG
pwsh -File .\scripts\odak\probe-mng-common-prod.ps1
pwsh -File .\scripts\odak\setup-mng-common-odak-prod.ps1   # sıradaki adım
```

---

## Hazır dosyalar (repo)

| Dosya | Açıklama |
|-------|----------|
| [DEVAM.md](./DEVAM.md) | Checkpoint + devam komutları |
| `ApplicationResources/mng_apps/.env.odak.prod.example` | Production mng_apps `.env` |
| `ApplicationResources/mng_common/.env.odak.prod.example` | Production mng_common `.env` |
| `docker-compose.odak.prod.yml` (mng_apps + mng_common) | IP `192.168.20.8` |
| `scripts/odak/sync-mng-common-prod.ps1` | mng_common senkron |
| `scripts/odak/setup-mng-common-odak-prod.ps1` | mng_common up |
| `scripts/odak/sync-odak-prod.ps1` / `deploy-odak-prod.ps1` | P3 deploy |
| `.env.odak.prod.local` | Yerel SSH (gitignore) |
