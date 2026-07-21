# DI AI API — MngLLM Controllers

**Tarih:** 16 Temmuz 2026  
**Durum:** Mimari karar  
**İlgili:** [AUTO_EXTRACT.md](./AUTO_EXTRACT.md)

---

## Karar

DI yapay zeka işlemleri **MngLLM** içinde **ayrı bir controller grubu** olarak durur.

| Kural | Değer |
|-------|--------|
| Girdi | **DI resource `id`** (GUID / dataId) |
| Girdi değil | Multipart dosya, ham PDF/XML upload |
| Dosya içeriği | MngLLM, id ile **MngDocument’ten çeker** |
| LLM omurgası | Ollama (`qwen2.5:7b`) — extract’te XML varsa kullanılmayabilir |

```text
İstemci / job
    │  { resourceId }
    ▼
MngLLM  DiAiController   (örn. /api/v1/di/...)
    │
    ├─► MngDocument GET resource + content/xml
    ├─► UBL mapper / LLM (işleme göre)
    └─► sonuç (ve isteğe bağlı Document’e yazma)
```

---

## Önerilen route’lar (taslak)

Base (gateway): `/llm/api/v1/di/...`

| Method | Path | İş |
|--------|------|-----|
| `POST` | `/di/extract` | Auto-Extract (`schema`: `earsiv_fatura`) |
| `POST` | `/di/summarize` | Özet (P1) |
| `POST` | `/di/translate` | Çevrim (P1; mevcut translate’e yaslanabilir) |

### Extract body (örnek)

```json
{
  "resourceId": "…",
  "schema": "earsiv_fatura"
}
```

### Extract response

MngLLM **yalnızca JSON döner** (ör. `earsiv_fatura` şeması).  
**Şimdilik DI/DB’ye yazmaz.** Kalıcı kayıt ve sonraki adımlar **Workflow** sorumluluğunda olacak.

```text
MngLLM DiAi  →  200 + extraction JSON
                      │
                      ▼ (sonra)
                 Workflow  →  DB / HTTP / WorkItem …
```

---

## Sorumluluk sınırı

| Servis | Yapar |
|--------|--------|
| **MngLLM DiAi** | `resourceId` alır, Document’ten içerik çeker, extract/özet/çeviri üretir, **JSON döner** |
| **MngDocument** | Kaynak/yetki/depolama; content download API |
| **MngWorkflow** | Extract JSON’u işler, DB’ye yazar, koşul/HTTP (sonraki faz) |

**UBL mapper** extract pipeline’ında çalışır (XML byte → `earsiv_fatura` JSON). Dosya API’ye gelmez; Document’ten gelen XML üzerinde çalışır.

---

## Yetki

İstekteki JWT ile Document API çağrılır (forward).  
Kullanıcının o `resourceId` için okuma (ve `persist` ise yazma) yetkisi yoksa 403/404.

---

## Bilinçli olmayanlar

- MngLLM’e doğrudan file upload  
- Gateway’de DI id olmadan ham fatura POST  
- GİB’ten şifre ile çekim
