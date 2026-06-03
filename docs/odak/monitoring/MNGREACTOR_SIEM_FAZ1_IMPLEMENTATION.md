# MngReactor — SIEM Faz 1 Implementasyon Planı (dosya + PR sırası)

**Durum:** ▶️ Uygulama rehberi  
**Son güncelleme:** 3 Haziran 2026  
**Bağlam:** [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md) · [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md) · [MngReactor analiz oturumu](./DEVAM.md)

Monorepo içindeki `MngReactor/` — harici repo değil. PR'lar bu solution üzerinden açılır.

---

## 1. Strateji özeti

| Karar | Seçim |
|-------|--------|
| Yeniden yazma | ❌ — mevcut ingest/Mongo/Rabbit desenini genişlet |
| SIEM ingest route (Faz 1) | `POST /api/v1/ingest/sec-events` (metrik path'e dokunma) |
| Auth / decrypt | Mevcut `IngestDecryptMiddleware` + Bearer (metrics ile aynı) |
| Parser konumu | `MngReactor.Persistence/Services/SecEvents/Parsers/` |
| Fixture kaynağı | `tests/fixtures/siem/` (repo kökü) → test projesine kopya veya symlink |
| Observation publish | **Paralel track** — [REACTOR_NATIVE_PUBLISH_HANDOFF.md](../alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md) R1–R3 |

**Faz 1.1 (sonra):** Engine tek batch istiyorsa `kind` discriminator ile `/ingest/batch` birleşimi ([SIEM_FAZ1_SPIKE §4](./SIEM_FAZ1_SPIKE.md)).

---

## 2. Hedef dosya ağacı

```text
MngReactor/
├── Core/MngReactor.Application/
│   ├── Abstractions/SecEvents/
│   │   ├── ISecEventIngestProcessing.cs
│   │   ├── ISecEventParser.cs
│   │   ├── ISecEventParserRegistry.cs
│   │   ├── ISecEventsRepository.cs
│   │   └── ISecEventPublisher.cs
│   ├── Features/Command/Ingest/
│   │   ├── SecEventIngestRequest.cs          # Items[], Source, Raw (JsonElement)
│   │   ├── SecEventIngestResponse.cs
│   │   ├── SecEventIngestCommand.cs
│   │   └── SecEventIngestCommandHandler.cs
│   └── Models/SecEvents/
│       ├── SecEventRawContext.cs             # source.* + raw + receivedAt
│       ├── SecEventDocument.cs               # sec_events belge DTO
│       └── ParsedSecEvent.cs                 # parser çıktısı
│
├── Infrastructure/MngReactor.Persistence/
│   └── Services/SecEvents/
│       ├── SecEventIngestProcessing.cs       # orchestrator (IngestProcessing benzeri)
│       ├── SecEventsRepository.cs            # Mongo sec_events + indeksler
│       └── Parsers/
│           ├── SecEventParserRegistry.cs
│           ├── WindowsSecurityParser.cs      # windows.security.v1
│           ├── FirewallGenericSyslogParser.cs
│           └── UnknownSecEventFallback.cs    # event.action=unknown
│
├── Infrastructure/MngReactor.Infrastructure/
│   └── Services/
│       └── SecEventPublisher.cs              # sec_events.created.{domain}
│
├── Presentation/MngReactor.Api/
│   └── Controllers/Ingest/
│       └── IngestController.cs               # + SecEvents action
│
└── Tests/MngReactor.Tests/
    ├── fixtures/siem/                        # kopya: firewall_deny, 4625, unparseable
    └── Services/SecEvents/
        ├── WindowsSecurityParserTests.cs
        ├── FirewallGenericSyslogParserTests.cs
        ├── UnknownSecEventFallbackTests.cs
        └── SecEventParserRegistryTests.cs
```

---

## 3. Arayüz taslakları

### `ISecEventParser`

```csharp
public interface ISecEventParser
{
    string ParserId { get; }           // örn. windows.security.v1
    bool CanParse(SecEventRawContext raw);
    ParsedSecEvent Parse(SecEventRawContext raw);
}
```

Routing: `source.product` → registry (`windows` → `windows.security.v1`, `generic-syslog` → `firewall.generic_syslog.v1`).

### `ISecEventsRepository`

```csharp
Task<int> InsertManyAsync(string domain, IReadOnlyList<SecEventDocument> docs, CancellationToken ct);
// EnsureIndexes: @timestamp, source.type, event.action, network.srcIp
```

DB: `mng_{domain}` · koleksiyon: `sec_events` (time-series **değil** — [SIEM_PLANNING §4](./SIEM_PLANNING.md)).

### `ISecEventPublisher`

```csharp
Task PublishCreatedAsync(string domain, IReadOnlyList<SecEventCreatedMessage> messages, CancellationToken ct);
// Exchange: mng.topics (veya ayrı sec_events exchange — spike: mng.topics)
// Routing: sec_events.created.{domain}
```

### API request (Faz 1)

```json
{
  "items": [
    {
      "receivedAt": "2026-06-03T14:00:01Z",
      "source": { "type": "firewall", "product": "generic-syslog", "host": "fw01" },
      "raw": "2026-06-03T14:00:01 fw01 kernel: DENY ... SRC=203.0.113.5 DST=10.0.0.10 DPT=445"
    }
  ]
}
```

---

## 4. PR sırası (önerilen)

Her PR: `dotnet test MngReactor/Tests/MngReactor.Tests` yeşil.

| PR | Başlık | Kapsam | Kabul |
|----|--------|--------|-------|
| **PR-1** | `feat(reactor): sec_events domain models + abstractions` | §2 Application abstractions + models; DI stub'lar | Derleme + boş handler 501/placeholder |
| **PR-2** | `feat(reactor): P0 sec_event parsers + unit tests` | Windows 4625, firewall deny, unknown fallback; fixture testleri S2.1–S2.4 | Parser testleri PASS |
| **PR-3** | `feat(reactor): sec_events Mongo repository` | `SecEventsRepository`, indeksler, insert | Integration test (Testcontainers veya in-memory mock) |
| **PR-4** | `feat(reactor): sec_event ingest processing + MQ publish` | `SecEventIngestProcessing`, `SecEventPublisher`, bulk chunk (1000) | Unit test: parse→repo mock→publisher mock |
| **PR-5** | `feat(reactor): POST ingest/sec-events API` | Controller action, MediatR handler, decrypt middleware path | Swagger + manuel curl |
| **PR-6** | `test(reactor): SIEM Faz 1 E2E script` | MonitraNG: `scripts/odak/test-siem-faz1-e2e.ps1` (S4.2–S4.6) | Odak'ta PASS ([deploy checklist](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md)) |

### Paralel track — Observation publish (Alarm C6)

| PR | Başlık | Ref |
|----|--------|-----|
| **PR-O1** | `feat(reactor): IObservationPublisher + monitra.observations` | [REACTOR_NATIVE_PUBLISH_HANDOFF R1](../alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md) |
| **PR-O2** | `feat(reactor): ingest hook ObservationPublish` | R2 |
| **PR-O3** | Odak: bridge kapat + `test-observation-native-e2e.ps1` PASS | R4 |

SIEM (PR-1…6) ile Observation (PR-O1…3) **aynı geliştirici** için sıra: önce PR-O1–O2 (küçük, Alarm E2E açar) **veya** PR-1–5 (SIEM spike) — ekip önceliğine göre.

---

## 5. PR içi implementasyon notları

### PR-2 — Parser beklenen alanlar

Fixture: `tests/fixtures/siem/` ([HANDOFF §Fixture](./SIEM_FAZ1_HANDOFF.md))

| Fixture | `event.action` | Zorunlu alanlar |
|---------|----------------|-----------------|
| `firewall_deny.syslog.txt` | `denied_flow` | `network.srcIp`, `network.dstIp`, `network.dstPort` |
| `windows_4625_failed_logon.json` | `login_failed` | `actor.user`, `network.srcIp`, `event.code=4625` |
| `unparseable_01.txt` | `unknown` | `raw` korunmuş |

### PR-3 — Mongo indeksler

```javascript
// mng_{domain}.sec_events
{ "@timestamp": -1 }
{ "source.type": 1, "@timestamp": -1 }
{ "event.action": 1, "@timestamp": -1 }
{ "network.srcIp": 1, "@timestamp": -1 }
```

### PR-4 — Orchestrator akışı

```text
SecEventIngestProcessing.ProcessAsync
  foreach item in request.Items
    ctx = SecEventRawContext.From(item)
    parser = registry.Resolve(ctx)
    parsed = parser.Parse(ctx)  // catch → UnknownSecEventFallback
    doc = SecEventDocument.From(parsed, domain, ingestedAt)
    bulk.Add(doc)
  repository.InsertManyAsync (chunk 1000)
  publisher.PublishCreatedAsync (fire-and-forget, ingest başarısız sayılmaz)
```

### PR-5 — Middleware

`IngestDecryptMiddleware`: path prefix `/api/v1/ingest/` zaten metrics'i kapsıyorsa **sec-events otomatik** dahil — doğrula, gerekirse tek satır path listesi güncelle.

---

## 6. DI kayıtları (`ServiceRegistration`)

**Persistence** (`AddPersistenceServices`):

```csharp
services.AddSingleton<ISecEventParserRegistry, SecEventParserRegistry>();
services.AddSingleton<ISecEventParser, WindowsSecurityParser>();
services.AddSingleton<ISecEventParser, FirewallGenericSyslogParser>();
services.AddSingleton<ISecEventParser, UnknownSecEventFallback>();
services.AddScoped<ISecEventsRepository, SecEventsRepository>();
services.AddScoped<ISecEventIngestProcessing, SecEventIngestProcessing>();
```

**Infrastructure** (`AddInfrastructureServices`):

```csharp
services.AddSingleton<ISecEventPublisher, SecEventPublisher>();
```

---

## 7. Definition of Done (Faz S1–S4)

- [ ] PR-1…PR-5 merge
- [ ] Parser unit testleri PASS (S2.1–S2.4)
- [ ] Odak: `test-siem-faz1-e2e.ps1` S4.2–S4.6 PASS
- [ ] Mongo'da `sec_events` belgeleri; MQ'da `sec_events.created`
- [ ] [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) durum güncelle
- [ ] Engine syslog (MngEngine) — **ayrı epik**, Reactor hazır olduktan sonra

---

## 8. Bilinçli erteleme (Faz 2+)

| Madde | Neden |
|-------|--------|
| `monitra.observations` sec_event publish | [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) — Alarm Faz 2 |
| Birleşik `/ingest/batch` + `kind` | Engine tek POST isterse Faz 1.1 |
| Dedup `event.id` hash | Faz 1.1 |
| JWT imza doğrulama | R0 sertleştirme — [ODAK deploy §R0](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md) |
| OpenSearch | Spike kararı: Mongo yeterli (D4) |

---

## 9. Referanslar

- Mevcut ingest: `MngReactor/.../Ingest/IngestProcessing.cs`
- Metrik repo: `MonMetricsRepository.cs`
- MQ örnek: `MetricPublisher.cs`
- [MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md)
