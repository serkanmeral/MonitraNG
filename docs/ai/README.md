# AI — Çalışma Alanı

**Amaç:** Platform yapay zeka ve DI Auto-Extract planlama / seed / infra.  
**Başlangıç:** 16 Temmuz 2026  
**Aktif odak:** **Auto-Extract** (`earsiv_fatura`) + LLM omurgası (`qwen2.5:7b`)

> Workflow (fatura değeri → koşul → HTTP) extract köprüsünden **sonra**.  
> Workflow alanı: [docs/workflow/](../workflow/README.md)

---

## Hızlı giriş

| Doküman | İçerik |
|---------|--------|
| [planning/AUTO_EXTRACT.md](./planning/AUTO_EXTRACT.md) | Süreç, terminoloji, MVP, sıra |
| [planning/EARSIV_FATURA_SCHEMA.md](./planning/EARSIV_FATURA_SCHEMA.md) | Fatura alan sözleşmesi |
| [planning/INFRA_LLM.md](./planning/INFRA_LLM.md) | Ollama / 7B kurulum |
| [planning/DEVAM.md](./planning/DEVAM.md) | Nerede kaldık |
| [seeds/earsiv-fatura.schema.json](./seeds/earsiv-fatura.schema.json) | JSON Schema |
| [seeds/earsiv-fatura.example.json](./seeds/earsiv-fatura.example.json) | Örnek extract çıktısı |

---

## Klasör yapısı

```text
docs/ai/
├── README.md
├── planning/
├── seeds/
└── history/
```

---

## Kilit kararlar (özet)

1. **Auto-Extract** = key-value şema; **Auto-Tag** ≠ extract  
2. MVP dikeyi: e-arşiv/UBL XML parse  
3. Keşif: tag/metadata/link — birincil full-text yok  
4. LLM: `qwen2.5:7b`, CPU-first prod  
5. GİB şifre ile çekim yok  

---

## Mevcut platform referansları

| Dosya | Rol |
|-------|-----|
| [docs/odak/AI_PLANNING_DECISION.md](../odak/AI_PLANNING_DECISION.md) | Scorer vs copilot, on-prem |
| [docs/monitrang/faz3/ai_platform/Roadmap.md](../monitrang/faz3/ai_platform/Roadmap.md) | Faz 3 AI roadmap |
| [docs/content/MngLLM/](../content/MngLLM/) | MngLLM teknik |
| [docs/odak/document_intelligence/](../odak/document_intelligence/) | DI handoff / plan |
| [docs/odak/workflow/DEVAM.md](../odak/workflow/DEVAM.md) | Workflow (sonra bağlanacak) |
