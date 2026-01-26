# GitLab Kullanıcı Bilgileri

**Tarih:** 2 Ocak 2026  
**Domain:** `gitlab.monitrang.com`

---

## 🔐 Root Kullanıcı Bilgileri

### Giriş Bilgileri
- **URL:** `https://gitlab.monitrang.com`
- **Kullanıcı Adı:** `root`
- **Şifre:** `MonitraNG2026!`

> ⚠️ **Güvenlik Notu:** İlk girişten sonra şifreyi mutlaka değiştirin!

---

## 📝 Şifre Değiştirme

### Web UI ile
1. GitLab'a giriş yapın: `https://gitlab.monitrang.com`
2. Sağ üst köşedeki profil ikonuna tıklayın
3. **"Edit profile"** veya **"Preferences"** seçin
4. Sol menüden **"Password"** seçin
5. Yeni şifreyi girin ve kaydedin

### Komut Satırı ile
```bash
docker exec -it gitlab gitlab-rails runner "user = User.find_by(username: 'root'); user.password = 'YENI_SIFRE'; user.password_confirmation = 'YENI_SIFRE'; user.save!"
```

---

## 🔗 Erişim URL'leri

- **HTTPS:** `https://gitlab.monitrang.com`
- **SSH:** `ssh://git@gitlab.monitrang.com:2222`
- **Localhost:** `http://localhost:8090` (internal only)

---

**Son Güncelleme:** 2 Ocak 2026

