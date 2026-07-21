# AI Platform — Roadmap (Faz 3)

**Kapsam:** On-prem AI omurgası (MngLLM + Ollama) + ortak API + async job + vektör (MongoDB MVP) + tüketici sözleşmeleri  
**Klasör:** `docs/monitrang/faz3/ai_platform/`  
**Öncelik:** P1 (çapraz — DI / Monitoring / Üretim AI’nın backend’i)  
**Durum:** Planlama  
**Son güncelleme:** 13 Temmuz 2026

---

## 1. Amaç

Odak Kompozit teklifindeki AI vaatlerini karşılayacak **platform AI omurgasını** kurmak. Ürün özellikleri (DI RAG UI, Monitoring anomaly paneli vb.) tüketici paketlerinde kalır; bu roadmap **ortak backend** ve sözleşmeler içindir.

**İlkeler (Odak AI kararları ile uyumlu):**

| # | İlke |
|:--:|:---|
| K1 | İki şerit: scorer/anomaly (sinyal) vs copilot/LLM (özet, RAG, çeviri) |
| K2 | AI sıcak ingest yolunu bloklamaz — uzun işler async |
| K3 | On-prem / offline model (Ollama); cloud-only bağımlılık yok |
| K4 | AI çıktısı öneri / sinyal; kritik aksiyon insan veya workflow onayı |
| K5 | Yetki: tüketici katmanı kullanıcının göremediği veriyi LLM’e vermez |
| K6 | **CPU-first:** test/prod GPU yok varsayılır; GPU müşteri opsiyonel hızlandırma (aynı API) |

---

## 2. Kapsam sınırı

| Dahil | Bilinçli ertelenen (bu faz dışı / sonra) |
|:---|:---|
| Summarize, tag, embed, translate (genişletme) | DG NLQ |
| Text extract worker | Moni chatbot olgunlaştırma |
| Async job + status | SIEM AI scorer |
| MongoDB vektör saklama + similarity MVP | Erken Qdrant zorunluluğu |
| DI / Monitoring / Production **sözleşmeleri** | Tüketici UI’nin tamamı (paket Roadmap’lerinde) |

Tüketici detay: [../document_intelligence/Roadmap.md](../document_intelligence/Roadmap.md), [../monitoring/Roadmap.md](../monitoring/Roadmap.md), [../production_operations/Roadmap.md](../production_operations/Roadmap.md).

---

## 3. Mevcut durum (kabaca)

| Parça | Durum |
|:---|:---|
| MngLLM + çeviri API | ✅ |
| Ollama compose / gateway `/llm` | ✅ (ortama göre) |
| Chatbot (Moni) planlama | Kısmi / ayrı track |
| DI `dm_resource_ai` + extract→embed pipeline | Tasarım var, ürün yok |
| Ortak summarize/tag/embed API | Yok |
| Monitoring anomaly motoru | Yok (istatistiksel + LLM açıklama planlı) |

Referans: `docs/content/MngLLM/`, `docs/odak/document_intelligence/DI_PRODUCT_ROADMAP.md` §17, `docs/odak/AI_PLANNING_DECISION.md`.

---

## 4. Mimari (hedef)

```text
Tüketiciler: DI · Monitoring · Production · (ileride diğer)
        │
        ▼
   MngLLM API  (auth, quota, audit)
        │
   ┌────┴────┬────────────┐
   ▼         ▼            ▼
 Ollama   Extract/   Embedding store
 (LLM +   async      (MongoDB MVP;
  embed)  worker      IEmbeddingStore)
```

**Vektör kararı (bu faz):** MongoDB’de embedding saklama; benzerlik için yetki/domain filtresi + aday setinde cosine (veya mevcut Mongo yetenekleri). Arayüz soyut (`IEmbeddingStore`) — ileride Qdrant’a taşıma opsiyonu.

---

## 5. Altyapı ve performans (kilitli — CPU-first)

### 5.1 Ortam varsayımları

| Ortam | GPU | Davranış |
|:---|:---|:---|
| Local / test / bizim prod | **Yok** (hiç olmayabilir) | Tüm tasarım ve kabul **CPU** üzerinde |
| Müşteri ortamı | Tavsiye edilir, **zorunlu değil** | Aynı API; GPU varsa Ollama hızlanır, kod yolu değişmez |

> Ürün “GPU şart” demez. Teklif/doküman notu: *GPU opsiyonel hızlandırma; CPU’da çalışır, etkileşim süresi uzayabilir.*

### 5.2 Model seti

| Rol | Model | Kullanım |
|:---|:---|:---|
| **Sync / hafif (varsayılan LLM)** | `qwen2.5:3b` | Kısa özet, tag, alarm açıklaması, kısa çeviri, sınırlı RAG cevabı |
| **Async / kalite** | `qwen2.5:7b` | Uzun özet, diff, tutarsızlık, zengin RAG, toplu iş |
| **Embed** | `nomic-embed-text` (birincil aday; AI-0’da smoke) | Benzerlik + RAG retrieval |
| **Opsiyon (müşteri GPU veya güçlü CPU)** | `qwen2.5:14b` | Config ile; varsayılan değil |

Mevcut MngLLM default (`qwen2.5:3b`) **korunur** — sync yolu buna oturur.

### 5.3 Sync vs async (prod UX)

CPU’da uzun senkron generate UX’i bozar. Kural:

| Mod | Ne zaman | SLA hedefi (CPU, kabaca) | Model |
|:---|:---|:---|:---|
| **Sync** | Kısa prompt, tek çıktı, kullanıcı bekliyor | **≤ 15–20 sn** (sert timeout); aşarsa job veya “devam ediyor” | `3b` |
| **Async (varsayılan ağır iş)** | Upload pipeline, uzun doküman, çok-chunk RAG, toplu tag/özet | Dakika ölçeği OK; status + bildirim | `7b` (yoksa `3b`) |
| **Retrieval-only** | Benzer dosya (embed + cosine) | **≤ 2–3 sn** (LLM yok) | embed |

**Senkron RAG (CPU):** (1) Embed + top-k sync · (2) Cevap: kısa context + `3b` sync **veya** async `7b`.  
**Ürün varsayılanı:** ağır RAG cevabı **async**; hafif kısa cevap sync `3b`.

### 5.4 Çalışma zamanı

| Madde | Karar |
|:---|:---|
| Concurrent generate | CPU’da **1** (kuyruk); paralel generate yok |
| Keep-alive | Model RAM’de (`keep_alive` uzun); cold start azalt |
| Prompt bütçesi | Sync: sıkı token limiti |
| Timeout | Sync 30–45 sn; async 120 sn+ |
| Quota | Tenant/kullanıcı rate limit; soft-cap: 1 ağır job |
| İzolasyon | Mümkünse Ollama ayrı VM/host |
| Anomaly | İstatistiksel CPU; LLM sadece kısa açıklama |

### 5.5 Sunucu boyut (GPU’suz tavsiye)

| Profil | RAM | CPU | Not |
|:---|:---|:---|:---|
| Minimum (test / düşük yük) | 8–12 GB | 4 vCPU | Çoğunlukla `3b` + embed; `7b` riskli |
| **Prod AI minimum** | **16 GB** | **8 vCPU** | `7b` + embed + OS; `3b` swap ile. Teorik ~12 GB yetebilir ama prod’da sıkışır |
| **Önerilen prod (AI host)** | **24–32 GB** | **8+ vCPU** | `3b` + `7b` warm + embed + rahat kuyruk |
| Rahat | 48 GB+ | 12+ vCPU | Büyük context / daha uzun kuyruk |
| App + AI aynı kutu | **32 GB+** (tüm makine) | 8+ vCPU | Mongo/app stack üzerine AI payı eklenir |

**Kabaca model payı (Q4, yüklü):** `7b` ~5–6 GB · `3b` ~2–3 GB · embed &lt;1 GB · Ollama+OS ~2–3 GB.

Disk modeller: ~5–10 GB. Müşteri GPU opsiyonu: 8 GB+ VRAM → aynı API, daha hızlı sync.

> Teklif / müşteri notu: **Prod AI host minimum 16 GB RAM**; önerilen **24–32 GB**. GPU zorunlu değil.

### 5.6 Mimari sonuç

```text
İstek → MngLLM
         ├─ light/sync  → Ollama qwen2.5:3b
         ├─ heavy/async → job → Ollama qwen2.5:7b
         └─ embed       → nomic-embed-text → Mongo IEmbeddingStore
```

Model adları config; **tier (light / heavy / embed) sabit**.

---

## 6. Fazlar

| Faz | Hedef | Öncelik |
|:---|:---|:---:|
| **AI-0** | Envanter + CPU-first doğrulama; embed adayı smoke; eksik API listesi | P0 |
| **AI-1** | `summarize` / `tag` (+ `translate` cilası); sync=`3b`, async model seçimi | P0 |
| **AI-2** | Text extract + normalize; **async job + status (ağır yol)** | P0 |
| **AI-3** | Embed API + Mongo persistence + similarity | P0 |
| **AI-4** | Consumer: DI; Monitoring anomaly (istatistiksel + kısa summarize) | P1 |
| **AI-5** | RAG: retrieval sync + cevap **async-first** (kısa sync opsiyon) | P1 |
| **AI-E** | NLQ, Moni, SIEM scorer, Qdrant, 14b varsayılan, OCR derinliği | — |

### Teklif AI ihtiyacı eşlemesi

| Teklif ihtiyacı | Omurga fazı |
|:---|:---|
| Otomatik etiket / özet | AI-1 + AI-2 |
| Benzer dosya / semantik | AI-3 |
| RAG soru–cevap | AI-3 + AI-5 (cevap async-first) |
| Diff / tutarsızlık / varlık / klasör önerisi | AI-1/2 + DI kuralları |
| Çeviri / dil varyantı | translate + AI-1 (`3b` sync) |
| Anomaly + alarm açıklaması | AI-4 |
| Upload’u bloklamama | AI-2 |

---

## 7. Bağımlılıklar

- Ollama (CPU-first; model pull: `3b` + `7b` + embed)  
- MngScheduler veya eşdeğer queue (async — **zorunlu omurga**)  
- DG / MinIO (extract için binary)  
- Migration → [../MIGRATION.md](../MIGRATION.md)

## 8. Kabul (özet)

- On-prem + **GPU’suz** smoke geçer  
- Ağır işler async + status; sync yalnızca light tier  
- Embedding Mongo’da; similarity yetki sınırında  
- DI/Monitoring en az birer smoke (AI-4 sonrası)  
- GPU yokken “çalışmıyor” kabulü **yok** — süre uzaması beklenen davranış
---

İş takibi: [work.md](./work.md)
