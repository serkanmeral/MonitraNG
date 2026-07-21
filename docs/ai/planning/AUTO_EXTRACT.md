# DI Auto-Extract — Süreç ve Kararlar

**Tarih:** 16 Temmuz 2026  
**Durum:** Plan kilitlendi — altyapı kurulumu başlıyor  
**Kapsam:** Document Intelligence yapılandırılmış alan çıkarımı (fatura dikeyi) → ileride Workflow

---

## 1. Terminoloji (kilit)

| Terim | Anlam | Örnek çıktı |
|-------|--------|-------------|
| **Auto-Extract** | Belgeden şema’ya uygun **key-value** (ve satırlar) | `{ "payableAmount": 166730.4, "invoiceId": "…" }` |
| **Auto-Tag** | `dm_tags` kataloğundan keşif etiketleri | `["Fatura", "E-Arşiv"]` |
| **Özet** | İnsan okuma metni | serbest metin |
| **Çevrim** | Dil çevirisi | MngLLM translate |
| **RAG / full-text** | İçerik arama / soru-cevap | **MVP dışı** |

**Birincil ürün vaadi (bu hat):** Auto-Extract.  
Auto-Tag keşif için yararlı ama müşteri “fatura değerine göre flow” senaryosunu **karşılamaz**.

Keşif stratejisi: **etiket + metadata + ilişki** (binlerce dosyada full-text birincil kanal değil).

---

## 2. Hedef akış (uçtan uca)

```text
E-arşiv paketi (XML [+ PDF/HTML]) DI’ye eklenir
        │
        ▼
  Auto-Extract job
        │  ├─ tercihen UBL-TR XML parse (deterministik)
        │  └─ yoksa / yedek: LLM şema extract (7–8B)
        ▼
  Kalıcı extraction JSON (DI)
        │
        ▼
  Event: extraction.ready   ← Workflow fazı (sonra)
        │
        ▼
  Workflow If/Switch (örn. payableAmount > eşik)
        │
        ▼
  HTTP POST / WorkItem / bildirim
```

**Bugün odak:** extract altyapısı + şema + saklama sözleşmesi.  
**Sonra:** event → Workflow. GİB hesabı ile canlı çekim **kapsam dışı** (entegratör/dosya yükleme).

---

## 3. MVP dikeyi

| Karar | Değer |
|-------|--------|
| İlk şema | `earsiv_fatura` (genel “her belge extract” yok) |
| Birincil girdi | UBL-TR XML (`ProfileID` örn. `EARSIVFATURA`) |
| PDF (metin katmanlı) | Yedek: PdfPig metin → LLM → aynı şema (`source: llm_pdf`) |
| OCR / taranmış PDF / ZIP | Dışı |
| Kalemler (`lines[]`) | Opsiyonel (ilk flow’lar genelde toplam tutar) |

Şema detayı: [EARSIV_FATURA_SCHEMA.md](./EARSIV_FATURA_SCHEMA.md) · JSON: [../seeds/earsiv-fatura.schema.json](../seeds/earsiv-fatura.schema.json)

---

## 4. E-belge gerçekliği

- GİB **UBL-TR** ile veri modeli ailesi ortak; **PDF görünümü** firma/entegratöre göre değişir.
- Bu yüzden fatura MVP’sinde **XML parse birincil**, “her PDF aynı” varsayımı yok.
- Tip/profil çeşitliliği (`SATIS`, `IADE`, …) şemada alan olarak taşınır.

---

## 5. Platform yerleşimi

| Sorumluluk | Servis |
|------------|--------|
| Kaynak, yetki, dosya depolama | **MngDocument (DI)** |
| DI AI API (id ile): extract / özet / çeviri → **JSON response** | **MngLLM — DiAi controller** |
| İçerik okuma | MngLLM, `resourceId` ile Document’ten çeker (**file upload yok**) |
| Extract JSON’u DB’ye yazma + koşul/HTTP | **MngWorkflow** (sonraki faz) |

Detay: [DI_AI_API.md](./DI_AI_API.md)

Örnek:

```http
POST /llm/api/v1/di/extract
{ "resourceId": "…", "schema": "earsiv_fatura" }
→ 200 + earsiv_fatura JSON   (persist yok; DB yazımı Workflow’ta)
```

UBL mapper bu extract pipeline içinde çalışır; girdi XML Document’ten gelir.

---

## 6. LLM altyapısı (kilit)

| Konu | Karar |
|------|--------|
| Omurga | Ollama + MngLLM (değişmez) |
| Standart model | **qwen2.5:7b** (dev + prod aynı band) |
| Prod varsayım | **CPU** (GPU opsiyonel hız) |
| Concurrency | Düşük (1–2); async job |
| Embed / RAG | Ertelendi |
| Scorer (SIEM) | Ayrı şerit; bu pakette yok |

Detay: [INFRA_LLM.md](./INFRA_LLM.md)

**Not:** E-arşiv XML parse **LLM gerektirmez**; 7B özet/çeviri/PDF-only extract ve ilerideki işler için standarttır.

---

## 7. MVP DI AI paketi (revize)

| # | Yetenek | Öncelik |
|---|---------|---------|
| 1 | Auto-Extract (`earsiv_fatura`) | P0 |
| 2 | Özet | P1 |
| 3 | Çevrim | P1 (MngLLM’de kısmen var) |
| — | Auto-Tag | P2 (keşif; extract’ten türetilebilir) |
| — | RAG / full-text birincil arama | Dışı |
| — | GİB portal şifre ile çekim | Dışı |

---

## 8. Uygulama sırası

1. ✅ Kararlar + şema dokümanı (bu dosya + şema)  
2. ▶️ LLM altyapı: varsayılan model 7b, Ollama pull  
3. UBL XML → JSON mapper (MngDocument)  
4. Extract saklama + status alanları  
5. Extract API + seed/smoke  
6. Event + Workflow demo (müşteri flow)

Handoff: [DEVAM.md](./DEVAM.md)
