# Monitoring Simulator (MngSim)

Bu doküman, MonitraNG Monitoring için **sentetik cihaz simülasyonu** yapan **MngSim** (Monitoring Simulator) uygulamasının planını ve mevcut uygulama durumunu tanımlar. Simulator, **MonitraNG servislerine bağımlı değildir**; kendi içinde N adet sanal cihaz (PDU, host vb.) çalıştırır ve bu cihazlar **HTTP, SNMP ve MQTT** ile veri sunar. **Veriyi MngEngine çeker** — Engine, simulator'daki sanal cihazlara gerçek cihazmış gibi bağlanıp toplar, topladığı veriyi Reactor'a ingest eder.

Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanına bakınız.

---

## Uygulama Durumu (Özet)

| Faz | Protokol | Durum | Açıklama |
|-----|----------|-------|----------|
| **1** | HTTP | ✅ Tamamlandı | Host metrikleri (cpu_usage, memory, disk); cihaz başına port 19001, 19002, … |
| **2** | SNMP | ✅ Tamamlandı | PDU simülasyonu; OID 1.3.6.1.4.1.99999.1.1; port 11161, 11162, … |
| **3** | MQTT | ⏳ Planlandı | Topic `mngsim/devices/{roomId}/metrics`; Mosquitto kullanılacak |

**MngSim uygulaması:** .NET 9 Blazor + Web API; HTTP/SNMP simülasyonu; Keeper/Reactor'a bağlanmaz. UI portu: yerel **6060**, Docker **6061**. Detaylı kullanım: `MngSim/README.md`.

---

## 1. Amaç ve Prensipler

- **Test ve geliştirme:** Dashboard, query builder, alarm kuralları vb. için gerçek cihaz olmadan test.
- **Demo:** Müşteri sunumları için canlı veri akışı (örn. 5 farklı lokasyondaki 5 PDU).
- **Eğitim:** MonitraNG ve Engine kullanımı için örnek veri kaynağı.

**Temel prensipler:**

- **Bağımsızlık:** Simulator, Keeper/Reactor/domain bilmez. Gerçek hayatta bir PDU'nun bizim servislerimizi bilmesi beklenmediği gibi, simulator da bizim uygulamalarımıza bağlı değildir.
- **Veriyi Engine çeker:** Simulator veriyi "sunar"; **MngEngine** bu veriyi çeker (HTTP poll, SNMP poll, MQTT subscribe). Engine, simulator'daki her sanal cihazı ayrı bir asset gibi yapılandırır ve topladığı veriyi mevcut pipeline (Reactor ingest) ile iletir.
- **Çok cihaz:** Aynı anda birden fazla sanal cihaz simüle edilebilir (örn. 5 farklı lokasyondaki 5 PDU); her cihaz kendi endpoint'i ve kimliği ile sunulur.

---

## 2. Mimari: Bağımsız Simulator, Engine Veriyi Çeker

```
┌─────────────────────────────────────────────────────────────────────────┐
│  MngSim (Simulator) — MonitraNG'den bağımsız                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐                        │
│  │ Sanal Cihaz │ │ Sanal Cihaz │ │ Sanal Cihaz │  ... N cihaz           │
│  │ HTTP        │ │ SNMP        │ │ MQTT        │                        │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘                        │
│         │               │               │                                │
│         │  (veri sunar: endpoint / trap / topic)                         │
└─────────┼──────────────┼───────────────┼────────────────────────────────┘
          │              │               │
          │   poll       │   poll        │  subscribe
          ▼              ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  MngEngine — MonitraNG tarafı                                            │
│  Asset'ler simulator'daki cihazlara işaret eder (connection_info).      │
│  Engine toplar → Reactor Ingest → MongoDB Time Series / Dashboard        │
└─────────────────────────────────────────────────────────────────────────┘
```

- **Simulator:** Sadece sanal cihazları çalıştırır; Keeper/Reactor URL'i, token, domain yok. Konfigürasyon: hangi cihazların hangi protokol (HTTP/SNMP/MQTT) ve endpoint ile sunulacağı, lokasyon/kimlik, metrik üretim kuralları.
- **Engine:** MonitraNG'de N asset tanımlanır; her asset'in `connection_info`'su simulator'daki bir sanal cihaza gider (HTTP URL, SNMP adres/port, MQTT broker/topic). Engine bu cihazlardan veriyi çeker, Reactor'a ingest eder.

---

## 3. Protokoller: HTTP, SNMP, MQTT

Simulator'da **üç protokol** desteklenir; her sanal cihaz birini (veya gerekiyorsa birden fazlasını) kullanabilir.

| Protokol | Simulator tarafı | Engine tarafı |
|----------|------------------|---------------|
| **HTTP** | Her cihaz için HTTP endpoint (path veya port). GET ile metrik JSON döner. | Engine HTTP collector ile bu URL'lere periyodik istek atar. |
| **SNMP** | SNMP agent (v2c/v3) dinler; farklı port veya community ile N cihaz. OID'lere yanıt verir / trap gönderir. | Engine SNMP collector ile poll eder (veya trap alır). |
| **MQTT** | Sanal cihaz belirli topic'lere metrik publish eder. Broker simulator içinde veya harici. | Engine MQTT client ile subscribe eder, gelen mesajları metrik olarak işler. |

Böylece gerçek PDU'ların HTTP, SNMP veya MQTT ile nasıl sunulduğuna göre Engine aynı collector'ları simulator'a karşı da kullanır; tek fark connection_info'nun simulator adresi/port/topic'e işaret etmesidir.

---

## 4. Sanal Cihaz ve Çok Lokasyon

- **Sanal cihaz:** Simulator içinde "cihaz" = bir kimlik (id, ad, lokasyon), bir protokol (HTTP/SNMP/MQTT), endpoint bilgisi ve metrik üretim kuralları.
- **Birden fazla cihaz:** Aynı anda N cihaz (örn. 5 PDU) simüle edilir; her biri farklı path/port/topic ile sunulur. Engine'de N asset tanımlanır; her asset bir sanal cihaza bağlanır.
- **Lokasyon:** Her sanal cihaza lokasyon bilgisi (isim, şehir, bina vb.) verilebilir; bu bilgi HTTP yanıtında veya MQTT payload'da metadata olarak dönebilir; Engine'de asset metadata veya metrik etiketleri ile kullanılabilir.

Örnek: 5 lokasyondaki 5 PDU → Simulator'da 5 sanal cihaz (örn. HTTP: port 19001..19005; SNMP: port 11161..11165; MQTT: room/topic ayrımı). Engine'de 5 asset; her birinin connection_info'su ilgili simulator endpoint'ine işaret eder.

### 4.1 Port ve MQTT Topic (Room) Stratejisi

**Kararlar:** HTTP ve SNMP için **cihaz başına ayrı port**; MQTT için ortamda mevcut **Mosquitto** kullanılır, cihaz başına **kendi room (topic)** kullanılır.

#### HTTP — cihaz başına bir port

- Her sanal cihaz kendi TCP portunda dinler (örn. Cihaz 1 → 19001, Cihaz 2 → 19002).
- **Port atama:** **Temel port (base) + 1 + cihaz indeksi.** Varsayılan HTTP base = **19000** → Cihaz 0 → 19001, Cihaz 1 → 19002, … (port = base + 1 + index). Config'te base değiştirilebilir; varsayılan 19000.
- **İsteğe bağlı:** Cihaz bazında port override (çakışma olduğunda belirli cihaza manuel port).
- Engine'de her asset'in connection_info'su: `http://simulator-host:19001`, `http://simulator-host:19002`, …

#### SNMP — cihaz başına farklı port

- Her sanal cihaz kendi UDP portunda SNMP agent gibi dinler (Cihaz 1 → 11161, Cihaz 2 → 11162, …).
- **Port atama:** **SNMP temel port + cihaz indeksi.** Varsayılan SNMP base = **11161** → 11161, 11162, 11163 (port = base + index). Tek process'te N UDP dinleyici; gelen isteğin portuna göre hangi sanal cihazın OID'lerini yanıtlayacağı belli olur.
- Engine'de her asset: `address = simulator-host`, `port = 11161 | 11162 | …`.

#### MQTT — Mosquitto + cihaz başına room (topic)

- Ortamda mevcut **Mosquitto** broker kullanılır; simulator broker çalıştırmaz, sadece **publish** eder.
- Her sanal cihaz kendi **room**'unu kullanır: topic yapısı örn. `mngsim/devices/{roomId}/metrics` veya `mngsim/rooms/{roomId}/data`. `roomId` = cihaz id'si veya lokasyon kodu (loc1, loc2, pdu-ankara, …).
- Konfig'te: Mosquitto broker URL (örn. `tcp://mosquitto:1883`), topic prefix (`mngsim/devices`), her cihaz için `roomId`. Simulator bu topic'lere periyodik metrik publish eder; Engine ilgili topic'e subscribe eder.
- Engine'de her asset'in connection_info'su: broker URL + bu cihazın topic'i (örn. `mngsim/devices/loc1/metrics`).

#### Port üretim özeti

| Protokol | Port/topic atama | Varsayılan base | Örnek (5 cihaz) |
|----------|------------------|-----------------|------------------|
| **HTTP** | Base + 1 + index | **19000** | 19001, 19002, 19003, 19004, 19005 |
| **SNMP** | Base + index | **11161** | 11161, 11162, 11163, 11164, 11165 |
| **MQTT** | Port yok (broker tek); cihaz başına topic/room | — | `mngsim/devices/loc1/metrics`, `…/loc2/metrics`, … |

Böylece "sanal port" üretimi: **config'te sadece base portlar (ve isteğe bağlı override)**; cihaz eklendikçe portlar veya topic'ler otomatik ve Engine tarafında tahmin edilebilir olur.

#### Port çakışmasının önlenmesi

Port çakışması **kesinlikle olmamalı**; simulator başlarken kullanacağı tüm portları kontrol eder.

- **Başlangıçta kontrol:** Dinleyiciyi (listen) açmadan önce, her sanal cihaz için hesaplanan HTTP ve SNMP portlarının **boş (kullanılabilir)** olduğu doğrulanır. Bir port zaten kullanımdaysa simulator **başlamaz**; açık bir hata mesajı verir (hangi port, hangi protokol, hangi cihaz).
- **Rezerve aralık (önerilen):** HTTP için örn. 19001–19099, SNMP için 11161–11199 gibi bir aralık ayrılır; varsayılan base portlar (19000, 11161) bu aralıkla uyumludur. Cihaz sayısı aralığı aşarsa config/validasyon aşamasında hata verilir.
- **Otomatik atlama yok:** Çakışma durumunda başka bir porta sessizce geçilmez; böylece Engine tarafındaki connection_info (port) her zaman base+index ile eşleşir. Kullanıcı çakışmayı çözer (diğer uygulamayı kapatır veya base portu değiştirir) ve simulator'ı yeniden başlatır.
- **Uygulama:** .NET'te `TcpListener` veya socket ile kısa süreli bind denemesi yapılıp port boşsa bırakılır; veya başlangıçta tüm gerekli portları sırayla dinlemeye açıp "port already in use" durumunda exception yakalanır ve anlamlı mesajla loglanıp çıkılır.

---

## 5. Konfigürasyon Modeli (Simulator)

Simulator'ın konfigürasyonu **yalnızca kendi içeriği** ile ilgilidir; MonitraNG kayıtları (engineId, assetId vb.) simulator tarafında yoktur.

- **Sanal cihaz listesi:** Her cihaz için: id, ad, lokasyon (opsiyonel), protokol (HTTP / SNMP / MQTT), endpoint bilgisi (URL path veya port, SNMP community/port, MQTT broker/topic), üretilecek metrik türleri ve algoritmaları (rastgele, sinüs, sabit, seed).
- **Global ayarlar:** HTTP dinleme portu, SNMP port(lar), MQTT broker adresi (yerel veya harici), log seviyesi.
- **Bağımlılık:** Keeper/Reactor bağlantısı yok; config dosyası veya UI ile sadece yukarıdaki bilgiler yönetilir.

---

## 5. Veri Üretim Algoritmaları (Sanal Cihazlar)

Sanal cihazlar, seçilen protokole (HTTP, SNMP veya MQTT) göre aynı metrikleri farklı arayüzlerle sunar. Üretim kuralları Engine'in beklediği formatla uyumlu olmalıdır ([MONITORING_DATA_PRODUCTION](MONITORING_DATA_PRODUCTION.md)).

### 5.1 Host (HTTP veya SNMP ile sunulabilir)

| collectibleCode | value tipi | Örnek algoritma |
|-----------------|------------|-----------------|
| `cpu_usage` | number | 0–100 arası rastgele veya sinüs dalga; unit: `%` |
| `memory_used` | number | Sabit veya hafif salınım; unit: `KB` |
| `memory_total` | number | Sabit; unit: `KB` |
| `disk_usage` | object | `{ total, used, free, percent }` — rastgele veya deterministik |

### 5.2 SNMP (PDU simülasyonu — uygulandı)

Sanal PDU MIB: OID base `1.3.6.1.4.1.99999.1.1`. GET/GETNEXT desteklenir; community `public`. SNMP template: **PDU** (güç dağıtım) veya **Router** (MIB-II benzeri ağ cihazı).

| OID (sonek) | İsim | Tip | Açıklama |
|-------------|------|-----|----------|
| .1.1.1 | deviceName | OctetString | Cihaz adı |
| .1.1.2 | inputVoltage | Gauge32 | Giriş gerilimi (V) |
| .1.1.3 | inputCurrent | Gauge32 | Akım (0.1 A birimi) |
| .1.1.4 | activePowerW | Gauge32 | Aktif güç (W) |
| .1.1.5 | temperature | Integer32 | Sıcaklık (°C) |
| .1.1.6 | outletCount | Integer32 | Priz sayısı (8) |
| .1.1.7.1 … .1.1.7.8 | outletStatus | Integer32 | Priz durumu (1=açık, 0=kapalı) |

### 5.3 MQTT

Sanal cihaz belirli topic'lere JSON veya key-value payload publish eder. Engine subscribe edip parse eder; collectibleCode/value eşlemesi topic veya payload şemasına göre yapılır.

**Determinizm:** Seed ile tekrarlanabilir senaryolar (test için).

---

## 6. Simulator Çalışma Modları

| Mod | Açıklama |
|-----|----------|
| **Açık (listen)** | HTTP/SNMP/MQTT dinleyicileri açık; Engine bağlandığında yanıt verir / publish eder. |
| **Başlat / Durdur** | Sanal cihazların üretimini ve dinleyicileri başlatır veya durdurur. |
| **Cihaz duraklatma** | Cihaz bazında IsEnabled; duraklatılan cihaz dinleyici açmaz (erişilemez simülasyonu). |

Simulator veriyi Reactor'a göndermez; sadece "cihaz" gibi davranır. Veriyi **Engine çeker**.

---

## 7. Engine Tarafı: Asset ve connection_info

Engine'de simulator'daki her sanal cihaz için **normal bir asset** tanımlanır. Asset tipi ve `connection_info`, cihazın protokolüne göre ayarlanır:

- **HTTP:** `collection_method: "http"`, connection_info: `url` (örn. `http://simulator-host:19001/metrics` — sanal cihaz portları 19001, 19002, …; MngSim UI portu yerel 6060, Docker 6061).
- **SNMP:** `collection_method: "snmp"`, connection_info: `address`, `port`, `community` (veya v3 auth); Engine SNMP poll ile bu adrese gider.
- **MQTT:** Push/event modunda Engine MQTT'e subscribe olur; connection_info veya asset metadata'da broker URL ve topic pattern. Simulator bu topic'lere publish eder.

Ayrı "simulator asset type" gerekmez: mevcut **http**, **snmp**, **mqtt** (veya Engine'de tanımlı ilgili method) kullanılır; fark sadece connection_info'nun simulator'ın adresine/portuna/topic'ine işaret etmesidir.

---

## 9. Teknoloji ve Dağıtım

| Konu | Öneri |
|------|-------|
| **Backend** | .NET 9 — bağımsız uygulama. Keeper/Reactor bağımlılığı yok. |
| **UI** | Blazor Server — sanal cihaz listesi, protokol/endpoint config, başlat/durdur, durum. |
| **Host** | Tek process: HTTP server + SNMP agent(lar) + MQTT client (publish). |
| **Konfig** | `appsettings.json` veya UI; sanal cihaz listesi, protokol ve endpoint bilgisi. |
| **Çalışma yeri** | MonitraNG ortamında veya geliştirici makinesi; Engine'in erişebileceği ağda. |
| **Bağımlılık** | Yok (MonitraNG servislerine bağlanmaz). |
| **Docker** | `ApplicationResources/mng_others/docker-compose.yml` — port 6061. `docker compose up -d` |

---

## 10. Öncelik Sırası ve Durum

1. **Faz 1 (HTTP):** ✅ Tamamlandı — N sanal cihaz; port 19001, 19002, …; Host metrikleri (cpu_usage, memory, disk); Engine HTTP collector ile poll eder.

2. **Faz 2 (SNMP):** ✅ Tamamlandı — PDU ve Router template; OID 1.3.6.1.4.1.99999.1.1 (PDU) / MIB-II benzeri (Router); port 11161, 11162, …; GET/GETNEXT; test API (`/api/run/test-snmp`); cihaz duraklatma (erişilemez simülasyonu).
3. **Faz 3 (MQTT):** ⏳ Planlandı — Sanal cihazlar publish edecek; Engine MQTT subscribe ile veriyi çeker.
4. **Sonra:** Deterministik seed, lokasyon metadata, çok cihaz senaryoları.

---

## 11. Kararlar

| Konu | Karar |
|------|-------|
| **Uygulama adı** | **MngSim** (MonitraNG Monitoring Simulator). |
| **Bağımsızlık** | Simulator Keeper/Reactor bilmez; kendi içinde çalışır. |
| **Veriyi kim çeker** | **MngEngine** — Engine, simulator'daki sanal cihazlara bağlanıp veriyi toplar; topladığı veriyi Reactor'a ingest eder. |
| **Protokoller** | **HTTP, SNMP ve MQTT** hepsi desteklenir. Her sanal cihaz bir (veya gerekirse birden fazla) protokol ile sunulabilir. |
| **Çok cihaz** | Birden fazla sanal cihaz aynı anda simüle edilir (örn. 5 lokasyon, 5 PDU); Engine'de her biri ayrı asset olarak yapılandırılır. |
| **HTTP port** | Cihaz başına ayrı port; atama = **base + 1 + index**. Varsayılan base = **19000** (→ 19001, 19002, …). |
| **SNMP port** | Cihaz başına farklı port; atama = **base + index**. Varsayılan base = **11161** (→ 11161, 11162, …). |
| **MQTT** | Ortamda mevcut **Mosquitto** kullanılır; her cihaz kendi **room**'u (topic) ile publish eder. Topic şeması: **`mngsim/devices/{roomId}/metrics`** (bu aşamada yeterli). |
| **Port çakışması** | Olmaz: Başlangıçta tüm kullanılacak portlar kontrol edilir; herhangi biri meşgulse simulator başlamaz, açık hata mesajı verilir. Otomatik başka porta geçiş yok. |
| **UI** | Blazor Server — sanal cihaz ve protokol/endpoint yönetimi. |
| **Docker** | `ApplicationResources/mng_others/docker-compose.yml`; port 6061. |

---

## 12. Açık Kararlar

Şu aşamada netleştirilmesi gereken açık karar yok. İleride ihtiyaç olursa: MQTT topic versiyonlama, config kalıcılığı (dosya), SNMP community per device vb. eklenebilir.

---

## 13. Uygulama: MngSim (Mevcut)

**Konum:** `MngSim/` — .NET 9 Blazor Web App + Web API.

**Mevcut uygulama:** Keeper/Reactor'a push eden prototip (Faz 1 proof-of-concept). Bu davranış, "bağımsız simulator + Engine veriyi çeker" modeline göre değiştirilecek.

**Güncel kullanım ve API detayları için:** [MngSim README](../../../MngSim/README.md).

**Docker:** `ApplicationResources/mng_others/docker-compose.yml` — tek servis, port 6061. `docker compose up -d` ile çalıştırılır.

**Hedef (bu dokümana göre):**
- Simulator **yalnızca sanal cihazları** sunar: HTTP endpoint'ler, SNMP agent(lar), MQTT publish.
- **Konfig:** Sanal cihaz listesi (id, ad, lokasyon, protokol: HTTP/SNMP/MQTT, endpoint bilgisi, metrik kuralları). Keeper/Reactor URL veya token yok.
- **Engine:** MonitraNG'de N asset tanımlanır; her asset'in connection_info'su simulator'daki ilgili cihazın URL/port/topic'ine işaret eder. Engine bu cihazlardan veriyi çeker, Reactor'a ingest eder.

---

## 14. Referanslar

- [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md)
- [Monitoring Data Production](MONITORING_DATA_PRODUCTION.md)
- [Monitoring Engine Architecture](MONITORING_ENGINE_ARCHITECTURE.md)
- [Monitoring Reactor Architecture](MONITORING_REACTOR_ARCHITECTURE.md)
- [MngSim README](../../../MngSim/README.md) — Uygulama kullanım kılavuzu
- [SNMP PDU Plan](../../../MngSim/SNMP_PDU_PLAN.md) — SNMP teknik plan
