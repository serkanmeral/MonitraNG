# MngSim — MonitraNG Monitoring Simulator

**Bağımsız** sanal cihaz simülatörü. Keeper/Reactor’a bağlanmaz; N adet sanal cihazı **HTTP**, **SNMP** ve **MQTT** ile sunar. **Veriyi MngEngine çeker** — Engine bu endpoint’lere bağlanıp toplar, Reactor’a ingest eder.

## Gereksinimler

- .NET 9
- (MQTT cihazlar için) Ortamda Mosquitto broker (opsiyonel)

## Çalıştırma

### Yerel (dotnet run)

```bash
cd MngSim
dotnet run
```

Tarayıcıda `http://localhost:6060` açın.

### Geliştirme (dotnet watch)

Kod değişikliklerinde otomatik yeniden derleme için `dotnet watch run` kullanılabilir. **Tren haritası** (`/train-map.html`) veya API istekleri sırasında şu hata oluşursa:

- `System.IO.InvalidDataException: The archive entry was compressed using an unsupported compression method` (BrowserRefresh middleware kaynaklı)

**Çözüm seçenekleri:**

1. **Watch’ı script ile çalıştırın** (önerilen — Browser Refresh middleware tamamen yüklenmez):
   ```powershell
   .\run-watch-no-browser-refresh.ps1
   ```
2. **Tren haritası / API için watch kullanmayın:** `dotnet run` ile çalıştırın (yukarıdaki “Yerel” gibi).

Script hem `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH` hem de `ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES=Microsoft.AspNetCore.Watch.BrowserRefresh` ayarlar. IDE'den F5 ile çalıştırırken `launchSettings.json` içinde aynı exclude tanımlı.

### Docker

```bash
cd ApplicationResources/mng_others
docker compose up -d
```

Tarayıcıda `http://localhost:6061` açın. Durdurmak için: `docker compose down`.

## Konfigürasyon

1. **HTTP base port:** Varsayılan 19000 → cihaz portları 19001, 19002, … (base+1+index)
2. **SNMP base port:** Varsayılan 11161 → 11161, 11162, …
3. **MQTT broker URL:** Opsiyonel (örn. `tcp://localhost:1883`)
4. **Sanal cihazlar:** En az bir cihaz ekleyin — Id, Ad, Lokasyon (opsiyonel), Protokol (Http / Snmp / Mqtt), SNMP için template (PDU/Router), MQTT için RoomId. Cihaz **duraklatılabilir** (IsEnabled) — erişilemez cihaz testi için.

**Kaydet** ile config bellekte tutulur. **Başlat** ile HTTP ve SNMP dinleyicileri açılır (port çakışması varsa başlamaz, hata mesajı verilir). **Durdur** ile tüm dinleyiciler kapanır.

## API

| Endpoint | Açıklama |
|----------|----------|
| `GET /api/config` | Mevcut konfigürasyon |
| `POST /api/config` | Konfigürasyon güncelle (JSON: SimulatorConfig) |
| `GET /api/status` | Durum (hasConfig, isRunning, lastError, httpEndpoints, snmpEndpoints) |
| `POST /api/run/start` | Dinleyicileri başlat (port kontrolü sonrası) |
| `POST /api/run/stop` | Dinleyicileri durdur |
| `GET /api/run/test-snmp?port=11161&oid=...` | SNMP test (Net-SNMP gerekmez; simülatöre GET gönderir) |
| `GET /api/device/{id}/info` | Cihaz bilgileri (protokol, endpoint, SNMP template, durum) |
| `GET /api/device/{id}/metrics` | Cihaz canlı metrikleri (profil sayfası için) |
| `GET /api/health` | Sağlık kontrolü |

## HTTP cihazlar

Her HTTP cihaz kendi portunda dinler. Engine bu URL’lere **GET** atar; yanıt JSON:

- `GET http://localhost:19001/metrics` → `{ "collectedAt": "...", "deviceId": "loc1", "metrics": [ { "collectibleCode": "cpu_usage", "value": 45.2, "unit": "%" }, ... ] }`

Üretilen metrikler (Host): `cpu_usage`, `memory_used`, `memory_total`, `disk_usage`.

## SNMP cihazlar

Her SNMP cihaz kendi UDP portunda dinler (11161, 11162, …). **Template seçimi:** PDU (güç dağıtım) veya Router (ağ cihazı, MIB-II benzeri). PDU template OID ağacı: **1.3.6.1.4.1.99999.1.1** (sanal PDU MIB).

| OID | Açıklama |
|-----|----------|
| .1.1.1 | deviceName (OctetString) |
| .1.1.2 | inputVoltage (Gauge32, V) |
| .1.1.3 | inputCurrent (Gauge32, 0.1 A birimi) |
| .1.1.4 | activePowerW (Gauge32) |
| .1.1.5 | temperature (Integer32, °C) |
| .1.1.6 | outletCount (Integer32) |
| .1.1.7.1 … .1.1.7.8 | outletStatus (Integer32, 1=açık 0=kapalı) |

**Test (Net-SNMP yüklü değilse):** MngSim kendi test API’sini sunar; tarayıcı veya curl ile:

```bash
# Simülatör çalışırken: tek OID (varsayılan port 11161, gerilim)
# Yerel: 6060, Docker: 6061
curl "http://localhost:6060/api/run/test-snmp?port=11161&oid=1.3.6.1.4.1.99999.1.1.2"
```

Yanıt örneği: `{ "port": 11161, "requestedOid": "1.3.6.1.4.1.99999.1.1.2", "variables": [{ "oid": "...", "value": "230", "type": "Gauge32" }] }`

**Net-SNMP yüklüyse:**

```bash
snmpget -v2c -c public -p 11161 127.0.0.1 1.3.6.1.4.1.99999.1.1.2
snmpwalk -v2c -c public -p 11161 127.0.0.1 1.3.6.1.4.1.99999.1.1
```

UI’da en az bir cihazı **Protokol: Snmp** seçip Başlat’a basın; ilk SNMP cihaz portu 11161 olur.

## MQTT

- **Sanal cihaz metrikleri (Faz 3):** topic `mngsim/devices/{roomId}/metrics`, mevcut Mosquitto kullanılır.
- **Tren event’leri:** topic `mngsim/trains/events` ve `mngsim/trains/{trainId}/events`. Broker kimlik doğrulaması için `TrainSim:MqttUserName` ve `TrainSim:MqttPassword` (bkz. `docs/content/infrastructure/MOSQUITTO_CREDENTIALS.md`).

## Tren simülasyonu (konum + sensörler + event’ler)

- **Konum:** `Data/TrainSim` içinde `routes-reference.json`, `route-geometries/*.json` ve `trains-config.json` ile rota ve tren tanımlanır. Konumlar REST ile sunulur.
- **API:**  
  `GET /api/trains/positions?includeSensors=true` · `GET /api/trains/{trainId}/position?includeSensors=true` · `PATCH /api/trains/{trainId}/sensors` (override) · `GET /api/trains/events` · `POST /api/trains/{trainId}/events` (event tetikle).
- **UI:** Ana sayfada **Trenler** → tren listesi, **Sensörler** (anlık değerler + override), **Event’ler** (tetikle + log), **Tren haritası** (konum + sensör popup + son 10 dk event uyarısı).
- **Config (appsettings / TrainSim):** `GeoServerBaseUrl` (harita tile), `MqttBrokerUrl`, `MqttUserName`, `MqttPassword` (event MQTT için).

## Dokümantasyon

- **Monitoring Simulator planı:** `docs/content/monitoring_plans/MONITORING_SIMULATOR.md` — Genel mimari, protokoller, Engine entegrasyonu
- **SNMP PDU teknik plan:** `MngSim/SNMP_PDU_PLAN.md` — OID ağacı, GET/GETNEXT mantığı, uygulama adımları
- **SNMP Template planı:** `MngSim/SNMP_TEMPLATES_PLAN.md` — PDU/Router template sistemi
- **Tren simülasyonu spec:** `docs/content/offline_map/MNGSIM_TRAIN_SIMULATION_SPEC.md` — Konum, polling sensörleri, event (MQTT) modeli
- **Tren event planı:** `docs/content/offline_map/MNGSIM_TRAIN_EVENTS_PLAN.md` — Event topic’leri ve uygulama fazları
- **Mosquitto kimlik bilgileri:** `docs/content/infrastructure/MOSQUITTO_CREDENTIALS.md` — Broker kullanıcı/şifre (MngSim ve diğer servisler)
