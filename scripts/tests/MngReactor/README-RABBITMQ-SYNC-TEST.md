# RabbitMQ → MngReactor → MQTT Sync Test

## Ön Koşullar

1. **Altyapı çalışıyor:** mng_common (MongoDB, RabbitMQ, MQTT/Mosquitto)
2. **Servisler çalışıyor:** mngdatagateway, mngreactor (docker-compose)
3. **Publish mode:** mon_engines, mon_agents, mon_assets → `basic` (UI'dan ayarlandı ✓)
4. **Test verisi:** En az 1 engine kaydı (Monitoring sayfasından oluşturulabilir)

## Test Adımları

### Adım 1: Token al (ilk kez veya süresi dolduysa)

```powershell
cd scripts/tests/MngDataGateway/auth
.\get-token.ps1 -DomainName "meral" -Username "meral_admin" -Password "Admin123!"
```

(Veya kendi domain/kullanıcı bilgilerinizle)

### Adım 2: Test script'ini çalıştır

```powershell
cd scripts/tests/MngReactor
.\test-rabbitmq-sync.ps1
```

Gateway varsayılan: `http://localhost:5040`. DG direkt kullanacaksanız:

```powershell
.\test-rabbitmq-sync.ps1 -BaseUrl "http://localhost:5010"
```

### Adım 3: Sonuç kontrolü

Script çıktısında:
- **PASS:** "Monitoring sync" logları bulundu → RabbitMQ → Reactor akışı çalışıyor
- **UYARI:** Log görülmedi → RabbitMQ bağlantısı veya publish_mode kontrolü

### Adım 4: MngEngine tarafı (opsiyonel)

MngEngine ayrı bir host'ta (veya local) çalışıyorsa ve config string ile MQTT'ye bağlıysa:

1. Engine'in config string'inde domain ve engineId, test ettiğiniz engine ile eşleşmeli
2. Değişiklik yaptıktan sonra MngEngine loglarında şunları arayın:

```
[INF] MQTT sync mesajı alındı, config sync başlatılıyor...
[INF] MQTT tetikli config sync tamamlandı. Agent=X, job'lar yeniden zamanlandı
```

## Manuel Test (UI üzerinden)

1. MNG UI → Monitoring → Engine/Agent/Asset listesi
2. Bir Engine veya Agent veya Asset kaydını **düzenleyin** (örn. açıklama alanını güncelleyin)
3. Kaydet
4. MngReactor loglarını kontrol edin: `docker logs mngreactor 2>&1 | Select-String "Monitoring sync"`
5. MngEngine çalışıyorsa: Engine loglarında sync tetiklendiğini doğrulayın
