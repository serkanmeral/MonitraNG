# Mosquitto MQTT Broker — Kimlik Bilgileri

Bu doküman, MonitraNG ortamında kullanılan **Mosquitto MQTT broker** (ApplicationResources/mng_common docker-compose) için varsayılan kimlik bilgilerini kaydeder. Broker `allow_anonymous false` ve `password_file` ile çalışır; bağlantı için kullanıcı adı ve şifre gereklidir.

## Varsayılan kimlik bilgileri (geliştirme / test)

| Alan        | Değer        |
|------------|--------------|
| **Kullanıcı adı** | `monitrang` |
| **Şifre**         | `!2345qawsedrf` |

## Kullanım

- **Broker adresi:** `localhost:1883` (host makineden) veya `mosquitto:1883` (Docker ağı içinden).
- **Şifre dosyası:** `ApplicationResources/mng_common/mosquitto/config/passwd` — bu kullanıcı/şifre ile uyumlu olacak şekilde aşağıdaki komutla oluşturulur:

```bash
cd ApplicationResources/mng_common/mosquitto/config
docker run --rm -v "${PWD}:/mosquitto/config" eclipse-mosquitto:2.0 mosquitto_passwd -b /mosquitto/config/passwd monitrang '!2345qawsedrf'
```

Ardından Mosquitto container'ını yeniden başlatın: `docker restart mosquitto`.

- **Ortam değişkenleri:** `ApplicationResources/mng_common/env.example` içinde `MQTT_USERNAME` ve `MQTT_PASSWORD` aynı değerlerle tanımlıdır.

## Bu kimlik bilgilerini kullanan bileşenler

- **MngSim** (tren event MQTT): `TrainSim:MqttBrokerUrl`, `TrainSim:MqttUserName`, `TrainSim:MqttPassword`
- **MngReactor:** `MngReactorSettings__Mqtt__UserName`, `MngReactorSettings__Mqtt__Password` (veya env: `MQTT_USERNAME`, `MQTT_PASSWORD`)
- **MngKeeper:** MQTT ayarlarında aynı kullanıcı/şifre

**Production:** Farklı ve güçlü bir şifre kullanın; bu değerler yalnızca geliştirme/test ortamı için dokümante edilmiştir.
