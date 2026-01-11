# i18n Implementasyon Planı - Güvenli Yaklaşım

## Önemli Notlar

⚠️ **Daha önce i18n implementasyonu sorun çıkardı ve geri alma zor olmuştu.**
Bu sefer **çok dikkatli ve aşamalı** bir yaklaşım izlenecektir.

## Güvenlik Önlemleri

### 1. Git Branch Stratejisi

```bash
# Yeni branch oluştur
git checkout -b feature/i18n-implementation

# Veya main'de çalışıyorsak:
git checkout -b feature/i18n-implementation
```

**Avantajlar:**
- Sorun olursa kolayca geri dönebiliriz (`git checkout main`)
- Her aşamada commit yapabiliriz
- Test ettikten sonra merge edebiliriz

### 2. Aşamalı Implementasyon

Her aşama bağımsız test edilebilir olmalı:
1. ✅ **Aşama 0: Hazırlık** - Git branch, mevcut durum kontrolü
2. ⏳ **Aşama 1: Temel Altyapı** - Locale store, plugin (test edilebilir)
3. ⏳ **Aşama 2: Dil Dosyaları** - tr.json, en.json temel içerik
4. ⏳ **Aşama 3: Test** - Her şeyin çalıştığını doğrula
5. ⏳ **Aşama 4: Backend Error Codes** (isteğe bağlı, daha sonra)

### 3. Geri Alma Stratejisi

**Her aşamada:**
- Çalışıyorsa → Sonraki aşamaya geç
- Sorun varsa → Git commit yap (nerede kaldığımızı görmek için)
- Ciddi sorun → `git checkout main` ile geri dön

## Implementasyon Aşamaları

### Aşama 0: Hazırlık ✅

**Durum:** Tamamlandı
- Dokümantasyon hazır
- Türkçe bayrağı eklendi
- LanguageDD component çalışıyor

### Aşama 1: Temel Altyapı (Yaklaşık 1-2 saat)

**Hedef:** Locale store ve plugin oluştur, test et

**Yapılacaklar:**
1. `stores/locale.ts` oluştur
2. `plugins/locale.client.ts` oluştur
3. `plugins/vuetify.ts`'i güncelle (sadece locale store init ekle)
4. Test: Dil değiştirme çalışıyor mu?

**Önemli:** 
- Sadece altyapı, içerik yok
- Mevcut kod çalışmaya devam etmeli
- Geri alma kolay (sadece 3 dosya değişikliği)

### Aşama 2: Dil Dosyaları (Yaklaşık 2-3 saat)

**Hedef:** Temel dil dosyalarını oluştur

**Yapılacaklar:**
1. `utils/locales/tr.json` oluştur (temel key'ler)
2. `utils/locales/en.json` güncelle (temel key'ler)
3. `utils/locales/messages.ts` güncelle (tr import)
4. Test: Çeviriler çalışıyor mu?

**Önemli:**
- Sadece temel key'ler (common, errors)
- Sayfa çevirileri YOK (daha sonra)
- Geri alma kolay (sadece 3 dosya)

### Aşama 3: Test ve Doğrulama (30 dakika)

**Hedef:** Her şeyin çalıştığını doğrula

**Kontroller:**
- [ ] Dil değiştirme butonu çalışıyor
- [ ] localStorage'a kayıt yapılıyor
- [ ] Sayfa yenilendiğinde dil korunuyor
- [ ] Vuetify component mesajları doğru dilde
- [ ] Uygulama çökmeden çalışıyor

### Aşama 4: Backend Error Codes (Daha Sonra)

**Hedef:** Backend error code sistemi (opsiyonel, şimdilik gerekli değil)

**Not:** Frontend tamamlandıktan sonra yapılabilir.

## Mevcut Durum

✅ **Tamamlananlar:**
- Türkçe bayrağı eklendi
- LanguageDD component çalışıyor (legacy mode ile)
- messages.ts'ye tr eklendi (boş object)
- Dokümantasyon hazır

❌ **Yapılmayanlar:**
- Locale store yok
- Locale plugin yok
- tr.json içeriği yok
- Dil dosyaları doldurulmadı

## Sonraki Adım

**Aşama 1'e başlayalım:**
1. Git branch oluştur (opsiyonel, güvenlik için)
2. Locale store oluştur
3. Locale plugin oluştur
4. Test et
5. Sorun yoksa devam et

---

**Önemli:** Her aşamada test edelim ve sorun olursa durdurup geri alalım. Hızlı olmak yerine güvenli olmak öncelikli.
