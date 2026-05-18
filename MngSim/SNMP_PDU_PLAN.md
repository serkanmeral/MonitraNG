# SNMP PDU Simülasyonu – Plan

> **Uygulama durumu:** ✅ Tamamlandı (Şubat 2025)

Bu belge, MngSim içinde bir **PDU (Power Distribution Unit)** cihazının SNMP ile nasıl simüle edileceğini ve teknik plânı özetler.

---

## 1. Amaç

- **PDU benzeri** bir cihaz: güç, akım, gerilim, priz durumu, sıcaklık gibi metrikleri SNMP GET / GETNEXT ile sunmak.
- Gerçek bir MIB’e bire bir uyum zorunlu değil; Engine’in (veya herhangi bir SNMP yönetim istasyonunun) bu OID’lerden veri çekebilmesi yeterli.
- Cihaz başına **tek UDP port** (SnmpBasePort + device_index), mevcut config ile uyumlu.

---

## 2. PDU’yu Nasıl Simüle Edeceğiz?

### 2.1 Sanal “MIB” – OID ağacı

Gerçek PDU’lar (APC, Eaton vb.) kendi enterprise MIB’lerini kullanır. Simülatörde **sadece simülasyon için** kullanılan, küçük bir OID ağacı tanımlayacağız:

- **Enterprise OID:** `1.3.6.1.4.1.99999` (örnek enterprise; 99999 = MngSim)
- **PDU dalı:** `1.3.6.1.4.1.99999.1.1`

| OID (sonek) | İsim           | Tip          | Açıklama                          |
|-------------|----------------|--------------|-----------------------------------|
| .1.1.1      | deviceName     | OctetString  | Cihaz adı (VirtualDevice.Name)    |
| .1.1.2      | inputVoltage   | Gauge32      | Giriş gerilimi (V)                |
| .1.1.3      | inputCurrent   | Gauge32      | Giriş akımı (A × 10, 0.1 A çözünürlük) |
| .1.1.4      | activePowerW   | Gauge32      | Aktif güç (W)                     |
| .1.1.5      | temperature    | Integer32    | Sıcaklık (°C)                     |
| .1.1.6      | outletCount    | Integer32    | Priz sayısı (sabit, örn. 8)       |
| .1.1.7.1..N | outletStatus   | Integer32    | Priz N durumu: 1=açık, 0=kapalı   |

Değerler her istekte **IHostMetricGenerator** benzeri bir katmandan (PDU’ya özel metrik üretici) okunacak; böylece hafif rastgele/salınımlı değerler üretebiliriz (örn. gerilim 220–235 V, akım 5–25 A).

### 2.2 SNMP sürümü ve community

- **SNMP v2c** yeterli (GET / GETNEXT).
- Community: Başta **sabit "public"** kabul edilir; isteğin community’si eşleşmezse yanıt vermeyebilir veya ileride config’e alınabilir.

---

## 3. Teknik Yaklaşım

### 3.1 Kütüphane

- **Lextm.SharpSnmpLib** (NuGet): Gelen SNMP paketini decode etmek ve yanıt paketini encode etmek için.
- Agent tarafında **tam SnmpEngine pipeline** kullanmak zorunlu değil; daha hafif yol:
  - UDP socket ile dinle.
  - Gelen byte[] → `MessageFactory.ParseMessages(bytes, userRegistry)` ile parse et.
  - İstek GET veya GETNEXT ise: istenen OID’leri “PDU OID tablosu”nda ara, değer (veya GETNEXT için sonraki OID) bul.
  - `ResponseMessage` (requestId, version, community, ErrorCode.NoError, variable list) oluştur → `ToBytes()` → UDP ile gönder.

### 3.2 Dinleyici

- **Bir SNMP cihazı = bir UDP portu.** Port = `SnmpBasePort + device_index` (örn. 11161, 11162).
- `SimulatorHostService` içinde HTTP’ye ek olarak:
  - Config’ten `Protocol == "Snmp"` olan cihazları filtrele.
  - Her biri için `RunSnmpListenerAsync(port, device, ct)` görevini başlat.
- `RunSnmpListenerAsync`:
  - `UdpClient` veya `Socket(Bound)` ile `IPAddress.Any` (veya Loopback) üzerinde ilgili portu dinle.
  - Döngü: `ReceiveAsync` → parse → OID çözümle → yanıt oluştur → `SendAsync` (remote endpoint’e).
  - Hata: port zaten kullanımdaysa (SocketException), HTTP’deki gibi `_lastError` set et; Start sonunda hata varsa dinleyicileri kapat ve başarısız dön.

### 3.3 GET / GETNEXT mantığı

- **GET:** İstenen OID tam olarak tabloda varsa → ilgili değeri döndür. Yoksa → noSuchInstance veya noSuchObject.
- **GETNEXT:** İstenen OID’den **sonraki** (lexicographic order’da) tablodaki OID’yi bul; o OID’nin değerini döndür. Sonrası yoksa endOfMibView.
- Tüm “PDU OID’leri” sıralı bir liste veya sorted dictionary olarak tutulabilir; GETNEXT için “requested OID’den büyük en küçük OID” bulunur.

### 3.4 Metrik kaynağı (PDU değerleri)

- Mevcut **IHostMetricGenerator** host metrikleri (CPU, bellek, disk) üretiyor; PDU için **ayrı bir üretici** mantıklı:
  - Seçenek A: `IPduMetricGenerator` (veya `ISnmpPduMetricProvider`) interface’i + `PduMetricGenerator` implementasyonu. Metod örn. `PduSnmpValues GetValues(VirtualDevice device)` → tüm OID’lerin güncel değerlerini döner.
  - Seçenek B: `IHostMetricGenerator`’ı genişletip “cihaz tipi / protokol”e göre farklı metrik seti döndürmek (SNMP PDU için ayrı branch). Proje büyüdükçe A daha temiz olur.
- PDU değerleri: device name sabit (device.Name), gerilim/akım/güç/sıcaklık/priz sayısı ve priz durumları **her istekte** bu üreticiden alınır; böylece hafif salınım/rastgelelik eklenebilir.

---

## 4. Uygulama Adımları (Özet)

| Adım | İçerik |
|------|--------|
| 1 | MngSim.csproj’a **Lextm.SharpSnmpLib** paketini ekle. |
| 2 | **PDU OID sabitleri** ve (OID → değer üretimi) eşlemesini içeren sınıf: örn. `PduSnmpOidMap` veya `SnmpPduStore`. GET/GETNEXT için “sonraki OID” bulma mantığı burada. |
| 3 | **PDU metrik üretici:** `IPduMetricGenerator` + `PduMetricGenerator` (sentetik voltage, current, power, temperature, outlet count/status). |
| 4 | **SNMP yanıt mantığı:** Gelen byte[] → ParseMessages; GET/GETNEXT için variable list oluştur; ResponseMessage oluştur; ToBytes → gönder. Ayrı sınıf olabilir (örn. `SnmpPduRequestHandler`). |
| 5 | **RunSnmpListenerAsync:** UDP dinleme döngüsü + yukarıdaki handler’ı çağırma; her SNMP cihazı için bir görev. |
| 6 | **SimulatorHostService.StartAsync:** SNMP cihazlarını listele; her biri için port = SnmpBasePort + index; dinleyiciyi başlat; port hata verirse _lastError ve rollback (HTTP’deki gibi). |
| 7 | **Stop:** SNMP dinleyicilerini de iptal et / kapat (ConcurrentDictionary ile CTS veya socket referansları). |
| 8 | **Status / README:** Aktif SNMP portlarını ve “SNMP cihazları çalışıyor” bilgisini gösterme; README’de SNMP test komutu (örn. `snmpget -v2c -c public localhost:11161 1.3.6.1.4.1.99999.1.1.2`) örneği. |

---

## 5. Test

- `snmpget -v2c -c public -p 11161 127.0.0.1 1.3.6.1.4.1.99999.1.1.2` → inputVoltage değeri dönmeli.
- `snmpwalk -v2c -c public -p 11161 127.0.0.1 1.3.6.1.4.1.99999` → Tüm PDU OID’leri listelenmeli.
- MngEngine’de SNMP collector (varsa) bu porta ve OID’lere yönlendirilerek gerçek pipeline ile test edilebilir.

---

## 6. Özet

- **PDU simülasyonu:** Sabit bir OID ağacı (1.3.6.1.4.1.99999.1.1.x) ile gerilim, akım, güç, sıcaklık, priz sayısı ve priz durumları.
- **Teknik:** SharpSnmpLib ile decode/encode, UDP listener ile cihaz başına bir port, GET/GETNEXT’e yanıt.
- **Metrikler:** PDU’ya özel bir metrik üretici ile her istekte güncel (hafif rastgele) değerler.
- **Entegrasyon:** SimulatorHostService’te SNMP cihazları için dinleyici başlatma/durdurma ve port çakışmasında hata raporlama.

Bu plan uygulandığında, “bir PDU cihazını SNMP ile simüle etmiş” olacağız; MngEngine bu cihaza bağlanıp aynı OID’lerden veri toplayabilir.
