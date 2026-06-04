# Production sunucu — erişim bilgileri

## SSH

| Alan | Değer |
|------|--------|
| Host | `192.168.20.8` |
| Port | `22` (varsayılan) |
| Kullanıcı | `odak` |
| Parola | Müşteri IT tarafından iletildi — **bu repoda saklanmaz** |

### Yerel saklama (önerilen)

```powershell
# Repo kökünden (bir kez)
Copy-Item .env.odak.prod.local.example .env.odak.prod.local
# .env.odak.prod.local içinde ODAK_PROD_SSH_PASSWORD=... doldurun
```

`.env.odak.prod.local` gitignore’dadır.

### Bağlantı testi

```powershell
ssh odak@192.168.20.8
```

İlk bağlantıda host key onayı gerekir. `Posh-SSH` scriptleri `-AcceptKey` kullanır.

---

## Test sunucu (karşılaştırma)

| Alan | Test |
|------|------|
| Host | `192.168.20.20` |
| Kullanıcı | `odak` |
| Yerel parola dosyası | `.env.odak.local` |

---

## Sudo / Docker

Test sunucudaki model ile aynı beklenir: `odak` kullanıcısı `sudo` ve `docker` gruplarında. Production’da ilk kurulumda doğrulayın ([INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md) §1).

---

## Güvenlik notları

- SSH parolasını commit, PR veya müşteri-facing dokümana **yazmayın**.
- Production `.env` ve Keycloak secret’ları yalnızca `192.168.20.8` sunucusunda tutulur; test `.env` ile karıştırmayın.
- Ağ erişimi (VPN, firewall) müşteri IT sorumluluğundadır — deploy öncesi geliştirme PC’den `22` ve uygulama portlarına erişim doğrulanmalıdır.
