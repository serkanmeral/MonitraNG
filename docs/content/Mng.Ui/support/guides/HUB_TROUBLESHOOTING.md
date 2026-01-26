# Hub Bağlantı Sorunları ve Çözümleri

## Bilinen Sorunlar

### 1. SignalR Negotiation Hatası

**Hata Mesajı**:
```
Failed to complete negotiation with the server: TypeError: Failed to fetch
```

**Belirtiler**:
- Event Messages sayfasında "Bağlantı Hatası" görünüyor
- Browser console'da negotiation hatası
- Network sekmesinde negotiation request'i başarısız (404 veya CORS hatası)

**Olası Nedenler**:
1. **Gateway Üzerinden WebSocket Proxy Sorunu**: Ocelot'un WebSocket/negotiation endpoint desteği sınırlı olabilir
2. **CORS Sorunu**: Negotiation endpoint'i CORS policy'sinden geçemiyor olabilir
3. **HTTPS/HTTP Karışımı**: Gateway HTTPS, Hub HTTP olabilir (mixed content)
4. **Authentication Header Sorunu**: Negotiation sırasında token iletilemiyor olabilir

**Çözüm Önerileri** (Gelecek Geliştirme):
- Gateway üzerinden SignalR bağlantısı için özel proxy middleware eklenebilir
- Veya direkt Hub URL'i kullanılabilir (development için)
- Negotiation endpoint'inin gateway route'larında düzgün handle edildiğinden emin olunmalı

**Geçici Çözüm** (Development):
- `.env` dosyasından `GATEWAY_URL` tanımını kaldırın
- Böylece direkt Hub URL'i kullanılır: `ws://localhost:5020/ws`

---

## Diğer Yaygın Sorunlar

### 2. Bağlantı Kurulamıyor

**Belirtiler**:
- "Bağlantı Hatası" mesajı
- Console'da connection error

**Kontrol Edilmesi Gerekenler**:
- Gateway/Hub servislerinin çalıştığından emin olun
- Token'ın geçerli olduğundan emin olun
- Network sekmesinde hangi URL'ye bağlanmaya çalışıldığını kontrol edin

### 3. Event Görünmüyor

**Belirtiler**:
- Hub bağlantısı başarılı
- Ancak event'lar görünmüyor

**Kontrol Edilmesi Gerekenler**:
- Browser console'da `ReceiveMessage` event'i geliyor mu?
- MngHub loglarını kontrol edin
- RabbitMQ'da mesajlar var mı?

---

**Son Güncelleme**: 31 Aralık 2025  
**Durum**: Bilinen sorunlar dokümante edildi, çözümler gelecek geliştirmeler için planlandı



