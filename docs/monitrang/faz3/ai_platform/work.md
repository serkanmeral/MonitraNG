# AI Platform — Work

**Son güncelleme:** 13 Temmuz 2026  
**Durum:** Altyapı (CPU-first) kilitlendi; implementasyon başlamadı

---

## Nerede kaldık

CPU-first altyapı ve model tier’ları Roadmap §5’e yazıldı. Sonraki adım AI-0 envanter + embed smoke.

## Bu oturumda yapılanlar

- [x] Kapsam / on-prem / vektör kararları  
- [x] `Roadmap.md` + indeks linkleri  
- [x] **CPU-first kilit:** GPU yok varsayımı; müşteri GPU opsiyonel  
- [x] Model: sync `qwen2.5:3b` · async `qwen2.5:7b` · embed `nomic-embed-text`  
- [x] Sync/async UX kuralı + sunucu boyut tavsiyesi  
- [x] Prod AI RAM: **minimum 16 GB**, önerilen **24–32 GB** (Roadmap §5.5)  
- [ ] AI-0 envanter (MngLLM API + compose + CPU smoke)

## Sıradaki

1. **AI-0** — Endpoint/compose envanteri; `3b`/`7b`/embed pull + kısa CPU latency ölçümü  
2. AI-1 API sözleşmesi (`summarize`/`tag` + `tier: light|heavy`)  
3. Async job şeması (AI-2) — ağır yolun omurgası  
4. DI Roadmap ile `dm_resource_ai` hizası

## Blocker

- Yok (kod için açık “kodla” talebi gerekir)

## Commit / deploy

| Tarih | Commit | Not |
|:---|:---|:---|
| — | — | — |
