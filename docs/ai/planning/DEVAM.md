# AI / Auto-Extract — DEVAM

**Son güncelleme:** 16 Temmuz 2026

## Nerede kaldık?

Auto-Tag yerine **Auto-Extract** birincil vaat olarak kilitlendi.  
MVP dikeyi: **`earsiv_fatura`** (UBL XML parse → JSON).  
LLM standardı: **`qwen2.5:7b`**. Workflow bağlantısı extract sonrasına bırakıldı.

## Tamamlanan

- [x] `docs/ai` çalışma alanı
- [x] Süreç dokümanı: [AUTO_EXTRACT.md](./AUTO_EXTRACT.md)
- [x] Şema: [EARSIV_FATURA_SCHEMA.md](./EARSIV_FATURA_SCHEMA.md) + JSON seed
- [x] LLM infra kararları: [INFRA_LLM.md](./INFRA_LLM.md)
- [x] Varsayılan model `qwen2.5:7b` (appsettings + docker-compose)
- [x] Ollama pull `qwen2.5:7b` + `mngllm` recreate (lokal Docker)

## Sıradaki

1. ~~MngDocument: UBL XML → `earsiv_fatura` mapper~~ → **MngLLM DiAi + UBL mapper** (JSON only)
2. Extract API smoke (DI’ye XML yükle → `POST /llm/api/v1/di/extract`)
3. Workflow’un JSON’u DB’ye yazması (sonra)

## Kod (16 Tem 2026)

- `POST /api/v1/di/extract` → `{ resourceId, schema }` → `earsiv_fatura` JSON
- UBL mapper (namespace-agnostic) + unit test ✅
- Document metadata + DG file download (JWT forward)
- Persist yok
- Lokal Docker: `mngllm` rebuild/recreate

## Bilinçli dışı

- RAG / birincil full-text  
- OCR  
- GİB şifre ile canlı çekim  
- Genel “her belge” extract şeması
