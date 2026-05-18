# Madde 2: Reactor → MngHub throttle’lu “data.updated” event — Uygulama planı

**Hedef:** Ingest başarılı olduğunda UI’a tek, throttle’lu bildirim (domain bazlı; örn. 5 saniyede en fazla 1 mesaj).  
**Referans:** [ROADMAP_TODAY.md](ROADMAP_TODAY.md) §2.

---

## 1. Özet

| Bileşen | Yapılacak |
|--------|------------|
| **Reactor** | Config (throttle süresi, enable); `IIngestNotifyPublisher` + throttle’lu implementasyon; `ProcessAsync` sonunda çağrı. |
| **MngHub** | Binding: `monitoring.data.updated.{domainName}`; MessageRouter’da bu routing key → domain room. |
| **UI** | İsteğe bağlı: Hub’tan `monitoring.data.updated` alınca listeyi/haritayı yenileme. |

**Mevcut akış (değişmeyecek):** Metrik başına `monitoring.metric.inserted.{domain}` publish (MetricPublisher) aynen kalacak. MngHub, UI için **sadece** `monitoring.data.updated.{domain}` dinleyecek; böylece UI tarafındaki güncelleme sıklığı throttle’a indirilir.

---

## 2. Reactor tarafı

### 2.1 Konfigürasyon

**Dosya:** `MngReactor/Core/MngReactor.Application/Configuration/MngReactorSettings.cs`

- `MngReactorSettings` içine yeni bir bölüm eklenebilir (örn. `IngestNotify`) veya doğrudan root’a iki property:
  - **IngestNotifyThrottleSeconds** (int): Aynı domain için iki “data.updated” arasında en az bu kadar saniye olacak. **Varsayılan: 5.**
  - **IngestNotifyEnabled** (bool): `false` ise “data.updated” hiç gönderilmez. **Varsayılan: true.**

**Alternatif:** `MonitoringSettings` içine `IngestNotifyThrottleSeconds` ve `IngestNotifyEnabled` eklemek (zaten `MetricsTtlDays` var).

**appsettings / env:**  
`MngReactorSettings__Monitoring__IngestNotifyThrottleSeconds`, `MngReactorSettings__Monitoring__IngestNotifyEnabled` (veya seçilen yapıya göre).

### 2.2 Interface

**Dosya:** `MngReactor/Core/MngReactor.Application/Abstractions/Ingest/IIngestNotifyPublisher.cs` (yeni)

```csharp
namespace MngReactor.Application.Abstractions.Ingest;

/// <summary>
/// Ingest başarılı olduktan sonra UI için tek, throttle'lu "data.updated" event'i yayınlar.
/// </summary>
public interface IIngestNotifyPublisher
{
    /// <summary>
    /// Domain bazlı throttle uygular; süre dolmuşsa mng.topics'e monitoring.data.updated.{domain} yayınlar.
    /// </summary>
    Task TryPublishDataUpdatedAsync(string domain, DateTime lastIngestAtUtc, IReadOnlyList<string> engineIds, CancellationToken cancellationToken = default);
}
```

### 2.3 Implementasyon (throttle + RabbitMQ)

**Dosya:** `MngReactor/Infrastructure/MngReactor.Infrastructure/Services/IngestNotifyPublisher.cs` (yeni)

- **Exchange:** `mng.topics` (mevcut).
- **Routing key:** `monitoring.data.updated.{domain}`.
- **Payload (JSON):** `{ "domain", "lastIngestAtUtc" (ISO 8601), "engineIds" (string[]) }`.
- **Throttle:** In-memory `ConcurrentDictionary<string, DateTime>` (domain → son publish zamanı).  
  - `TryPublishDataUpdatedAsync` içinde:  
    - `IngestNotifyEnabled == false` ise return.  
    - Bu domain için son publish’ten bu yana `IngestNotifyThrottleSeconds` saniye geçmemişse return.  
    - Geçtiyse: RabbitMQ’ya mesajı gönder, sözlükte bu domain için zamanı güncelle.
- **Bağlantı:** Mevcut `MetricPublisher` ile aynı exchange’i kullanıyor; istenirse RabbitMQ bağlantısı paylaşılabilir veya `IngestNotifyPublisher` kendi connection/channel’ını açabilir (tercih: ayrı singleton, basit tutmak için).

### 2.4 IngestProcessing’e enjeksiyon ve çağrı

**Dosya:** `MngReactor/Infrastructure/MngReactor.Persistence/Services/Ingest/IngestProcessing.cs`

- Constructor’a `IIngestNotifyPublisher ingestNotifyPublisher` ekle (opsiyonel: null ise çağrı yapma).
- `ProcessAsync` sonunda, **savedCount > 0** ve **engineIds.Count > 0** ise:  
  `await _ingestNotifyPublisher.TryPublishDataUpdatedAsync(domainFromToken, DateTime.UtcNow, engineIds.ToList(), cancellationToken);`  
  (fire-and-forget değil, await; hata log’lanıp ingest sonucu bozulmasın.)

### 2.5 DI kaydı

**Dosya:** `MngReactor/Infrastructure/MngReactor.Infrastructure/ServiceRegistration.cs`

- `IIngestNotifyPublisher` → `IngestNotifyPublisher` singleton (veya scoped, throttle state domain bazlı olduğu için singleton mantıklı).

### 2.6 Test

- **Unit:** `IIngestNotifyPublisher` mock’u ile `IngestProcessing` testinde, başarılı ingest sonrası `TryPublishDataUpdatedAsync`’in doğru domain/engineIds ile çağrıldığını doğrula.
- **Throttle:** İki ardışık ingest’te ikincide publish yapılmadığını (throttle süresi içinde) doğrulayan test (implementasyon sırasında eklenebilir).

---

## 3. MngHub tarafı

### 3.1 Binding (routing key listesi)

**Dosya:** `MngHub/Infrastructure/MngHub.Infrastructure/Helpers/RoutingKeyHelper.cs`

- `BuildRoutingKeysForConnection(domainName, domainId)` içinde, domain’e özel pattern olarak **`monitoring.data.updated.{domainName}`** ekle (tam eşleşme; örn. `monitoring.data.updated.meral`).  
- Böylece her SignalR bağlantısı kendi domain’inin “data.updated” event’ini alır; `monitoring.data.updated.#` kullanmak da mümkün ama Hub’ta domain filtresi gerekir.

### 3.2 MessageRouter (routing key → SignalR room)

**Dosya:** `MngHub/Infrastructure/MngHub.Infrastructure/Services/SignalR/MessageRouter.cs`

- `RouteMessageAsync` içinde:  
  **`routingKey.StartsWith("monitoring.data.updated.")`** ise → **targetRoom = domainRoomName**, log, sonra `ReceiveMessage` ile gönder.  
- Mevcut `ReceiveMessage` payload’ı (MessageDto) aynen kullanılır; UI tarafında `routingKey === "monitoring.data.updated.{domain}"` veya event adına göre “monitoring data updated” davranışı uygulanabilir.

### 3.3 Test

- Hub’a bağlanan bir test client’ı ile `monitoring.data.updated.{domain}` routing key’li mesajın ilgili domain room’una iletildiğini doğrula (entegrasyon veya manuel test).

---

## 4. UI tarafı (opsiyonel)

- SignalR `ReceiveMessage` handler’da:  
  `routingKey` veya event tipi **“monitoring.data.updated”** (veya `monitoring.data.updated.{domain}`) ise:  
  - İlgili sayfa (monitoring listesi, harita, dashboard) için veriyi yeniden çek veya listeyi yenile.
- Bu adım “gerekirse” yapılacak; önce Reactor + Hub ile mesajın doğru room’a ulaştığı doğrulanabilir, sonra UI davranışı eklenir.

---

## 5. Uygulama sırası (önerilen)

1. **Reactor: Config** — `IngestNotifyThrottleSeconds`, `IngestNotifyEnabled` (MonitoringSettings veya ayrı blok).
2. **Reactor: IIngestNotifyPublisher + IngestNotifyPublisher** — throttle + `mng.topics` + `monitoring.data.updated.{domain}`.
3. **Reactor: IngestProcessing** — constructor’a inject, `ProcessAsync` sonunda çağrı.
4. **Reactor: DI** — ServiceRegistration.
5. **Reactor: Unit test** — IngestProcessing ve isteğe bağlı throttle testi.
6. **MngHub: RoutingKeyHelper** — `monitoring.data.updated.{domainName}` ekle.
7. **MngHub: MessageRouter** — `monitoring.data.updated.*` → domain room.
8. **Test** — Uçtan uca: Ingest → RabbitMQ → Hub → SignalR (ve isteğe bağlı UI).
9. **UI (opsiyonel)** — “monitoring data updated” event’inde yenileme.

---

## 6. Dosya referansları (mevcut)

| Bileşen | Dosya |
|--------|--------|
| Reactor config | `MngReactor/Core/MngReactor.Application/Configuration/MngReactorSettings.cs` |
| Reactor metrik publish | `MngReactor/Infrastructure/MngReactor.Infrastructure/Services/MetricPublisher.cs` |
| Reactor ingest | `MngReactor/Infrastructure/MngReactor.Persistence/Services/Ingest/IngestProcessing.cs` |
| Hub routing keys | `MngHub/Infrastructure/MngHub.Infrastructure/Helpers/RoutingKeyHelper.cs` |
| Hub router | `MngHub/Infrastructure/MngHub.Infrastructure/Services/SignalR/MessageRouter.cs` |
| Hub consumer | `MngHub/Infrastructure/MngHub.Infrastructure/Services/RabbitMq/RabbitMqConsumerService.cs` (binding burada; routing key listesi RoutingKeyHelper’dan gelir) |

Bu plan, ROADMAP_TODAY.md Madde 2’deki kararlar ve yapılacaklar listesi ile uyumludur.
