# Odak — Eski Kalite Uygulaması (Legacy)

**Son güncelleme:** 2 Temmuz 2026  
**Durum:** ✅ Sunucuda çalışıyor · **DB salt okunur** (`kalite_ro`)  
**Amaç:** CakePHP tabanlı eski **Kalite** uygulamasının sunucu erişimi, kaynak kodu ve migrasyon referansları.

---

## Hızlı başlangıç

| Ne yapıyorsunuz? | Doküman / script |
|------------------|------------------|
| **Uygulamayı aç** | `http://192.168.20.30/kalite/users/login` |
| Kurulum / onarım scripti | [scripts/setup-legacy-kalite-server.ps1](./scripts/setup-legacy-kalite-server.ps1) |
| **DB salt okunur mod** | [scripts/enable-legacy-kalite-db-readonly.ps1](./scripts/enable-legacy-kalite-db-readonly.ps1) |
| SSH / sunucu erişimi | [SERVER_ACCESS.md](./SERVER_ACCESS.md) |
| Uygulama özeti, menü, veri modeli | [../siparis/LEGACY_KALITE_OVERVIEW.md](../siparis/LEGACY_KALITE_OVERVIEW.md) |
| **Legacy ↔ Keeper kullanıcı karşılaştırma** | **[LEGACY_KEEPER_USER_COMPARE.md](./LEGACY_KEEPER_USER_COMPARE.md)** · son rapor: [reports/legacy-keeper-user-compare_LATEST.md](./reports/legacy-keeper-user-compare_LATEST.md) |
| Sipariş migrasyon planı | [../siparis/README.md](../siparis/README.md) |
| Lokal çalıştırma (PHP+MySQL) | [../siparis/NATIVE_LOCAL_PLAN.md](../siparis/NATIVE_LOCAL_PLAN.md) |

---

## Sunucu özeti

| Öğe | Değer |
|-----|--------|
| **Rol** | Eski Kalite (CakePHP) kaynak ve canlı ortam |
| **IP** | `192.168.20.30` |
| **SSH kullanıcı** | `odak` |
| **Uygulama yolu** | `/home/odak/html/kalite/` |
| **Web** | `http://192.168.20.30/kalite/` |
| **Giriş** | `http://192.168.20.30/kalite/users/login` |
| **Veritabanı** | MariaDB · schema `kalite` · uygulama: `kalite_ro` (SELECT only) |
| **Upload kökü** | `/home/odak/html/` (`CAKEPHP_UPLOAD_ROOT`) |

### Veri özeti (2 Temmuz 2026)

| Tablo | Kayıt |
|-------|------:|
| `packages` | 825 |
| `packageitems` | 2769 |
| `firms` | 801 |
| `users` | 111 |

### Stack

| Bileşen | Sürüm / not |
|---------|-------------|
| Apache | 2.4.58 · `kalite.conf` vhost |
| PHP (Apache) | 8.2 (CakePHP 3.10 — `debug=false` ile çalışır) |
| MariaDB | 10.11 · localhost:3306 |

---

## İlişkili MonitraNG ortamları

| Ortam | IP | Not |
|-------|-----|-----|
| Test (MonitraNG) | `192.168.20.20` | Odak POC / geliştirme |
| Prod (MonitraNG) | `192.168.20.8` | Canlı MonitraNG |
| **Eski app (Kalite)** | `192.168.20.30` | Migrasyon kaynağı |

---

## Güvenlik notu

SSH parolası [SERVER_ACCESS.md](./SERVER_ACCESS.md) içinde tutulur. Bu bilgiler hassastır; repoyu paylaşırken dikkat edin. Mümkünse erişim sonrası parola rotasyonu yapın.
