# GitLab Runner Token Bulma Rehberi

**Sorun:** "Set up a specific runner manually" bölümü görünmüyor

---

## 🔍 Runner Token'ı Bulma Yöntemleri

GitLab'ın farklı versiyonlarında token bulma yeri değişebilir. İşte birkaç yöntem:

---

## Yöntem 1: Settings > CI/CD > Runners (Genişlet)

1. **GitLab proje sayfasında:**
   - `Settings > CI/CD` sekmesine gidin
   - **"Runners"** bölümünü bulun
   - **"Expand"** veya **">"** işaretine tıklayarak bölümü genişletin

2. **Token'ı arayın:**
   - **"Set up a specific runner manually"** veya
   - **"Register a runner manually"** veya
   - **"Project runners"** bölümünde
   - Bir token veya **"Registration token"** görmelisiniz

---

## Yöntem 2: Yeni Runner Ekleme Butonu

1. **Settings > CI/CD > Runners** sayfasında
2. **"New instance runner"** veya **"New project runner"** butonuna bakın
3. Bu butona tıkladığınızda token gösterilebilir

---

## Yöntem 3: API ile Token Alma

GitLab API kullanarak token'ı alabilirsiniz:

```bash
# GitLab API ile project runner token al
curl --header "PRIVATE-TOKEN: YOUR_PERSONAL_ACCESS_TOKEN" \
  "http://localhost/api/v4/projects/1/runners_token"
```

Ancak bu için önce Personal Access Token gerekiyor.

---

## Yöntem 4: GitLab Admin Area (Root Kullanıcısı için)

Root kullanıcısı olarak admin area'dan token alabilirsiniz:

1. Sağ üst köşedeki profil ikonuna tıklayın
2. **"Admin Area"** (veya menüden **"Admin"**) seçin
3. **"Overview > Runners"** veya **"CI/CD > Runners"** gidin
4. Burada instance runner token görebilirsiniz (proje runner token değil)

---

## Yöntem 5: GitLab Config Dosyasından (Container İçinden)

GitLab container içinden project token'ı alabiliriz:

```bash
# GitLab container içinden project ID'yi bul
docker exec gitlab gitlab-rails runner "puts Project.find_by_full_path('root/MonitraNG').id"

# Project runner token'ı al
docker exec gitlab gitlab-rails runner "puts Project.find_by_full_path('root/MonitraNG').runners_token"
```

---

## 🚀 En Hızlı Yöntem: Container'dan Token Alma

GitLab container'dan direkt token'ı alalım:

