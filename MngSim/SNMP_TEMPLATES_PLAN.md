# SNMP Template Sistemi ve Cihaz Profil Sayfası

## 1. Özet

- **SNMP template:** PDU veya Router seçilebilir; her template farklı OID seti ve metrik üretimi.
- **Cihaz profil sayfası:** Her cihazın kendi sayfası; bilgiler (IP/port) + canlı metrikler, periyodik güncelleme.

---

## 2. SNMP Template Sistemi

### 2.1 Template Türleri

| Template | OID Base | Açıklama |
|----------|----------|----------|
| **Pdu** | 1.3.6.1.4.1.99999.1.1 | Güç dağıtım ünitesi: gerilim, akım, güç, sıcaklık, priz durumları |
| **Router** | 1.3.6.1.2.1 (MIB-II) | Ağ cihazı: sysDescr, sysUpTime, ifTable (interface sayacları) |

### 2.2 VirtualDevice Değişikliği

```csharp
// SNMP cihazlarda: "Pdu" | "Router"
public string? SnmpTemplate { get; set; } = "Pdu";
```

### 2.3 Router OID’leri (MIB-II benzeri)

- **sysDescr.0** (1.3.6.1.2.1.1.1.0): "MngSim Router - {device.Name}"
- **sysUpTime.0** (1.3.6.1.2.1.1.3.0): TimeTicks (sentetik)
- **sysContact.0**, **sysName.0**, **sysLocation.0**
- **ifNumber.0** (1.3.6.1.2.1.2.1.0): interface sayısı (2–4)
- **ifTable:** ifIndex, ifDescr, ifType, ifMtu, ifSpeed, ifAdminStatus, ifOperStatus, ifInOctets, ifOutOctets
- Her interface için artan sayaçlar (ifInOctets, ifOutOctets) — hafif rastgele artış

---

## 3. Cihaz Profil Sayfası

- **Route:** `/device/{id}`
- **İçerik:** Cihaz bilgileri (Id, Ad, Lokasyon, Protokol, Endpoint), canlı metrikler tablosu
- **Güncelleme:** 5 saniyede bir API’den metrik çekme (timer + InvokeAsync)

### 3.1 Endpoint Bilgisi

- HTTP: `http://localhost:{port}/metrics`
- SNMP: `udp://127.0.0.1:{port}` (community: public)

### 3.2 Metrik API

- `GET /api/device/{id}/info` → cihaz config + endpoint
- `GET /api/device/{id}/metrics` → anlık metrikler (generator’dan; HTTP/SNMP formatına göre)

---

## 4. UI İyileştirmeleri

1. Cihaz satırında **Profil** linki → `/device/{id}`
2. SNMP cihazlarda **Template** dropdown (Pdu / Router)
3. Profil sayfasında metrik kartları, periyodik yenileme
