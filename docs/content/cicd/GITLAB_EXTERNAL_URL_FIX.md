# GitLab External URL Düzeltmesi

**Sorun:** Pipeline'da `fatal: unable to access 'http://gitlab.local/root/monitrang.git/': Could not resolve host: gitlab.local` hatası

**Tarih:** 27 Aralık 2024

---

## 🔍 Sorun

GitLab yapılandırmasında `external_url 'http://gitlab.local'` olarak ayarlanmıştı, ancak runner container'ı bu hostname'i çözemiyordu.

---

## ✅ Çözüm

`external_url`'i container network ismi olan `gitlab` olarak değiştirdik:

**Önceki:**
```yaml
external_url 'http://gitlab.local'
```

**Yeni:**
```yaml
external_url 'http://gitlab'
```

---

## 🔧 Yapılan Değişiklikler

1. `ApplicationResources/mng_common/docker-compose.yml` dosyasında GitLab yapılandırması güncellendi
2. GitLab container'ı yeniden başlatıldı

---

## 📝 Notlar

- Container içinden erişim için `http://gitlab` kullanılmalı (container network ismi)
- Browser'dan erişim için `http://localhost` kullanılabilir (port mapping sayesinde)
- GitLab yapılandırması değiştiği için container yeniden başlatılması gerekti

---

## 🚀 Sonraki Adımlar

1. ✅ GitLab container'ı yeniden başlatıldı
2. ⏳ GitLab'ın tamamen başlamasını bekleyin (2-3 dakika)
3. ⏳ Pipeline'ı tekrar çalıştırın veya yeni bir push yapın

---

**Son Güncelleme:** 27 Aralık 2024

