---
title: "Locale Editor (Dil Düzenleyici)"
category: "ui-guides"
tags: ["i18n", "locale", "translation", "language"]
service: "Mng.Ui"
route: "/apps/locale-editor"
difficulty: "intermediate"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Locale Editor Sayfasına Git"
    route: "/apps/locale-editor"
    action: "Sol menüden 'Locale Editor' menü öğesine tıklayın"
    expected_result: "Locale Editor sayfası açılır"
  - order: 2
    title: "Dil Dosyasını Seç"
    route: "/apps/locale-editor"
    action: "Dil seçici'den dil seçin (tr, en, fr, ar, zh)"
    expected_result: "Seçilen dil dosyası yüklenir"
  - order: 3
    title: "Çevirileri Düzenle"
    route: "/apps/locale-editor"
    action: "JSON editor'de çevirileri düzenleyin"
    expected_result: "Çeviriler güncellenir"
  - order: 4
    title: "Dil Dosyasını Güncelle"
    route: "/apps/locale-editor"
    action: "'Dil Dosyalarını Güncelle' butonuna tıklayın (MngLLM ile otomatik çeviri)"
    expected_result: "Tüm dil dosyaları otomatik güncellenir"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Locale Editor sayfasına erişim"
related_guides:
  - "I18N Guide"
---

# Locale Editor (Dil Düzenleyici)

## Özet
Locale Editor ile uygulamanın dil dosyalarını görüntüleyebilir ve düzenleyebilirsiniz. MngLLM entegrasyonu ile otomatik çeviri desteği vardır.

## Önkoşullar
- Manager veya Admin yetkisi
- Locale Editor sayfasına erişim izni

## Özellikler
- ✅ Dil dosyalarını görüntüleme (tr, en, fr, ar, zh)
- ✅ JSON editor ile çeviri düzenleme
- ✅ Otomatik çeviri (MngLLM entegrasyonu)
- ✅ Dil dosyalarını güncelleme

## Adımlar

### 1. Locale Editor Sayfasına Git
**Route:** `/apps/locale-editor`

**Yöntem:**
1. Sol menüden **"Locale Editor"** menü öğesine tıklayın
2. Locale Editor sayfası açılır

**Beklenen Sonuç:** Dil dosyası editor'ü görüntülenir

### 2. Dil Dosyasını Seç
**Route:** `/apps/locale-editor`

**Yöntem:**
1. Üst kısımdaki dil seçici'den dil seçin:
   - 🇹🇷 Türkçe (tr)
   - 🇬🇧 İngilizce (en)
   - 🇫🇷 Fransızca (fr)
   - 🇸🇦 Arapça (ar)
   - 🇨🇳 Çince (zh)
2. Seçilen dil dosyası yüklenir

**Beklenen Sonuç:** Seçilen dil dosyasının içeriği JSON editor'de görüntülenir

### 3. Çevirileri Düzenle
**Route:** `/apps/locale-editor`

**Yöntem:**
1. JSON editor'de çevirileri düzenleyin
2. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Çeviriler güncellenir

**Dikkat:** JSON formatına dikkat edin! Geçersiz JSON kaydedilemez.

### 4. Otomatik Çeviri (MngLLM)
**Route:** `/apps/locale-editor`

**Yöntem:**
1. **"Dil Dosyalarını Güncelle"** butonuna tıklayın
2. MngLLM servisi Türkçe metinleri diğer dillere otomatik çevirir
3. Tüm dil dosyaları güncellenir

**Beklenen Sonuç:** Tüm dil dosyaları otomatik olarak güncellenir

**Not:** Bu özellik MngLLM servisinin çalışıyor olmasını gerektirir.

## Desteklenen Diller

- 🇹🇷 **Türkçe (tr)** - Varsayılan dil
- 🇬🇧 **İngilizce (en)** - Fallback dil
- 🇫🇷 **Fransızca (fr)**
- 🇸🇦 **Arapça (ar)** - RTL desteği
- 🇨🇳 **Çince (zh)**

## İlgili Linkler
- [I18N Guide](../../I18N_GUIDE.md)

---

**Son Güncelleme:** 16 Ocak 2026
