# MngReactor Test Planı

Bu doküman, MngReactor için test stratejisini, birim testlerini, entegrasyon testlerini ve manuel test adımlarını tanımlar. [Monitoring Implementasyon Planı](MONITORING_IMPLEMENTATION_PLAN.md) kontrol listesindeki "Her faz tamamlandığında: Birim/integrasyon testleri yazıldı" maddesine karşılık gelir.

---

## 1. Genel Bakış

| Katman | Araç | Kapsam |
|--------|------|--------|
| **Unit testler** | xUnit, Moq | Servis katmanı (CryptProcessing, ConfigString, Ingest, EngineConfigSync, DomainDefaults, EngineIdsForAssetResolver) |
| **Controller testler** | WebApplicationFactory, Integration test | API endpoint'leri |
| **Entegrasyon testler** | Testcontainers (opsiyonel) | MongoDB, RabbitMQ ile gerçek akış |
| **Manuel/script testler** | PowerShell | Health, Ingest, Config Sync, CRUD, Domain Init |

---

## 2. Test Projesi Kurulumu

### 2.1 Proje yapısı

```
MngReactor/
├── Tests/
│   ├── MngReactor.Tests/                    # Unit + Controller testleri
│   │   ├── MngReactor.Tests.csproj
│   │   ├── Helpers/
│   │   │   └── LoggerMockHelper.cs
│   │   ├── Services/
│   │   │   ├── Crypt/
│   │   │   │   └── CryptProcessingTests.cs
│   │   │   ├── Engine/
│   │   │   │   ├── ConfigStringProcessingTests.cs
│   │   │   │   ├── EngineConfigSyncProcessingTests.cs
│   │   │   │   └── EngineIdsForAssetResolverTests.cs
│   │   │   ├── Ingest/
│   │   │   │   └── IngestProcessingTests.cs
│   │   │   └── Domain/
│   │   │       └── DomainDefaultsProcessingTests.cs
│   │   └── Controllers/
│   │       ├── HealthControllerTests.cs
│   │       ├── IngestControllerTests.cs
│   │       ├── EngineControllerTests.cs
│   │       └── MonAssetsControllerTests.cs  # connection_info şifreleme
│   └── MngReactor.IntegrationTests/         # (Opsiyonel) Gerçek DB/MQ ile
│       └── ...
scripts/tests/MngReactor/
├── auth/
│   └── load-token.ps1                       # Keeper token (scripts/tests/MngKeeper/auth veya paylaşımlı)
├── test-health.ps1
├── test-ingest.ps1
├── test-config-sync.ps1
├── test-domain-init.ps1
└── test-monitoring-crud.ps1                 # Engine, Agent, Asset CRUD
```

### 2.2 NuGet paketleri (MngReactor.Tests.csproj)

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="coverlet.collector" Version="6.0.0" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
```

### 2.3 Referanslar

- `MngReactor.Application`
- `MngReactor.Persistence`
- `MngReactor.Infrastructure`
- `MngReactor.Api` (controller testleri için)

---

## 3. Unit Test Matrisi

### 3.1 CryptProcessing

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `Encrypt_Decrypt_RoundTrip` | Plain text → Encrypt → Decrypt → eşit mi | Aynı metin |
| `Encrypt_EmptyString` | Boş string şifreleme | Boş veya hata |
| `Decrypt_InvalidBase64` | Geçersiz Base64 decrypt | Hata |

### 3.2 ConfigStringProcessing

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `CreateConfigStringAsync_ValidEngine_ReturnsBase64` | Geçerli engineId → Base64 string | Non-null, Base64 format |
| `CreateConfigStringAsync_UnknownEngine_ReturnsNull` | Bilinmeyen engineId | null |
| `CreateConfigStringAsync_OtherDomain_ReturnsNull` | Başka domain'e ait engine | null |

**Mock:** `IMongoClient`, `ICryptProcessing`, `IOptions<MngReactorSettings>`

### 3.3 EngineConfigSyncProcessing

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `GetConfigAsync_ValidEngine_ReturnsResult` | Geçerli engineId → agents, assetConfigs | Non-null, agents list |
| `GetConfigAsync_UnknownEngine_ReturnsNull` | Bilinmeyen engineId | null |
| `GetConfigAsync_ConnectionInfoDecrypted` | connection_info decrypt edilmiş mi | Şifreli değil, okunabilir |

**Mock:** `IMongoClient`, `ICryptProcessing`

### 3.4 IngestProcessing

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `ProcessAsync_ValidBatch_SavedCountPositive` | Geçerli batch → MongoDB'ye yazılır | savedCount > 0 |
| `ProcessAsync_EmptyBatches_BadRequest` | Boş batches | savedCount=0 veya hata |
| `ProcessAsync_InvalidPayload_PartialSuccess` | Bozuk batch varsa partial success | errorList dolu |

**Mock:** `IMongoClient`, `IMetricPublisher`, `ICryptProcessing`, MongoDB Time Series koleksiyonu

### 3.5 DomainDefaultsProcessing

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `CreateDefaultsAsync_NewDomain_InsertsSchedulesAndPeriods` | Yeni domain → mon_schedules, mon_collection_periods | true |
| `CreateDefaultsAsync_Idempotent` | Aynı domain tekrar çağrılırsa | Hata vermez, duplicate olmaz |

**Mock:** `IMongoClient`

### 3.6 EngineIdsForAssetResolver

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `GetEngineIdsForAssetAsync_AssetWithAgents_ReturnsEngineIds` | Asset'e bağlı agent'ların engine'leri | Engine ID listesi |
| `GetEngineIdsForAssetAsync_NoAgents_ReturnsEmpty` | Asset'e agent yok | Boş liste |

**Mock:** `IMongoClient`

### 3.7 MonAssetsController – connection_info şifreleme

| Test | Açıklama | Beklenen |
|------|----------|----------|
| `Insert_WithConnectionInfo_EncryptsPassword` | connection_info.password → şifreli yazılır | Veritabanında şifreli |
| `Insert_WithAuthPassword_EncryptsNested` | connection_info.auth.password → şifreli | Veritabanında şifreli |
| `Insert_WithCommunity_EncryptsSnmpCommunity` | connection_info.community veya auth.community | Şifreli |

---

## 4. Controller / API Test Senaryoları

### 4.1 HealthController

| Senaryo | Method | Endpoint | Auth | Beklenen |
|---------|--------|----------|------|----------|
| Sağlıklı | GET | /api/v1/health | Yok | 200, MongoDB/RabbitMQ connected |
| Live | GET | /api/v1/health/live | Yok | 200, alive |
| Ready | GET | /api/v1/health/ready | Yok | 200, ready |

### 4.2 IngestController

| Senaryo | Method | Endpoint | Auth | Beklenen |
|---------|--------|----------|------|----------|
| Geçerli ingest | POST | /api/v1/ingest/metrics | Bearer | 200, savedCount |
| Token yok | POST | /api/v1/ingest/metrics | Yok | 401 |
| Boş batches | POST | /api/v1/ingest/metrics | Bearer | 400 |
| Geçersiz payload | POST | /api/v1/ingest/metrics | Bearer | 200 + failedCount/errorList |

### 4.3 EngineController

| Senaryo | Method | Endpoint | Auth | Beklenen |
|---------|--------|----------|------|----------|
| Config Sync | GET | /api/v1/engine/config?engineId={id} | Bearer | 200, agents, assetConfigs |
| Config Sync – engine yok | GET | /api/v1/engine/config?engineId=xxx | Bearer | 404 |
| Config String | GET | /api/v1/engine/config-string?engineId={id} | Bearer | 200, configString |
| Assets | GET | /api/v1/engine/assets?id={id} | Bearer | 200 |

### 4.4 MonEnginesController, MonAgentsController, MonAssetsController

| Senaryo | Method | Endpoint | Auth | Beklenen |
|---------|--------|----------|------|----------|
| List | GET | /api/v1/monitoring/engines | Bearer | 200, liste |
| Insert | POST | /api/v1/monitoring/engines | Bearer | 200, __dataId |
| Update | PUT | /api/v1/monitoring/engines | Bearer | 200 |
| Delete | DELETE | /api/v1/monitoring/engines | Bearer | 200 |
| Token yok | * | * | Yok | 401 |

(Aynı pattern mon_agents ve mon_assets için geçerli.)

### 4.5 DomainInitController

| Senaryo | Method | Endpoint | Auth | Beklenen |
|---------|--------|----------|------|----------|
| Init domain | POST | /api/v1/admin/domain/{domain}/init | Bearer (admin) | 200 |
| Tekrar init (idempotent) | POST | /api/v1/admin/domain/{domain}/init | Bearer | 200, hata vermez |

---

## 5. Entegrasyon Test Senaryoları

Gerçek MongoDB ve RabbitMQ gerektirir (Testcontainers veya test ortamı).

| Senaryo | Açıklama | Kontrol |
|---------|----------|---------|
| Ingest → MongoDB | POST ingest → mon_metrics'e doküman yazıldı mı | Time Series dokümanı var |
| Ingest → RabbitMQ | POST ingest → mesaj publish edildi mi | Queue'da mesaj |
| Ingest → lastSeenAt | POST ingest → mon_engines.lastSeenAt güncellendi mi | Tarih güncel |
| Config Sync → decrypt | connection_info şifreli saklanıyor, response'ta decrypt | Okunabilir |
| Domain created event | RabbitMQ'ya domain.created → mon_schedules/mon_collection_periods | Varsayılan kayıtlar |
| Domain init API | POST /admin/domain/{domain}/init → varsayılan kayıtlar | mon_schedules, mon_collection_periods |

---

## 6. Manuel / Script Test Adımları

### 6.1 Ön koşullar

- MngReactor, MngKeeper, MongoDB, RabbitMQ çalışıyor
- Test domain (örn. `meral`) ve kullanıcı mevcut
- Token: `scripts/tests/MngKeeper/auth/load-token.ps1` veya `get-token.ps1`

### 6.2 test-health.ps1

```powershell
# GET /api/v1/health
# Beklenen: 200, Status: healthy, MongoDB: Connected, RabbitMQ: Connected
```

### 6.3 test-ingest.ps1

```powershell
# 1. Token al
# 2. POST /api/v1/ingest/metrics
#    Body: { "batches": [ { "engineId": "...", "metrics": [...] } ] }
# Beklenen: 200, savedCount > 0
```

**Not:** Ingest payload genelde şifreli + sıkıştırılmış. Test için ya basit (şifresiz) test modu ya da Engine'den alınan gerçek payload kullanılır.

### 6.4 test-config-sync.ps1

```powershell
# 1. Token al
# 2. mon_engines'te kayıtlı engineId al
# 3. GET /api/v1/engine/config?engineId={id}
# Beklenen: 200, agents, assetConfigs
# 4. GET /api/v1/engine/config-string?engineId={id}
# Beklenen: 200, configString (Base64)
```

### 6.5 test-domain-init.ps1

```powershell
# 1. Admin token al
# 2. POST /api/v1/admin/domain/{domain}/init
# Beklenen: 200
# 3. MongoDB mng_{domain} içinde mon_schedules, mon_collection_periods kontrol
```

### 6.6 test-monitoring-crud.ps1

```powershell
# Engine CRUD: GET/POST/PUT/DELETE /api/v1/monitoring/engines
# Agent CRUD: GET/POST/PUT/DELETE /api/v1/monitoring/agents
# Asset CRUD: GET/POST/PUT/DELETE /api/v1/monitoring/assets
# Her CRUD sonrası MQTT sync publish edildi (log veya MQTT broker ile doğrula)
```

---

## 7. Test Verisi Gereksinimleri

| Veri | Kaynak | Açıklama |
|------|--------|----------|
| JWT token | MngKeeper get-token.ps1 | domain claim içermeli |
| Engine ID | mon_engines | Test domain'de en az 1 engine |
| Agent ID | mon_agents | Engine'e bağlı agent |
| Asset ID | mon_assets | connection_info ile (şifreleme testi) |
| Ingest batch | MONITORING_DATA_PRODUCTION | Örnek batch formatı |

---

## 8. Uygulama Sırası

| Öncelik | Görev | Tahmini |
|---------|-------|---------|
| 1 | Test projesi oluştur (MngReactor.Tests) | 1 gün |
| 2 | CryptProcessing unit testleri | 0.5 gün |
| 3 | ConfigStringProcessing, EngineConfigSyncProcessing (mock ile) | 1 gün |
| 4 | DomainDefaultsProcessing, EngineIdsForAssetResolver | 0.5 gün |
| 5 | HealthController, EngineController API testleri | 1 gün |
| 6 | IngestController, Mon* CRUD API testleri | 1 gün |
| 7 | test-health.ps1, test-config-sync.ps1 scriptleri | 0.5 gün |
| 8 | test-ingest.ps1, test-domain-init.ps1, test-monitoring-crud.ps1 | 1 gün |

**Toplam tahmini:** 5–6 gün

---

## 9. CI/CD Entegrasyonu

- `dotnet test MngReactor.sln` — tüm testler
- Coverlet ile coverage raporu (opsiyonel)
- Pipeline'da testler başarısızsa build fail

---

## 10. Referanslar

- [Monitoring Implementasyon Planı](MONITORING_IMPLEMENTATION_PLAN.md)
- [Monitoring Reactor Architecture](MONITORING_REACTOR_ARCHITECTURE.md)
- [Monitoring Data Production](MONITORING_DATA_PRODUCTION.md) — Ingest batch formatı
- MngDataGateway.Tests — örnek test projesi yapısı
