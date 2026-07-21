# LLM Altyapısı — Kurulum Kararları

**Tarih:** 16 Temmuz 2026  
**İlgili:** [AUTO_EXTRACT.md](./AUTO_EXTRACT.md)

---

## Kararlar

| # | Karar |
|---|--------|
| 1 | Omurga: **Ollama + MngLLM** |
| 2 | Standart generative model: **`qwen2.5:7b`** (dev ve prod aynı band) |
| 3 | Prod varsayım: **CPU**; GPU varsa Ollama kullanır (kod değişmez) |
| 4 | Async job + düşük concurrency; sıcak ingest’i bloklama |
| 5 | Embedding / RAG modeli: ertelendi |
| 6 | E-arşiv Auto-Extract **birincil yolu parse**; LLM yedek / özet / çeviri |

---

## Yapılandırma (repo)

| Yer | Alan | Değer |
|-----|------|--------|
| `MngLLM/.../appsettings.json` | `Ollama:DefaultModel` | `qwen2.5:7b` |
| `ApplicationResources/mng_apps/docker-compose.yml` | `MngLLMSettings__Ollama__DefaultModel` | `qwen2.5:7b` |
| Timeout | 120s (CPU için artırılabilir) | |

Production compose model adını env ile override edebilir; varsayılan appsettings/image ile uyumlu tutulur.

---

## Lokal kurulum adımları

```powershell
# Ollama container çalışıyorken:
docker exec ollama ollama pull qwen2.5:7b

# Kontrol:
docker exec ollama ollama list
```

Script: [../seeds/pull-ollama-model.ps1](../seeds/pull-ollama-model.ps1)

**RAM:** 7b Q4 için kabaca 5–6 GB model + overhead; Docker Desktop’ta diğer servislerle birlikte planlayın.

**Odak:** RAM kısıtlı ortamlarda Ollama kapalı kalabilir; AI host ayrı tutulabilir.

---

## MngLLM yeniden başlatma (local docker)

Model env değişince `mngllm` recreate:

```powershell
cd ApplicationResources/mng_apps
docker compose up -d mngllm --force-recreate
```

(Kullanıcı kuralı: backend docker deploy bu oturumlarda yapılabilir.)
