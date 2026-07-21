# Docker Desktop — lokal yığın

## Amaç

`mng_common` + `mng_apps` ile lokal stack’i ayağa kaldırma sırası ve dosya referansları.

## Ön koşullar

- Docker Desktop (WSL2 backend önerilir)
- Agent/terminal Docker komutları için tam yetki (sandbox dışı) — proje `.cursorrules` notu
- Repo kökü: MonitraNG

## Compose dosyaları

| Yığın | Dizin | Ana dosya | Override (müşteri) | Lokal not |
|-------|-------|-----------|--------------------|-----------|
| Altyapı | `ApplicationResources/mng_common` | `docker-compose.yml` | `docker-compose.odak.yml` vb. | Lokal’de hangi override? _TBD_ |
| Uygulama | `ApplicationResources/mng_apps` | `docker-compose.yml` | `docker-compose.odak.yml` / `production` | Lokal Development compose | 

Lokal MVP için çoğu senaryoda **repo’daki standart** `docker-compose.yml` (Development) yeterli olabilir; Odak override’ları müşteri HTTP/path özelidir.

## Önerilen sıra

```text
1. mng_common  →  up -d  →  network + health
2. Domain / Keycloak hazırlık  →  USERS_AND_AUTH.md
3. mng_apps    →  build / up (çekirdek veya full)
4. Smoke       →  CHECKLIST.md
```

Komutlar karar sonrası buraya net yazılacak. Servisleri kullanıcı çalıştırır; AI kendiliğinden `docker compose up` yapmaz (açık talep hariç).

## Port çakışması kontrol listesi

| Port | Tipik kullanım | Bu makinede boş mu? |
|------|----------------|---------------------|
| 3000 | UI | |
| 3001 | Domain UI | |
| 5040 | Gateway | |
| 5001 | Keeper | |
| 5010 | DataGateway | |
| 8080 | Keycloak | |
| 27017 | Mongo | |
| 5672 / 15672 | RabbitMQ | |
| 6379 | Redis | |

## `.env`

- Şablon: `env.example` / `.env.odak.example` / `.env.example`
- Gerçek dosya: gitignore; dokümana kopyalanmaz

## Ağ

Uygulama konteynerleri genelde external network: `mng_common_mng_network`.

## Bilinen tuzaklar

- Önce apps, common yok → network hatası
- Eski volume + yeni Keycloak secret → login bozulur (volume reset kararı gerekir)
- Hosts yokken `*.monitra.local` → tarayıcı çözemez
