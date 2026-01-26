# Let's Encrypt - Yapılacaklar (UI ve Servisler)

**Tarih:** 2 Ocak 2026  
**Durum:** ⏳ Not Alındı - İleride Yapılacak

---

## 🔴 MngUI "Not Secure" Sorunu

### Sorun
- MngUI browser'da "Not Secure" uyarısı veriyor
- Sertifika doğru (Let's Encrypt wildcard)
- Keycloak sorunsuz çalışıyor

### Neden
- MngUI production'da `.env` dosyası yok
- Varsayılan `HUB_URL` HTTP kullanıyor: `http://localhost:5020`
- Production'da HTTPS kullanılmalı

### Çözüm (İleride Yapılacak)
1. **MngUI Production Environment Variables:**
   ```env
   GATEWAY_URL=https://api.monitrang.com
   HUB_URL=https://api.monitrang.com/hub
   ```

2. **MngUI Build ve Deploy:**
   - `.env` dosyasını production'a ekle
   - MngUI'yi yeniden build et
   - MngUI'yi yeniden başlat

3. **Kod Değişiklikleri (Gerekirse):**
   - `nuxt.config.ts` - Production için HTTPS varsayılanları
   - `pages/apps/events/index.vue` - SignalR bağlantısı HTTPS

---

## 📝 Notlar

- Let's Encrypt sertifikası başarıyla kuruldu ✅
- Tüm subdomain'ler HTTPS kullanıyor ✅
- Keycloak sorunsuz çalışıyor ✅
- MngUI mixed content uyarısı var (ileride düzeltilecek)
- UI ve servis kodlarında değişiklik yapılmayacak (şimdilik)

---

**Son Güncelleme:** 2 Ocak 2026

