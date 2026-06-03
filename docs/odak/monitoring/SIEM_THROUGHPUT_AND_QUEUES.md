# SIEM — Kuyruk, Paralellik ve Yoğun Veri Yönetimi

**Durum:** Taslak — planlama (implementasyon workflow sonrası)
**Son güncelleme:** 3 Haziran 2026
**İlişki:** [SIEM_PLANNING.md](./SIEM_PLANNING.md) §15, [ALARM_RULE_ENGINE_PLAN.md](../alarm/ALARM_RULE_ENGINE_PLAN.md) §6, [MONITORING_ENGINE_ARCHITECTURE.md](../../content/monitoring_plans/MONITORING_ENGINE_ARCHITECTURE.md) §8

---

## 1. Sorunun özü

SIEM verisi (özellikle **firewall syslog**) metrik izlemeye göre **çok daha hacimli ve düzensiz** gelir:

| Kaynak | Tipik hacim | Burst |
|--------|-------------|-------|
| Metrik (SNMP/WMI) | Düşük, periyodik | Öngörülebilir |
| Firewall syslog | Yüksek (allow+log açıksa çok yüksek) | DDoS, tarama anında patlama |
| AD Security (dar filtre) | Orta | Login spike |
| Linux auth | Düşük–orta | Brute-force anında |

Plan: **katman katman** backpressure, filtreleme ve paralellik; tek noktada sınırsız buffer yok.

---

## 2. Uçtan uca akış (hedef mimari)

```text
[Kaynaklar]  push (syslog/WEF/agent)
      │
      ▼
┌─────────────────────────────────────┐
│  MngEngine (edge)                    │
│  • Listener thread pool              │
│  • SecEvent in-memory queue          │
│  • Batch builder + send job          │
└──────────────┬──────────────────────┘
               │ HTTP ingest (batch)
               ▼
┌─────────────────────────────────────┐
│  MngReactor                          │
│  • Parse (CPU)                       │
│  • Mongo sec_events bulk write       │
│  • MQ: sec_events batch/summary      │
└──────────────┬──────────────────────┘
               │ RabbitMQ stream
               ▼
┌─────────────────────────────────────┐
│  Alarm & Rule Engine                 │
│  • Partitioned consumers           │
│  • In-memory window state            │
│  • Checkpoint (Mongo)                │
└──────────────┬──────────────────────┘
               │ alarm events
               ▼
         MngWorkflow (sonra)
```

---

## 3. Katman 1 — MngEngine (topla + tampon)

### 3.1 Mevcut metrik modeli (referans)

Monitoring Engine bugün:
- Collector → **in-memory queue** (`MetricBatchQueue`, `MaxBatches`)
- Ayrı **send job** (cron) → HTTP Reactor
- Gönderim hatası veya limit aşımı → **eski veri atılır** (RPi / basitlik)

### 3.2 SIEM için fark

Syslog **push** gelir; collector job yok. Listener sürekli dinler.

**Öneri — ayrı kuyruk:** `SecEventQueue` (metrik kuyruğundan **ayrı** limit ve politika)

| Parametre | Metrik queue | SecEvent queue (öneri) |
|-----------|--------------|------------------------|
| `MaxItems` / `MaxBatches` | Düşük (RPi) | Müşteri profiline göre daha yüksek |
| Overflow | En eskiyi at | **Yapılandırılabilir:** drop oldest **veya** sample (1/N) **veya** deny-only filtresi |
| Offline buffer | Yok (disk) | Faz 2: opsiyonel disk spool (yüksek hacim müşteri) |
| Send interval | 1–5 dk | **Daha sık** (30s–1m) veya eşik (N olay / M MB) |

### 3.3 Listener paralelliği

| Bileşen | Paralellik |
|---------|------------|
| UDP syslog | Tek socket; alım hızlı → internal **Channel** (producer/consumer) |
| TCP syslog | **Connection başına** veya sınırlı thread pool |
| WEC / Event okuma | Ayrı task; batch'lenip aynı kuyruğa |
| Send job | Tek thread (sıralı HTTP); çok Engine instance ise her biri kendi kuyruğu |

**Ölçek:** Birden fazla **Engine** instance — kaynak bazlı bölme (ör. FW-A → Engine-1, FW-B + WEC → Engine-2).

### 3.4 Edge filtreleme (hacim düşürme)

Engine'de **ham syslog'un tamamını** göndermek zorunlu değil; spike öncesi:

| Filtre | Etki |
|--------|------|
| Sadece **deny** + **config change** (firewall) | Allow log hacmini %90+ düşürür |
| Rate limit kaynak IP başına | Flood sırasında koruma |
| Max message size | Bozuk/dev syslog satırı |

Filtre kuralları config sync ile Reactor'dan veya Engine local policy.

---

## 4. Katman 2 — MngReactor (ingest + parse)

### 4.1 Ingest modeli

**Faz 1 (mevcut monitoring ile uyumlu):** Senkron HTTP — istek içinde batch işlenir, yanıt `savedCount` / `failedCount`.

**Yoğunluk artınca (Faz 1.5+):**

| Seçenek | Artı | Eksi |
|---------|------|------|
| **A)** HTTP kalır, Reactor scale-out (LB) | Basit | Parse CPU sınırı |
| **B)** Engine → RabbitMQ `sec_events.raw` → Reactor worker | Decouple, buffer | Yeni altyapı |
| **C)** Müşteri rsyslog relay → rate limit → Engine | Edge yük dağıtımı | Ek sunucu |

**Öneri:** Faz 1 **A**; hacim testi sonrası **B** veya **C** (SIEM §21.5 ile birlikte).

### 4.2 Reactor içi paralellik

```
HTTP request
  → decrypt/decompress
  → batch split
  → [parallel] parser per item (CPU bound, sınırlı DOP)
  → bulk Mongo insert (ordered=false, chunk 500–1000)
  → MQ publish (batch summary — aşağıda)
  → response
```

**Parser:** Stateless — **paralel** parse güvenli.

**Mongo yazım:** **BulkWrite** chunk'ları; partial failure kabul (monitoring ile aynı).

### 4.3 RabbitMQ — kritik tasarım kararı

Monitoring bugün: **metrik başına** MQ mesajı (workflow notify için).

Firewall hacminde **sec_events başına 1 MQ mesajı yapılmamalı.**

| Strateji | Ne zaman |
|----------|----------|
| **Batch notification** | "Son 1 sn'de domain X'e N olay yazıldı" + opsiyonel örnek |
| **Filtered publish** | Sadece `event.outcome=failure` veya `action in (...)` |
| **Alarm engine pull** | MQ yerine change stream / polling (Faz 2 alternatif) |

**Öneri:** Alarm engine tüketimi için **`sec_events.ingested` batch event** veya Mongo **Change Stream** (hacim testine göre).

---

## 5. Katman 3 — Alarm & Rule Engine (stateful)

ALARM_RULE_ENGINE_PLAN §6 ile uyumlu:

| Konu | Yaklaşım |
|------|----------|
| **Correlation state** | Bellekte kayan pencere; `(ruleId, groupBy hash)` başına sayaç |
| **Paralellik** | **Partition zorunlu** — aynı `groupBy` anahtarı aynı worker (consistent hash) |
| **Restart** | Checkpoint Mongo |
| **Geç gelen olay** | `@timestamp` (event time) vs `ingestedAt`; sınırlı lateness (ör. 2 dk) |
| **Cooldown / dedup** | Alarm fırtınası önleme |

**Yoğun firewall + U4 (deny sayımı):** Threshold kuralları stateless veya düşük cardinality groupBy (`dstIp`, `srcIp`) — worker sayısı = partition sayısı.

**Brute-force U1:** Düşük hacim (4625) — correlation rahat.

---

## 6. Yoğunluk senaryoları ve tepkiler

| Senaryo | Belirti | Tepki (katman sırasıyla) |
|---------|---------|---------------------------|
| **Normal** | < X evt/s | Varsayılan pipeline |
| **Firewall allow-log açık** | Queue dolar | Edge: deny-only filtresi; müşteri config |
| **DDoS / scan burst** | UDP loss, queue overflow | Engine: sample/drop + rate limit; alarm U5 (eşik) |
| **Reactor yavaş** | HTTP 503/timeout | Engine: queue overflow politikası; scale Reactor |
| **Mongo yazım limiti** | Bulk slow | Chunk küçült; indeks gözden geçir; OpenSearch (§21.7) |
| **Alarm CPU** | Lag artar | Partition worker ekle; sadece failure olayları stream |

---

## 7. Kapasite planlama (kabaca)

Müşteri envanter §20 **günlük GB/gün** sorusu bu hesap için.

| Adım | Formül / not |
|------|----------------|
| Ham syslog EPS | Müşteri / firewall dokümantasyonu |
| Filtre sonrası | allow kapalı → ~%5–20 kalır (tahmini) |
| Ortalama olay boyutu | ~0.5–2 KB `sec_events` belge |
| Engine queue | `MaxItems` ≥ burst süresi × EPS (ör. 60s × 1000 eps = 60k) |
| Mongo | İndeks + retention; Time Series değil, düz koleksiyon |
| MQ | Batch/summary; EPS × 1 mesaj **değil** |

**Spike (Faz 1 sonrası):** Sentetik syslog flood (MngSim veya `hping` değil — log generator) ile Engine queue + Reactor latency ölçümü.

---

## 8. Paralellik özeti

| Bileşen | Paralel mi? | Nasıl? |
|---------|:-----------:|--------|
| Syslog UDP alım | Kısmen | Channel + parser thread pool |
| Engine → Reactor send | Hayır (sıralı batch) | Çok instance ile yatay |
| Reactor parse | Evet | Parallel foreach (sınırlı) |
| Mongo write | Evet | Bulk chunk paralel (dikkatli) |
| MQ publish | Evet | Batch mesaj |
| Alarm correlation | Evet | **Partitioned** workers (groupBy hash) |
| Workflow | Sonra | Event trigger async |

---

## 9. Açık kararlar (throughput)

| # | Karar | Öneri |
|---|-------|-------|
| T1 | Engine ayrı `SecEventQueue` | Evet |
| T2 | Overflow: drop vs sample | Drop oldest + metric; prod'da sample seçeneği |
| T3 | MQ: olay başına vs batch | **Batch / filtered** |
| T4 | Yüksek hacim ingest | HTTP → sonra RabbitMQ raw queue |
| T5 | OpenSearch | Spike sonrası (§21.7) |
| T6 | Disk spool Engine | Faz 2, sadece enterprise profil |

---

## 10. Referanslar

- [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) — ilk spike bilinçli olarak düşük hacim
- [SIEM_PLANNING.md](./SIEM_PLANNING.md) §21.5, §21.7
- [ALARM_RULE_ENGINE_PLAN.md](../alarm/ALARM_RULE_ENGINE_PLAN.md) §6
- [MONITORING_ENGINE_ARCHITECTURE.md](../../content/monitoring_plans/MONITORING_ENGINE_ARCHITECTURE.md) §8
