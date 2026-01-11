# Mng.Ui - Mevcut Durum Raporu

**Son Güncelleme:** 2026-01-15  
**Branch:** `main`  
**Commit:** `03fa036` (Merge feature/i18n-implementation into main)

---

## Son Çalışılan Konu

**i18n (Çoklu Dil Desteği) Implementasyonu - Runtime Locale Yönetimi**

Side Menu Manager'dan menü item'ları için dil dosyalarını otomatik olarak güncelleme özelliği eklendi. Ayrıca Locale Editor sayfası ile runtime'da dil dosyalarını düzenleme imkanı sağlandı.

---

## Tamamlanan İşler

### ✅ i18n Temel Altyapı
- Turkish language support eklendi
- Runtime locale loading from MinIO (z-locale-loader.client.ts)
- Locale store (Pinia) implementasyonu
- Side Menu items ve headers için pageCode-based translation
- Türkçe bayrak ve dil seçeneği header'a eklendi

### ✅ Runtime Locale Yönetimi
- **Locale Editor Sayfası** (`/apps/locale-editor`)
  - MinIO'dan locale dosyalarını yükleme
  - JSON editor ile düzenleme
  - MinIO'ya kaydetme
  - Cache invalidation
  - Admin-only access

### ✅ Backend API (MngKeeper)
- `SystemLocalesController` eklendi
  - `GET /api/system/locales/{locale}` - Locale dosyası getirme
  - `PUT /api/system/locales/{locale}` - Locale dosyası güncelleme (Admin only)
  - `GET /api/system/locales` - Mevcut locale'leri listeleme
- `IMinioService` ve `MinioService` genişletildi
  - `GetObjectAsync` method eklendi
  - `PutObjectAsync` method eklendi
  - System bucket ve locales folder yapısı otomatik oluşturuluyor

### ✅ Side Menu Manager Entegrasyonu
- **"Dil Dosyalarını Güncelle" butonu** eklendi
  - Edit mode'da görünüyor
  - pageCode kontrolü yapılıyor
  - Tüm dil dosyalarına (tr, en, fr, ar, zh) key ekliyor
  - Header type için: `menu.headers.{pageCode}`
  - Item type için: `menu.{pageCode}`
  - Source language (Türkçe) değeri tüm dillere placeholder olarak ekleniyor
  - Locale cache invalidate ediliyor

### ✅ Dil Dosyaları
- Tüm dil dosyalarına menu key'leri eklendi (tr, en, fr, ar, zh)
- `menu.headers` objesi eklendi (home, apps, pages, administration, vb.)
- Locale files MinIO'ya yüklendi (`System/locales/`)

### ✅ Git İşlemleri
- `feature/i18n-implementation` branch'i `main`'e merge edildi
- Tüm değişiklikler commit edildi ve push edildi (GitHub + GitLab)
- Branch silindi

---

## Devam Eden İşler

Şu anda aktif olarak devam eden bir iş yok.

---

## Sonraki Adımlar

### 🔄 Kısa Vadede (Gelecek Chat'te)
1. **API Gateway Entegrasyonu (MngLLM)**
   - MngLLM için gatewayUrl kontrolü eklenmesi
   - Şu anda sadece direkt servis URL'i kullanılıyor
   - Keeper ve DataGateway pattern'i takip edilmeli
   - `server/api/llm/[...path].ts` dosyasına gatewayUrl kontrolü eklenmeli

### ✅ Tamamlanan İşler (Bu Oturumda)

1. **✅ LLM Entegrasyonu (MngLLM Service)**
   - ✅ MngLLM Service oluşturuldu ve çalışıyor
   - ✅ Side Menu Manager'a LLM çeviri entegrasyonu eklendi
   - ✅ Otomatik çeviri özelliği aktif (Türkçe → EN, FR, AR, ZH)
   - ✅ `MenuItemForm.vue` - `updateLocales` fonksiyonu LLM API çağrısı yapıyor
   - ✅ Fallback mekanizması (LLM çalışmıyorsa placeholder)
   - ✅ `apiService.ts` - `fetchFromMngLLM` fonksiyonu eklendi
   - ✅ Nuxt server API route: `server/api/llm/[...path].ts`
   - ✅ `nuxt.config.ts` - `llmUrl` eklendi (https://localhost:5030)

2. **Dokümantasyon**
   - `docs/Mng.Ui/i18n/` klasöründeki eski dokümantasyon dosyalarının durumu netleştirilmeli
   - Yeni özellikler için dokümantasyon güncellemeleri

### 📋 Orta Vadede
1. **Diğer sayfalar için i18n**
   - Dataset sayfaları
   - User/Group management sayfaları
   - Form validation mesajları
   - Error mesajları

2. **Çeviri kalitesi**
   - Placeholder değerlerin gerçek çevirilerle değiştirilmesi
   - Çeviri review süreci

---

## Önemli Notlar

### ⚠️ Dikkat Edilmesi Gerekenler
1. **Locale Editor**: Sadece admin kullanıcılar erişebilir (`AdminAuthorization` attribute)
2. **Cache Invalidation**: Locale dosyası güncellendiğinde cache invalidate ediliyor, ancak sayfa yenileme gerekebilir
3. **MinIO Bağımlılığı**: Runtime locale loading için MinIO'nun çalışır durumda olması gerekiyor
4. **Fallback Mekanizması**: MinIO'dan yüklenemezse build-time dosyalar kullanılıyor

### ✅ Çalışan Özellikler
- Dil değiştirme (header'dan)
- Side Menu item'larının translation'ı (pageCode-based)
- Header item'larının translation'ı (menu.headers.{pageCode})
- Runtime locale loading from MinIO
- Locale Editor sayfası
- Side Menu Manager'dan locale dosyalarını güncelleme

### 📁 Önemli Dosyalar
- `Mng.Ui/pages/apps/locale-editor.vue` - Locale Editor sayfası
- `Mng.Ui/plugins/z-locale-loader.client.ts` - Runtime locale loader
- `Mng.Ui/components/apps/side-menu-manager/MenuItemForm.vue` - Update locale files butonu
- `MngKeeper/Presentation/MngKeeper.Api/Controllers/SystemLocalesController.cs` - Backend API
- `MngKeeper/Infrastructure/MngKeeper.Infrastructure/Services/MinioService.cs` - MinIO service

### 🔧 Yapılandırma
- MinIO endpoint: `MngKeeperSettings:MinIO:Endpoint` (config'den)
- System bucket: `system`
- Locales folder: `System/locales/`
- Available locales: `tr`, `en`, `fr`, `ar`, `zh`

---

## Test Durumu

- ✅ Locale Editor sayfası test edildi
- ✅ Side Menu Manager'dan locale güncelleme test edildi
- ✅ Runtime locale loading test edildi
- ✅ Cache invalidation test edildi
- ✅ MinIO API endpoint'leri test edildi

---

## Commit Geçmişi

```
03fa036 Merge feature/i18n-implementation into main
393671c feat(i18n): Add locale editor and update locale files from Side Menu Manager
ed88fe6 feat(i18n): Implement pageCode-based translation for Side Menu items and headers
9e04560 feat(i18n): Add Turkish language support - preparation phase
```

---

## Sonraki Chat İçin Hazırlık

Bir sonraki chat'te şunlar konuşulabilir:
1. LLM (Ollama) entegrasyonu planlaması ve implementasyonu
2. Dokümantasyon güncellemeleri
3. Diğer sayfalar için i18n implementasyonu
4. Çeviri kalitesi iyileştirmeleri
