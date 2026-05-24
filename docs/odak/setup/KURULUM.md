# Odak — Uzak Debian Sunucu Kurulum Planı

**Hedef:** Uzak bir Debian sunucuya SSH ile bağlanıp ortamı analiz etmek; gerekirse Docker kurmak ve sonraki Odak kurulum adımlarına zemin hazırlamak.  
**Durum:** mng_common ve mng_apps kurulumu tamamlandı — özet: [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)  
**Son güncelleme:** 22 Mayıs 2026

---

## Genel akış

| Sıra | Aşama | Durum |
|------|--------|--------|
| 1 | SSH bağlantı bilgilerinin paylaşılması ve bağlantı testi | Tamamlandı |
| 2 | Sunucu içi analiz (OS, kaynaklar, mevcut yazılımlar) | Tamamlandı |
| 3 | Docker yoksa resmi repo üzerinden kurulum | Tamamlandı |
| 4 | Kurulum sonrası doğrulama | Tamamlandı |
| 5 | mng_common Odak compose (`docker-compose.odak.yml`) | Tamamlandı ve doğrulandı — `192.168.20.20` |
| 6 | mng_apps Odak compose (`docker-compose.production.yml` + `odak`) | Tamamlandı |

---

## SSH bağlantı bilgileri

Aşağıdaki alanları doldurun veya sohbet üzerinden paylaşın. **Gerçek parolaları veya private key içeriğini bu repoya commit etmeyin**; yalnızca bağlantı için gerekli meta bilgileri tutun.

| Alan | Değer |
|------|--------|
| Sunucu IP / hostname | `192.168.20.20` (hostname: `monitrang`) |
| SSH kullanıcısı | `odak` (günlük); `root` doğrudan SSH **kapalı** |
| SSH portu | `22` |
| Kimlik doğrulama | Parola (repoda saklanmaz) |
| Sudo | Kurulu — `odak` `sudo` ve `docker` gruplarında (parola sorar) |
| Notlar (VPN, bastion, kısıtlı IP vb.) | VMware VM, ağ `ens192`; root işlemleri `su` ile |

### Yerel bağlantı testi (Windows PowerShell)

```powershell
ssh -p <PORT> <KULLANICI>@<HOST>
```

İlk bağlantıda host key onayı ve erişim doğrulaması yapılır. Bağlantı başarılı olduktan sonra sunucu analizine geçilir.

---

## Faz 2 — Sunucu analizi

SSH ile sunucuya bağlandıktan sonra aşağıdaki komutlar çalıştırılır; çıktılar bu bölümdeki checklist ile birlikte değerlendirilir. Analiz sonucu ayrı bir oturumda güncellenir (tablo veya kısa özet).

### Çalıştırılacak komutlar

```bash
# İşletim sistemi
cat /etc/os-release
uname -a

# Kaynaklar
free -h
df -h
lscpu
nproc

# Ağ ve dinleyen portlar (root/sudo gerekebilir)
ip -br a
ss -tlnp 2>/dev/null || sudo ss -tlnp

# Docker durumu
command -v docker && docker --version
docker compose version 2>/dev/null || docker-compose --version 2>/dev/null
systemctl is-active docker 2>/dev/null
docker ps 2>/dev/null

# Diğer yaygın bileşenler
command -v git && git --version
command -v nginx && nginx -v 2>&1
ufw status 2>/dev/null || echo "ufw: yok veya yetki yok"
```

### Analiz checklist

- [x] İşletim sistemi: Debian **13 (trixie)** — `6.12.86+deb13-amd64`
- [x] Mimari: `x86_64` (VMware)
- [x] RAM toplam / kullanılabilir: **15 GiB** / ~15 GiB available
- [x] Disk kök (`/`) boş alan: **176 GiB** (`/dev/sda1` 186G, %1 kullanım)
- [x] CPU çekirdek sayısı: **8** (Intel Xeon E5-2640 v3)
- [x] İnternet / paket mirror erişimi: Evet (DNS düzeltmesi sonrası)
- [x] Docker kurulu mu: **Evet** — 29.5.2
- [x] Docker Compose (plugin): **v5.1.4**
- [ ] Firewall (UFW/nftables): Kontrol edilmedi (yetki yok)
- [ ] Çakışan servisler / kritik portlar: Kontrol edilmedi (`ss -tlnp` root gerektirir)

### Analiz özeti

```
Tarih: 21 Mayıs 2026
Analizi yapan: Cursor oturumu (SSH odak@192.168.20.20)
Özet karar: Docker kuruldu (Debian 13 trixie resmi repo). DNS dhcpcd sonrası boştu; /etc/resolv.conf.tail ile 8.8.8.8 / 1.1.1.1 eklendi.
```

### Root erişimi

- `ssh root@192.168.20.20` parola ile **reddedilir** (`PermitRootLogin` varsayılan: parola ile giriş yok).
- `odak` oturumunda `su -` ve root parolası ile yükseltme **çalışır**.
- Kurulum root (`su`) ile yapıldı; `sudo` paketi kuruldu, `odak` → `sudo` + `docker` grupları.

### DNS notu

`/etc/resolv.conf` başlangıçta nameserver içermiyordu (dhcpcd); `apt` ve `curl` başarısız oluyordu. Kalıcı kayıt: `/etc/resolv.conf.tail` (8.8.8.8, 1.1.1.1). Kurumsal DNS kullanılacaksa bu dosya güncellenmeli.

---

## Faz 3 — Docker kurulumu (yoksa)

Analizde Docker yüklü değilse veya resmi `docker-ce` beklenen sürümde değilse, Debian için Docker’ın resmi deposu kullanılır. Adımlar [Hosting CI/CD Deployment Yol Haritası](../../content/cicd/HOSTING_CI_CD_DEPLOYMENT_ROADMAP.md) ile uyumludur.

### Ön koşullar

- Debian 11/12 önerilir; hedef sunucu **Debian 13 (trixie)** — repo uyumluluğu kurulum öncesi doğrulanmalı
- `sudo` veya root erişimi
- Çıkış interneti (apt ve `download.docker.com`)

### Kurulum komutları

```bash
sudo apt update
sudo apt install -y ca-certificates curl gnupg lsb-release

sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

sudo systemctl enable --now docker
sudo usermod -aG docker "$USER"
```

Oturumu kapatıp yeniden SSH ile bağlanın veya `newgrp docker` çalıştırın; ardından sudo olmadan `docker ps` deneyin.

### Kurulum doğrulama

```bash
docker --version
docker compose version
docker run --rm hello-world
```

**Kontrol (21 Mayıs 2026):**

- [x] `docker --version` → 29.5.2
- [x] `docker compose version` → v5.1.4
- [x] `hello-world` konteyneri çalıştı
- [x] `systemctl is-active docker` → `active`
- [x] `git` kuruldu (Docker kurulumu sırasında bağımlılık olarak)

---

## Sonraki adımlar

### Tamamlanan — mng_common

- Kurulum: [MNG_COMMON_ODAK.md](./MNG_COMMON_ODAK.md) — sunucuda `/home/odak/mng_common`
- IT teslimi: [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md)
- Doğrulama: altyapı servisleri ve Keycloak Admin UI çalışıyor (`KC_HOSTNAME_PORT=8080` düzeltmesi uygulandı)

### Tamamlanan — mng_apps

- Kurulum: [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md)
- Deploy: [MNG_APPS_ODAK_DEPLOY.md](./MNG_APPS_ODAK_DEPLOY.md)
- IT teslimi: [MNG_APPS_ODAK_MUSTERI_ERISIM.md](./MNG_APPS_ODAK_MUSTERI_ERISIM.md)
- Domain + initial data: [../domain/README.md](../domain/README.md)
- **Tam özet:** [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)

---

## İlgili dokümanlar

- [Hosting CI/CD Deployment Yol Haritası](../../content/cicd/HOSTING_CI_CD_DEPLOYMENT_ROADMAP.md) — Debian Docker kurulumu ve sunucu hazırlığı
- [MngAdmin Docker Kurulum](../../content/MngAdmin/support/guides/DOCKER_SETUP.md) — Uygulama seviyesinde compose kullanımı

---

## Notlar

- SSH bilgilerini yalnızca güvenli kanaldan (sohbet, vault, yerel `.env` gitignore) paylaşın.
- Üretim sunucusunda `apt upgrade` ve Docker kurulumu için bakım penceresi planlayın.
- Analiz tamamlanmadan Docker kurulumuna geçilmemesi önerilir (disk/RAM/OS uyumu).
