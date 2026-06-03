# Workflow AI Node — Extension Sözleşmesi (Taslak)

**Durum:** 📄 Sözleşme only — implementasyon **AI-3** (P1–P5 kapısı sonrası)  
**Son güncelleme:** 3 Haziran 2026  
**Kaynak:** [AI_PLANNING_DECISION.md §6 AI-3](../AI_PLANNING_DECISION.md) · [planing.md §19](./planing.md)

> Bu doküman kod yazmadan AI-ready hazırlığı tamamlar (checklist **R5**). Scorer / anomaly **bu dokümanın kapsamı dışındadır** → Alarm Engine §9.

---

## 1. Amaç

Workflow motoruna **opsiyonel** AI node'ları eklenmeden önce:

- Girdi/çıktı sözleşmesi sabitlenir
- MngLLM entegrasyonu mevcut node/registry desenine oturur
- Retry, timeout, yetki ve idempotency kuralları netleşir

**İlk node tipleri (AI-3):**

| `type` | Amaç |
|--------|------|
| `ai.summarize` | Event/alarm bağlamından operatör özeti |
| `ai.classify` | Sınıf/etiket üretimi → `If` ile dallanma |

---

## 2. Mimari sınır

```text
MngAlarm (tespit) → alarm event → Workflow Event Trigger
                                        ↓
                              ai.summarize / ai.classify
                                        ↓
                              MngLLM (istek bazlı, on-prem)
```

- AI node **tespit yapmaz**; yalnızca instance `ExecutionContext` içindeki veriyi işler.
- Scorer çıktısı (`kind=signal`) Alarm Engine'e girer; workflow'a **alarm event** olarak gelir.

---

## 3. Execution context — girdi sözleşmesi

Instance oluşturulurken (`StartFromVersionAsync`) zaten set edilir:

| Alan | Tip | AI node okur |
|------|-----|--------------|
| `event` | `Dictionary<string, object?>` | Tetikleyen payload (alarm `context`, OC event, manual data) |
| `variables` | `Dictionary<string, object?>` | Önceki node'ların yazdığı değişkenler |
| `outputs` | `Dictionary<string, object?>` | Node çıktıları (`nodeId` → dict) |

**R5 kuralı:** Event normalize edilirken **alan silinmez** (`WorkflowJsonNormalizer` tüm key'leri korur).

### Önerilen node config (taslak)

```json
{
  "id": "ai_summary_1",
  "type": "ai.summarize",
  "config": {
    "inputPaths": ["event.context", "event.severity", "variables.incidentId"],
    "outputVariable": "aiSummary",
    "promptTemplate": "Özetle: {{event}}",
    "maxTokens": 512,
    "language": "auto"
  }
}
```

```json
{
  "id": "ai_classify_1",
  "type": "ai.classify",
  "config": {
    "inputPaths": ["event", "variables.aiSummary"],
    "outputVariable": "aiClassification",
    "labels": ["false_positive", "investigate", "critical"],
    "allowUnknown": true
  }
}
```

---

## 4. Çıktı sözleşmesi

Başarılı çalışma sonrası:

1. `variables[outputVariable]` — birincil sonuç (string veya `{ label, confidence }`)
2. `outputs[nodeId]` — audit için tam yanıt:

```json
{
  "model": "…",
  "latencyMs": 1200,
  "tokenUsage": { "prompt": 400, "completion": 120 },
  "result": "…"
}
```

Sonraki node'lar Jint ile okur: `variables.aiClassification.label === 'critical'`.

---

## 5. MngLLM entegrasyonu

| Konu | Karar |
|------|-------|
| Transport | HTTP (`IHttpClientFactory`), workflow worker'dan |
| Auth | Keeper service token (mevcut `IWorkflowKeeperAuthClient` deseni) |
| Endpoint (taslak) | `POST /llm/api/v1/workflow/summarize`, `/classify` — veya genel `/chat` + sabit system prompt |
| Ayar | `MngWorkflowSettings.Llm.BaseUrl` (docker-compose env; Odak'ta boş = node devre dışı / skip) |
| Worker izolasyonu | LLM çağrısı **ayrı timeout**; execution engine core'a gömülmez |

---

## 6. Çalışma zamanı davranışı

| Konu | Karar |
|------|-------|
| **Timeout** | Varsayılan 90 sn (configurable); `NodeTimeoutSeconds`'dan bağımsız veya max(...) |
| **Retry** | 5xx / timeout → mevcut retry bucket; 4xx (bad prompt) → non-retryable |
| **Idempotency** | Aynı `(instanceId, nodeId, attempt)` başarılıysa skip (mevcut engine davranışı). Attempt > 1 için opsiyonel LLM cache key: `{instanceId}:{nodeId}` — AI-3 implementasyonunda |
| **Waiting** | Hayır — senkron node; çok uzun özetler için ileride async AI job + resume (backlog) |
| **Devre dışı LLM** | `Llm.Enabled=false` → node Fail (non-retryable) veya config `onMissingLlm: "skip"` (tasarım kararı AI-3'te) |

---

## 7. Yetki ve veri sızıntısı (R9)

- Service token domain-scoped; LLM isteğinde `X-Domain-Name` zorunlu.
- `inputPaths` yalnızca instance context'ten okunur — dış URL'den veri çekilmez.
- DI RAG entegrasyonu **AI-2** — ayrı node veya summarize `source: "rag"` flag (sonra).

---

## 8. UI / tanım editörü

AI-3 öncesi UI zorunlu değil. Minimum: JSON config ile dev/test.  
Editör Faz 5+ — node palette'e `ai.summarize`, `ai.classify` eklenir.

---

## 9. Implementasyon checklist (AI-3)

- [ ] `IWorkflowLlmClient` + settings
- [ ] `AiSummarizeNode`, `AiClassifyNode` → `INodeRegistry`
- [ ] Unit test: mock LLM, context path okuma
- [ ] E2E: alarm event → summarize → approval → log
- [ ] docker-compose: `MngWorkflowSettings__Llm__*` env
- [ ] DEVAM.md + AI_PLANNING AI-3 ✅

---

## 10. Şimdi yapılacaklar (kod yok)

| # | Madde | Sahip |
|---|-------|-------|
| 1 | Event payload'da alarm `context` alanlarının workflow'a eksiksiz aktarımı — code review | ✅ mevcut `event` = full triggerData |
| 2 | Bu sözleşme dokümanı | ✅ |
| 3 | Deploy Alarm Faz 2 + lifecycle E2E | Deploy SSH kullanıcı |
| 4 | P4 kapatma: üretim benzeri alarm→onay senaryosu | Sonraki sprint |

---

## 11. Referanslar

- [AI_PLANNING_DECISION.md](../AI_PLANNING_DECISION.md)
- [Workflow DEVAM.md](./DEVAM.md) §12.6
- [ALARM_RULE_ENGINE_PLAN.md §8](../alarm/ALARM_RULE_ENGINE_PLAN.md) — alarm event şeması
