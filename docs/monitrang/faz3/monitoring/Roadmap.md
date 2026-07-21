# Monitoring — Roadmap (Faz 3)

**Teklif:** §4.3 İzleme  
**Klasör:** `docs/monitrang/faz3/monitoring/`  
**Durum:** Planlama iskeleti  
**Son güncelleme:** 13 Temmuz 2026

---

## 1. Amaç

Operasyonel / metrik Monitoring: sunucu, DB, servis, HTTP/ping, MQTT/SNMP/TCP/OPC UA, kamera alarmı, dashboard, eşik alarm, bildirim, Monitoring AI (anomaly vb.).

**Dahil değil:** SIEM / güvenlik olay katmanı.

## 2. Kapsam özeti (tekliften)

| Alan | Madde |
|:---|:---|
| Host | Windows / Linux metrik |
| DB | MongoDB, Oracle, SQL Server, PostgreSQL |
| Servis | Windows Service, systemd, process |
| Ağ | HTTP health, ping |
| Saha | MQTT, SNMP, TCP, OPC UA |
| Kamera | Alarm yakalama (protokol keşifte) |
| AI | Anomaly, açıklama, gürültü azaltma, NL sorgu, trend, özet, eşik önerisi |
| Demo | Sensör yoksa simulator |

## 3. Mevcut / yeni

| | Durum (kabaca) |
|:---|:---|
| Engine / Reactor / mon_* | Platform planı var; teklif kapsamına göre collector olgunluğu |
| Anomaly + Monitoring AI | Büyük ölçüde **yeni** — omurga: [../ai_platform/Roadmap.md](../ai_platform/Roadmap.md) |
| OPC UA / kamera | Bağlayıcı + keşif |

## 4. Fazlar (taslak)

| Faz | Hedef |
|:---|:---|
| MON-0 | Gap: monitoring_plans ↔ teklif |
| MON-1 | Host + ping/HTTP + servis |
| MON-2 | DB metrikleri (4 motor) |
| MON-3 | MQTT/SNMP/TCP/OPC UA |
| MON-4 | Kamera alarm + simulator senaryosu |
| MON-5 | Alarm/dashboard/Telegram |
| MON-6 | Monitoring AI (anomaly önce) |

## 5. Bağımlılıklar

- MngEngine, MngReactor, alarm/notifier, UI  
- Üretim Operasyonu bu paketin asset’lerine yaslanır  
- [../MIGRATION.md](../MIGRATION.md)

## 6. Kabul (özet)

Erişilemeyen kaynaktan izleme yok; SIEM yok; simulator gerçek cihaz yerine geçmez.

---

İş takibi: [work.md](./work.md)
