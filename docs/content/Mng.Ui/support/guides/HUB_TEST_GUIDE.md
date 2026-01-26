# Hub Entegrasyonu Test Rehberi

## Genel Bakış

UI'daki SignalR Hub entegrasyonunu test etmek için bu rehberi kullanabilirsiniz. Bu test, bir kullanıcı grubu edit edildiğinde event'ların UI'daki Event Messages sayfasında gerçek zamanlı olarak görüntülenmesini doğrular.

---

## Test Senaryosu

**Hedef**: Bir kullanıcı grubu edit edildiğinde, Event Messages sayfasında `group` tipi event'in gerçek zamanlı olarak görüntülenmesi.

---

## Önkoşullar

### 1. Environment Variable Ayarları

**Dosya**: `Mng.Ui/.env` (veya `.env.local`)

```env
GATEWAY_URL=https://localhost:5040
```

**Not**: 
- `GATEWAY_URL` tanımlı ise, hub bağlantısı gateway üzerinden yapılır (`wss://localhost:5040/hub/ws`)
- `GATEWAY_URL` tanımlı değilse, direkt hub URL'i kullanılır (`ws://localhost:5020/ws`)
- `.env` dosyası `.gitignore` içinde olduğu için manuel oluşturulmalıdır
- **ÖNEMLİ**: `.env` dosyasını oluşturduktan sonra Nuxt dev server'ı yeniden başlatmanız gerekir!

### 2. Servislerin Çalışıyor Olması

Aşağıdaki servislerin çalıştığından emin olun:

- ✅ **MngGateway** (`https://localhost:5040`)
- ✅ **MngKeeper** (`https://localhost:5001`)
- ✅ **MngHub** (`http://localhost:5020`)
- ✅ **Mng.Ui** (`http://localhost:3000` veya Nuxt dev server)

**Kontrol**:
```powershell
docker ps --filter "name=mnggateway|mngkeeper|mnghub"
```

---

## Test Adımları

### Adım 1: Event Messages Sayfasını Açın

1. UI uygulamasını açın (`http://localhost:3000`)
2. Login yapın (token gereklidir)
3. **Event Mesajları** sayfasına gidin:
   - URL: `/apps/events`
   - Veya sidebar menüden "Event Mesajları" seçeneğini tıklayın

### Adım 2: Hub Bağlantısını Kontrol Edin

Event Messages sayfasında:

- ✅ **Bağlı** durumu görüyor olmalısınız (yeşil alert)
- ❌ Eğer **Bağlantı Hatası** görüyorsanız:
  - Browser console'u kontrol edin (F12 → Console)
  - Gateway ve Hub servislerinin çalıştığından emin olun
  - Token'ın geçerli olduğundan emin olun

**Konsol Logları**:
```
SignalR connected successfully
```

### Adım 3: Grup Edit Sayfasını Açın

1. **Grup Yönetimi** sayfasına gidin (`/apps/groups`)
2. Bir grup seçin ve **Düzenle** butonuna tıklayın
3. **Grup Düzenle** sayfası açılacak

### Adım 4: Grup Bilgilerini Güncelleyin

1. Grup adını değiştirin (örn: "Test Grubu" → "Test Grubu (Güncellendi)")
2. Veya açıklamayı değiştirin
3. **Kaydet** butonuna tıklayın

### Adım 5: Event Messages'da Event'i Kontrol Edin

Event Messages sayfasına dönün (veya zaten açıksa sayfayı yenileyin):

- ✅ Yeni bir **Grup** tipi event görünmeli (yeşil chip)
- ✅ `routingKey` içinde `group` kelimesi olmalı (örn: `group.updated`)
- ✅ Event detaylarında grup bilgileri olmalı
- ✅ Timestamp doğru olmalı

**Örnek Event**:
```json
{
  "routingKey": "group.updated",
  "message": {
    "groupId": "...",
    "name": "Test Grubu (Güncellendi)",
    ...
  },
  "timestamp": "2025-12-31T12:00:00Z",
  "type": "group"
}
```

---

## Sorun Giderme

### Sorun 1: Hub Bağlantısı Kurulamıyor

**Belirtiler**:
- Event Messages sayfasında "Bağlantı Hatası" görünüyor
- Console'da `SignalR connection error` mesajı

**Çözümler**:

1. **Gateway URL Kontrolü**:
   ```typescript
   // Browser console'da kontrol edin
   const config = useRuntimeConfig();
   console.log('Gateway URL:', config.public.gatewayUrl);
   console.log('Hub URL:', config.public.hubUrl);
   ```

2. **Gateway ve Hub Servislerini Kontrol Edin**:
   ```powershell
   docker logs mnggateway --tail 50
   docker logs mnghub --tail 50
   ```

3. **Token Kontrolü**:
   - Token'ın geçerli olduğundan emin olun
   - Token süresi dolmuşsa yeniden login yapın

### Sorun 2: Event Görünmüyor

**Belirtiler**:
- Hub bağlantısı başarılı
- Ancak grup edit edildiğinde event görünmüyor

**Çözümler**:

1. **Browser Console'u Kontrol Edin**:
   - `ReceiveMessage` event'i geliyor mu?
   - Event data'sı doğru mu?

2. **MngHub Loglarını Kontrol Edin**:
   ```powershell
   docker logs mnghub --tail 100
   ```

3. **MngKeeper Loglarını Kontrol Edin**:
   ```powershell
   docker logs mngkeeper --tail 100
   ```
   - Grup güncelleme event'i RabbitMQ'ya gönderiliyor mu?

4. **RabbitMQ Kontrolü**:
   - RabbitMQ'da `group.updated` routing key'ine bağlı queue var mı?
   - Mesajlar queue'ya geliyor mu?

### Sorun 3: Gateway Üzerinden Bağlantı Kurulamıyor

**Belirtiler**:
- Direkt Hub URL'i ile bağlantı kuruluyor (`http://localhost:5020`)
- Ancak Gateway üzerinden bağlantı kurulamıyor (`https://localhost:5040/hub/ws`)

**Not**: Ocelot'un WebSocket desteği sınırlıdır. Eğer Gateway üzerinden bağlantı kurulamıyorsa:

- **Geçici Çözüm**: `.env` dosyasında `GATEWAY_URL` tanımını kaldırın, böylece direkt Hub URL'i kullanılır
- **Kalıcı Çözüm**: Gateway'de SignalR için özel proxy middleware eklenebilir (gelecek geliştirme)

---

## Test Checklist

- [ ] Environment variable'lar doğru tanımlı (`GATEWAY_URL`)
- [ ] Tüm servisler çalışıyor (Gateway, Keeper, Hub, UI)
- [ ] Event Messages sayfası açık ve bağlantı başarılı
- [ ] Grup edit sayfası açık
- [ ] Grup bilgileri güncellendi
- [ ] Event Messages sayfasında yeni event görünüyor
- [ ] Event tipi "Grup" (yeşil chip)
- [ ] Event detaylarında doğru grup bilgileri var

---

## Teknik Detaylar

### Hub Bağlantı URL'i

```typescript
// pages/apps/events/index.vue
const hubBaseUrl = config.public.gatewayUrl 
  ? `${config.public.gatewayUrl}/hub`
  : (config.public.hubUrl || 'http://localhost:5020');

const connectionUrl = `${hubBaseUrl}/ws?access_token=${encodeURIComponent(token)}`;
```

**Gateway Üzerinden**: `https://localhost:5040/hub/ws?access_token=...`  
**Direkt**: `http://localhost:5020/ws?access_token=...`

### Gateway Route Yapılandırması

**Dosya**: `MngGateway/Presentation/MngGateway.Api/ocelot.json`

```json
{
  "DownstreamPathTemplate": "/ws/{everything}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "mnghub",
      "Port": 5020
    }
  ],
  "UpstreamPathTemplate": "/hub/ws/{everything}",
  "UpstreamHttpMethod": ["GET", "POST"],
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "Bearer"
  }
}
```

### Event Tipi Belirleme

**Dosya**: `Mng.Ui/pages/apps/events/index.vue`

```typescript
const getEventType = (routingKey: string): 'user' | 'group' | 'system' | 'data' | 'unknown' => {
  if (routingKey.includes('user')) return 'user';
  if (routingKey.includes('group')) return 'group';  // ← Grup event'leri burada
  if (routingKey.includes('system') || routingKey.includes('global')) return 'system';
  if (routingKey.includes('data') || ...) return 'data';
  return 'unknown';
};
```

---

## Başarı Kriterleri

✅ **Test Başarılı** eğer:
- Hub bağlantısı başarıyla kuruluyor
- Grup edit edildiğinde event gerçek zamanlı olarak görünüyor
- Event tipi "Grup" olarak işaretleniyor
- Event detaylarında doğru bilgiler var

❌ **Test Başarısız** eğer:
- Hub bağlantısı kurulamıyor
- Event görünmüyor veya gecikmeli geliyor
- Event tipi yanlış işaretleniyor
- Event detaylarında hatalı bilgiler var

---

**Son Güncelleme**: 31 Aralık 2025  
**Versiyon**: 1.0.0

