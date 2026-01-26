# i18n Test Rehberi

## LocalStorage Kontrolü

### Browser DevTools ile Kontrol

1. **Chrome/Edge:**
   - `F12` veya `Ctrl+Shift+I` (Windows) / `Cmd+Option+I` (Mac)
   - **Application** sekmesine git
   - Sol menüden **Local Storage** → `http://localhost:xxxx` (veya uygulamanın URL'i)
   - Key: `monitrang_locale`
   - Value: `tr`, `en`, `zh`, veya `ar`

2. **Firefox:**
   - `F12` veya `Ctrl+Shift+I` (Windows) / `Cmd+Option+I` (Mac)
   - **Storage** sekmesine git
   - Sol menüden **Local Storage** → `http://localhost:xxxx`
   - Key: `monitrang_locale`

3. **Browser Console ile Kontrol:**
   ```javascript
   // LocalStorage'da locale değerini gör
   localStorage.getItem('monitrang_locale')
   
   // Manuel olarak değiştir (test için)
   localStorage.setItem('monitrang_locale', 'en')
   
   // Sayfayı yenile, dil değişmeli
   location.reload()
   ```

### Test Senaryoları

#### Test 1: Dil Değiştirme
1. Dil seçici butonuna tıkla
2. Bir dil seç (örn: İngilizce)
3. Bayrak değişmeli
4. LocalStorage'da `monitrang_locale: "en"` görünmeli

#### Test 2: Sayfa Yenileme
1. Dil değiştir (örn: Çince)
2. Sayfayı yenile (`F5` veya `Ctrl+R`)
3. Dil korunmalı (Çince bayrak görünmeli)
4. LocalStorage'da `monitrang_locale: "zh"` görünmeli

#### Test 3: Browser Language Detection
1. LocalStorage'ı temizle: `localStorage.removeItem('monitrang_locale')`
2. Sayfayı yenile
3. Browser diline göre otomatik seçim yapılmalı
   - Browser dili Türkçe ise → Türkçe seçilmeli
   - Browser dili İngilizce ise → İngilizce seçilmeli
   - Browser dili desteklenmeyen bir dil ise → Türkçe (varsayılan) seçilmeli

#### Test 4: İlk Ziyaret (LocalStorage Yok)
1. LocalStorage'ı temizle: `localStorage.clear()` veya `localStorage.removeItem('monitrang_locale')`
2. Tarayıcıyı kapat ve yeniden aç
3. Uygulamayı aç
4. Browser diline göre seçim yapılmalı, Türkçe varsayılan olmalı

### Beklenen Davranış

✅ **Çalışmalı:**
- Dil değiştirme butonu çalışıyor
- Bayrak değişiyor
- LocalStorage'a kayıt yapılıyor
- Sayfa yenilendiğinde dil korunuyor
- Browser language detection çalışıyor

❌ **Çalışmamalı:**
- Uygulama çökmesi
- Console hataları
- Dil değiştirme butonu çalışmıyor

## Sorun Giderme

### LocalStorage'da Key Görmüyorum
- Browser DevTools'u aç
- Application/Storage sekmesine git
- Doğru domain'i seç (localhost:xxxx)
- Sayfayı yenile ve tekrar kontrol et

### Dil Değişmiyor
- Browser console'da hata var mı kontrol et
- LocalStorage'da doğru key var mı kontrol et
- Locale store çalışıyor mu kontrol et: `console.log(useLocaleStore().locale)`

### Sayfa Yenilendiğinde Dil Korunmuyor
- LocalStorage'da key var mı kontrol et
- Browser'ın localStorage'ı engellemediğinden emin ol
- Private/Incognito mode'da localStorage çalışmayabilir
