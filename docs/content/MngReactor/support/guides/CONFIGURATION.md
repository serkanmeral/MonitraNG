---
title: "MngReactor Yapılandırma"
category: "guides"
tags: ["reactor", "configuration", "mqtt", "security"]
service: "MngReactor"
language: "tr"
---

# MngReactor Yapılandırma Rehberi

Bu dokümanda MngReactor'un temel yapılandırma ayarları ve özellikle MQTT bağlantısı ile production güvenlik önerileri açıklanmaktadır.

## MQTT Yapılandırması

MngReactor, Mosquitto MQTT broker'a bağlanarak `MNG/collect/#` topic'ine subscribe olur ve mesajlaşma işlemlerini yürütür.

### appsettings.json Ayarları

```json
"MngReactorSettings": {
  "Mqtt": {
    "Host": "localhost",
    "Port": 1883,
    "UserName": "monitrang",
    "Password": "!2345qawsedrf"
  }
}
```

| Alan | Açıklama | Varsayılan |
|------|----------|------------|
| **Host** | MQTT broker adresi (localhost, IP veya hostname) | - |
| **Port** | MQTT portu (standart: 1883) | 1883 |
| **UserName** | Mosquitto kimlik doğrulama kullanıcı adı | - |
| **Password** | Mosquitto kimlik doğrulama şifresi | - |

### Mosquitto Uyumluluğu

- Mosquitto `allow_anonymous false` ise UserName ve Password **zorunludur**.
- Kullanıcı/sifre `ApplicationResources/mng_common/mosquitto/config/passwd` dosyasındaki tanımlarla uyumlu olmalıdır.
- Varsayılan kullanıcı: `monitrang` (passwd dosyasında tanımlı).

### Yerel Geliştirme

Docker Compose ile Mosquitto çalıştırıldığında:

- **Host:** `localhost` (host makineden) veya `mosquitto` (aynı Docker network içindeki servisten)
- **Port:** 1883
- **UserName / Password:** passwd dosyasındaki kullanıcıyla eşleşmeli

### MQTT Bağlantısını Devre Dışı Bırakma

Host boş veya boş string ise MQTT bağlantısı kurulmaz; uygulama MQTT olmadan çalışmaya devam eder (ör. test ortamında).

---

## Production için Güvenlik

### Ortam Değişkenleri ile Override

ASP.NET Core, yapılandırmayı ortam değişkenleriyle override eder. `appsettings.json` içindeki hassas değerleri production'da ortam değişkenleriyle değiştirin:

| Ortam Değişkeni | Açıklama |
|-----------------|----------|
| `MngReactorSettings__Mqtt__Host` | MQTT broker host |
| `MngReactorSettings__Mqtt__Port` | MQTT port |
| `MngReactorSettings__Mqtt__UserName` | MQTT kullanıcı adı |
| `MngReactorSettings__Mqtt__Password` | MQTT şifresi |

**Örnek (Linux/macOS):**

```bash
export MngReactorSettings__Mqtt__Host=mqtt.example.com
export MngReactorSettings__Mqtt__UserName=monitrang
export MngReactorSettings__Mqtt__Password=GercekProductionSifresi
```

**Örnek (Docker Compose):**

```yaml
environment:
  - MngReactorSettings__Mqtt__Host=mosquitto
  - MngReactorSettings__Mqtt__Port=1883
  - MngReactorSettings__Mqtt__UserName=${MQTT_USERNAME}
  - MngReactorSettings__Mqtt__Password=${MQTT_PASSWORD}
```

### .env Dosyası (ApplicationResources/mng_common)

`env.example` dosyasında MQTT için şu değişkenler tanımlanabilir:

- `MQTT_HOST`
- `MQTT_PORT`
- `MQTT_USERNAME`
- `MQTT_PASSWORD`

Bu değerler `.env` dosyasına kopyalanıp Docker Compose ile MngReactor'a geçirilebilir.

---

## Docker Deployment

MngReactor, `ApplicationResources/mng_apps/docker-compose.yml` içinde tanımlıdır. Build ve başlatma:

```bash
cd ApplicationResources/mng_apps
docker-compose -f docker-compose.yml build mngreactor
docker-compose -f docker-compose.yml up -d mngreactor
```

**Smoke test:** `test-mngreactor-docker.ps1` scripti ile container doğrulanabilir.

---

## Diğer Önemli Ayarlar

- **MngReactorSettings:Server** – API sunucu adresi ve portu
- **MngReactorSettings:Actors:MngKeeper** – Kimlik doğrulama servisi URL’i
- **MngReactorSettings:DataGateway** – Data Gateway API URL’i
- **MngReactorSettings:RabbitMQ** – RabbitMQ bağlantı bilgileri
- **MngReactorSettings:Crypt** – Ingest şifreleme anahtarları

Detaylı teknik spesifikasyonlar için [Technical Specs](../../main/TECHNICAL_SPECS.md) dokümanına bakınız.

---

**Son Güncelleme:** Ocak 2026
