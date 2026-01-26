# Hub Store Subscription Pattern

## Genel Bakış

Mng.Ui uygulamasında SignalR mesajlarını yönetmek için merkezi bir **Subscription Pattern** kullanılmaktadır. Bu pattern, tüm uygulama genelinde tek bir SignalR bağlantısı yönetir ve component'lerin ilgili mesajlara subscribe olmasını sağlar.

## Mimari Tasarım

### Hub Store (`stores/hub.ts`)

Hub Store, tüm uygulama için tek bir SignalR bağlantısı yönetir ve merkezi bir event bus görevi görür:

- **Tek Bağlantı**: Tüm uygulama için tek bir SignalR `HubConnection` instance'ı
- **Internal Handler**: Tüm mesajları alan tek bir `internalHandler`
- **Subscription Pattern**: Component'ler unique subscription ID'leri ile subscribe/unsubscribe yapar
- **Filter Pattern**: Her subscription kendi filter fonksiyonu ile sadece ilgili mesajları alır

### Avantajlar

1. **Duplicate Connection Önleme**: Tek bir bağlantı, duplicate connection sorunlarını önler
2. **Merkezi Yönetim**: Tüm SignalR mesajları tek bir yerden yönetilir
3. **Ölçeklenebilir**: Yeni component'ler kolayca subscribe edebilir
4. **Filter Desteği**: Her subscription sadece ilgili mesajları alır
5. **Deduplication**: Aynı mesajın 2 kez işlenmesini önler

## Kullanım

### Subscription Oluşturma

```typescript
import { useHubStore } from '@/stores/hub';

const hubStore = useHubStore();
const subscriptionId = 'my-component';

// Hub bağlantısını başlat (eğer bağlı değilse)
await hubStore.connectToHub();

// Filter: Sadece ilgili mesajları kabul et
const filter = (data: { routingKey: string; message: any; timestamp: string }) => {
  // Örnek: Sadece data event'lerini kabul et
  return data.routingKey.includes('dataupdatedevent');
};

// Handler: Mesaj geldiğinde çağrılacak fonksiyon
const handler = (data: { routingKey: string; message: any; timestamp: string }) => {
  // Mesajı işle
  console.log('Message received:', data);
};

// Subscribe
hubStore.subscribe(subscriptionId, { filter, handler });
```

### Subscription Kaldırma

```typescript
// Component unmount olduğunda
hubStore.unsubscribe(subscriptionId);
```

### Örnek: Events Page

```typescript
// pages/apps/events/index.vue
const subscriptionId = 'events-page';

const connectToHub = async () => {
  await hubStore.connectToHub();
  
  // Subscription zaten varsa, önce kaldır
  if (hubStore.hasSubscription(subscriptionId)) {
    hubStore.unsubscribe(subscriptionId);
  }
  
  // Filter: Tüm mesajları kabul et
  const filter = (data) => true;
  
  // Handler: Mesajları ekle
  const handler = (data) => {
    messages.value.unshift({
      id: `${Date.now()}-${Math.random()}`,
      routingKey: data.routingKey,
      message: data.message,
      timestamp: new Date(data.timestamp),
      type: getEventType(data.routingKey),
    });
  };
  
  hubStore.subscribe(subscriptionId, { filter, handler });
};

onMounted(async () => {
  await connectToHub();
});

onUnmounted(async () => {
  hubStore.unsubscribe(subscriptionId);
});
```

### Örnek: Side Menu Store

```typescript
// stores/apps/sideMenu.ts
const subscriptionId = 'side-menu';

async connectToHub() {
  await hubStore.connectToHub();
  
  if (hubStore.hasSubscription(subscriptionId)) {
    hubStore.unsubscribe(subscriptionId);
  }
  
  // Filter: Sadece @side_menu dataset event'lerini kabul et
  const filter = (data) => {
    const routingKey = data.routingKey || '';
    const isDatasetEvent = routingKey.includes('datacreatedevent') || 
                           routingKey.includes('dataupdatedevent');
    
    if (!isDatasetEvent) return false;
    
    const datasetName = data.message?.DatasetName || 
                       data.message?.datasetName || null;
    
    return datasetName?.toLowerCase() === '@side_menu';
  };
  
  // Handler: Menu'yu refresh et (debounce ile)
  const handler = (data) => {
    const now = Date.now();
    const lastRefreshTime = (this as any).lastRefreshTime || 0;
    const refreshDebounceMs = 500;
    
    if (now - lastRefreshTime < refreshDebounceMs) {
      return; // Debounce: 500ms içinde tekrar çağrılırsa ignore et
    }
    
    (this as any).lastRefreshTime = now;
    this.refreshMenuItems();
  };
  
  hubStore.subscribe(subscriptionId, { filter, handler });
}
```

## Deduplication Mekanizması

Hub Store, aynı mesajın 2 kez işlenmesini önlemek için otomatik deduplication mekanizması içerir:

### Çalışma Prensibi

1. **Mesaj ID Kullanımı**: Mesaj ID'si varsa (`data.message.id`), onu kullanır (en güvenilir yöntem)
2. **Fallback**: Mesaj ID'si yoksa `routingKey + timestamp` kombinasyonunu kullanır
3. **Deduplication Window**: 2 saniye içinde aynı mesaj tekrar gelirse ignore edilir
4. **Cache Temizleme**: Eski mesajlar otomatik olarak cache'den temizlenir (memory leak önlemek için)

### Neden Gerekli?

- **Backend'den Duplicate Mesajlar**: Backend'den aynı mesaj 2 kez gelebilir (örneğin 2 farklı browser/tab açık olduğunda)
- **SignalR Reconnection**: Bağlantı yeniden kurulduğunda duplicate mesajlar gelebilir
- **Network Issues**: Ağ sorunları nedeniyle mesajlar tekrar gönderilebilir

### Örnek Log

```
[Hub] Processing message
  messageKey: 'id_f3c5d324-48b2-49d1-b9a9-70c280f44ad4'
  messageId: 'f3c5d324-48b2-49d1-b9a9-70c280f44ad4'
  routingKey: 'meral.dataupdatedevent'

[Hub] Duplicate message ignored
  messageKey: 'id_f3c5d324-48b2-49d1-b9a9-70c280f44ad4'
  timeSinceLast: 62ms
```

## Connection Yönetimi

### Bağlantı Durumları

- **`isConnected`**: Bağlantı aktif mi?
- **`isConnecting`**: Bağlantı kuruluyor mu?
- **`connectionError`**: Bağlantı hatası var mı?

### Otomatik Reconnection

SignalR otomatik reconnection mekanizması içerir:
- İlk 3 denemede: 2 saniye bekle
- Sonraki denemelerde: 5 saniye bekle

### Race Condition Önleme

`connectionPromise` mekanizması ile aynı anda birden fazla bağlantı kurulması önlenir:

```typescript
if (this.connectionPromise) {
  await this.connectionPromise; // Mevcut promise'i bekle
  return;
}
```

## Best Practices

### 1. Unique Subscription ID Kullanın

Her component için unique bir subscription ID kullanın:

```typescript
// ✅ İyi
const subscriptionId = 'events-page';
const subscriptionId = 'side-menu';

// ❌ Kötü
const subscriptionId = 'handler';
const subscriptionId = 'subscription';
```

### 2. Component Lifecycle'da Cleanup Yapın

Component unmount olduğunda subscription'ı kaldırın:

```typescript
onMounted(async () => {
  await connectToHub();
});

onUnmounted(async () => {
  hubStore.unsubscribe(subscriptionId);
});
```

### 3. Filter Fonksiyonlarını Optimize Edin

Filter fonksiyonları her mesaj için çalışır, bu yüzden optimize edin:

```typescript
// ✅ İyi: Hızlı kontroller önce
const filter = (data) => {
  if (!data.routingKey) return false;
  if (!data.routingKey.includes('data')) return false;
  // Daha ağır kontroller sonra
  return data.message?.datasetName === '@side_menu';
};

// ❌ Kötü: Ağır kontroller önce
const filter = (data) => {
  const datasetName = data.message?.DatasetName || 
                     data.message?.datasetName || 
                     data.message?.Dataset || 
                     data.message?.dataset || null;
  return datasetName?.toLowerCase() === '@side_menu';
};
```

### 4. Handler'larda Debounce Kullanın

Hızlı ardışık mesajlar için debounce kullanın:

```typescript
const handler = (data) => {
  const now = Date.now();
  const lastTime = (this as any).lastRefreshTime || 0;
  const debounceMs = 500;
  
  if (now - lastTime < debounceMs) {
    return; // Debounce
  }
  
  (this as any).lastRefreshTime = now;
  // İşlemi yap
};
```

### 5. Error Handling

Handler'larda error handling yapın (Hub Store zaten try-catch içinde çağırır, ama ekstra koruma için):

```typescript
const handler = (data) => {
  try {
    // İşlemi yap
  } catch (error) {
    console.error('Handler error:', error);
    // Hata durumunda ne yapılacağı
  }
};
```

## API Referansı

### `connectToHub()`

SignalR bağlantısını kurar (eğer bağlı değilse).

```typescript
await hubStore.connectToHub();
```

### `disconnectFromHub()`

SignalR bağlantısını kapatır ve tüm subscription'ları temizler.

```typescript
await hubStore.disconnectFromHub();
```

### `subscribe(subscriptionId, options)`

Yeni bir subscription ekler.

**Parametreler:**
- `subscriptionId: string` - Unique subscription identifier
- `options: SubscriptionOptions` - Subscription options
  - `filter: (data: HubMessage) => boolean` - Filter function
  - `handler: (data: HubMessage) => void` - Handler function

**Dönüş Değeri:**
- `boolean` - `true` if subscription was added, `false` if already exists

### `unsubscribe(subscriptionId)`

Subscription'ı kaldırır.

**Parametreler:**
- `subscriptionId: string` - Subscription identifier to remove

**Dönüş Değeri:**
- `boolean` - `true` if subscription was removed, `false` if not found

### `hasSubscription(subscriptionId)`

Subscription'ın var olup olmadığını kontrol eder.

**Parametreler:**
- `subscriptionId: string` - Subscription identifier

**Dönüş Değeri:**
- `boolean` - `true` if subscription exists

### Getters

- `connection: HubConnection | null` - SignalR connection instance
- `connected: boolean` - Bağlantı aktif mi?
- `connecting: boolean` - Bağlantı kuruluyor mu?
- `error: string | null` - Bağlantı hatası
- `subscriptionCount: number` - Aktif subscription sayısı

## Sorun Giderme

### Duplicate Mesajlar

Eğer duplicate mesajlar görüyorsanız:

1. **Console log'larını kontrol edin**: `[Hub] Duplicate message ignored` görünüyor mu?
2. **Mesaj ID'sini kontrol edin**: Mesajlarda `id` field'ı var mı?
3. **Deduplication window'u kontrol edin**: 2 saniye yeterli mi?

### Connection Kurulmuyor

1. **Token kontrolü**: Access token var mı?
2. **Hub URL kontrolü**: `HUB_URL` veya `GATEWAY_URL` doğru mu?
3. **CORS kontrolü**: Development'ta CORS ayarları doğru mu?

### Handler Çağrılmıyor

1. **Filter kontrolü**: Filter fonksiyonu `true` dönüyor mu?
2. **Subscription kontrolü**: Subscription eklenmiş mi? (`hasSubscription`)
3. **Connection kontrolü**: Bağlantı aktif mi? (`connected`)

## İlgili Dosyalar

- `Mng.Ui/stores/hub.ts` - Hub Store implementation
- `Mng.Ui/pages/apps/events/index.vue` - Events page example
- `Mng.Ui/stores/apps/sideMenu.ts` - Side Menu Store example

## Gelecek Geliştirmeler

- [ ] Subscription priority desteği
- [ ] Message queuing (bağlantı kesildiğinde)
- [ ] Subscription statistics (kaç mesaj alındı, vb.)
- [ ] WebSocket fallback mekanizması
