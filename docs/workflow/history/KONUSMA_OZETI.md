# Workflow — Geçmiş Konuşma Özeti

**Oluşturma:** 16 Temmuz 2026  
**Amaç:** Eski chat oturumlarındaki karar ve evrimi tek yerde toparlamak. Kaynak: Cursor agent transcript’leri + repodaki plan dosyaları.

> Transcript kimlikleri yalnızca izlenebilirlik içindir; sohbet linkleri: ilgili UUID ile agent transcripts.

---

## 1. Zaman çizelgesi

| Dönem | Konu | Sonuç / artefakt |
|-------|------|------------------|
| **Şubat 2026** | IFTTT vs mini Node-RED tartışması | Hibrit niyet → önce **MngRules**; `docs/content/workflow/` |
| **Şubat 2026** | Trigger, kategori, system/user flow, Faz 1 netleştirme | Validation API, RabbitMQ worker, `@wf_*`, Notifier |
| **Şubat 2026** | Faz 1 kod başlangıcı + checkpoint | `MngWorkflow` iskeleti, Validate API, Gateway route |
| **Task Manager oturumları** | TM = DG + Workflow (ayrı TaskManager servisi yok) | `TASK_MANAGER_PLANNING.md` ↔ workflow wiring |
| **Odak dönemi** | Node-based engine (Api + Worker), OC/Alarm/SIEM seam | `docs/odak/workflow/*`, Faz 0–6+ kod |
| **UI** | Otomasyon Merkezi W1 (form editör) | Menü + admin UI; W2 canvas sırada |
| **Pazarlama (2026-07)** | Modül envanteri | `modul-workflow.md` |

---

## 2. İlk planlama sohbeti (çekirdek kararlar)

Kaynak oturum: [Workflow IFTTT vs Node-RED](9a6d29bd-d0fb-472f-8f04-804c757f5b57)

### 2.1 Yaklaşım

| Seçenek | Not |
|---------|-----|
| IFTTT (trigger → condition → action) | Öncelikli başlangıç |
| Mini Node-RED (görsel node graph) | İleride genişletme |
| **Karar** | İsim: **MngRules**; hibrit yol (önce IFTTT, sonra graph) |

> Not: Odak implementasyonunda graph/node motoru fiilen ana çizgi haline geldi; MngRules dönemi validation pipeline + erken dataset modeli olarak kaldı.

### 2.2 Kilit kararlar (o oturumda kilitlenenler)

| Konu | Karar |
|------|--------|
| Servis | **MngWorkflow** ayrı servis |
| Task Manager backend | **Yok** — DG + Workflow |
| Veri modeli | DG dataset’leri, **`@` prefix** (`@wf_*`) |
| Validation URL | `POST /validate/{dataset}` |
| Pipeline eşleme | Dataset adına göre; **çoklu pipeline** |
| Domain | JWT / exchange adından |
| RabbitMQ | Ayrı worker; DG exchange üzerinden |
| Mail | **MngNotifier** sadece gönderim; template Workflow’ta |
| DG HTTP validation | Gateway base URL, JWT forward, per-validation timeout |
| Admin | JWT `is_admin` |
| Kategori | Flow’lar kategori içinde; **system** vs **user** flow |
| CRUD hooks | Dataset create/update **before** (validation) ve **after** (aksiyon) |

### 2.3 Checkpoint (25 Şubat 2026)

- Plan + Faz 1 planı yazıldı; Validate API + Gateway route kodlandı.
- Açık kalan: `setup-workflow-datasets.ps1` (kategoriler, rules, mail templates seed).
- Belge: [CHECKPOINT_2026_02_25.md](../../content/workflow/CHECKPOINT_2026_02_25.md)

---

## 3. Task Manager ile kesişim

İlgili oturumlar (örnek): [TM devam](4a5c5221-c940-416b-a86d-9bda4a752a87), [TM hatırlat](5824c027-1f81-46d3-bdc7-045e7cafce4d), [TM planning](ce0fcd65-8977-4650-80cf-744058ff5a41)

- TM issue validasyonu → Workflow validation pipeline.
- Seed örneği: `scripts/tests/MngDataGateway/workflow/setup-wf-validation-pipelines.ps1`
- Wiring: [TM_ISSUES_WORKFLOW_WIRING.md](../../content/workflow/TM_ISSUES_WORKFLOW_WIRING.md)

---

## 4. Odak motor dönemi (özet)

Birincil handoff: [DEVAM.md](../../odak/workflow/DEVAM.md) (son güncelleme ~4 Haziran 2026)

### 4.1 Mimari kilitler (motor)

| Konu | Karar |
|------|--------|
| Persistence | Hibrit Mongo (Worker + Api doğrudan) |
| Delay/Schedule | MngScheduler + kısa delay bucket kuyrukları |
| Granularity | Per-node mesaj + context persist |
| Expression | Jint (sandbox) |
| Servisler | `MngWorkflow.Api` + `MngWorkflow.Worker` |
| OC sınırı | WI içi senkron = `op_rules` (MO); çok adımlı = Workflow |

### 4.2 Kodlanan fazlar (özet)

Faz 0–6+: Worker/engine, CRUD/publish, retry/DLQ, Event Trigger, approval, delay, workitem create/transition/update, MO `startWorkflow`.

### 4.3 UI durumu (DEVAM’a göre)

- ✅ W1 form editör (Otomasyon Merkezi)
- Sırada: W2 canvas · P4 mqtt/publish · SIEM ayrı hat

### 4.4 Alarm / SIEM

- Tespit → Alarm Engine; aksiyon/orkestrasyon → Workflow Event Trigger.
- Detay: alarm + SIEM planları; seam dokümanları `docs/odak/`.

---

## 5. Ürün kimliği (pazarlama envanteri)

Kaynak: [modul-workflow.md](../../monitrang/pazarlama/Docs/modul-workflow.md)

- Workflow = **orkestrasyon + dış HTTP kapısı** (SDK yok).
- DI = belge üretimi; Workflow = onay / ERP / kanal / zincir.
- Dış giriş: HTTP webhook (HMAC) + kanal akışları (WhatsApp/Telegram planı).

---

## 6. Bugün için açık uçlar (tartışma girdisi)

Bunlar sohbet özetinden “hala gündemde / sırada” görünenler — net öncelik bu oturumda seçilecek:

1. **Seed seti** — lokal Docker için tekrarlanabilir örnek workflow + `@wf_*` / definition seed’lerinin `docs/workflow/seeds/` altında toplanması.
2. **UI W2 canvas** — kullanıcı `npm run dev` ile; deploy ayrı talep.
3. **Erken MngRules vs motor** — validation pipeline’ın motor ile ilişkisi (birleştirme / yan yana) netliği.
4. **Kanal / Telegram** — Notifier bağları ile workflow kanal adımları (platformda kısmen ayrı hatlar ilerledi).
5. **Doküman kökü** — bu klasör aktif; `docs/odak/workflow` Odak lab referansı olarak kalır.

---

## 7. İlgili transcript listesi (tarama)

| UUID | Not |
|------|-----|
| [9a6d29bd-d0fb-472f-8f04-804c757f5b57](9a6d29bd-d0fb-472f-8f04-804c757f5b57) | İlk IFTTT/Node-RED + MngRules + Faz 1 plan |
| [9a49bad3-b3fc-429d-bf16-aeabaa3410b1](9a49bad3-b3fc-429d-bf16-aeabaa3410b1) | İlişkili planlama (şartname / harita) |
| [45b3574d-b77c-4188-8fd4-b7322c5886d5](45b3574d-b77c-4188-8fd4-b7322c5886d5) | Yeni modül planlama (TM vb.) |
| [4a5c5221-c940-416b-a86d-9bda4a752a87](4a5c5221-c940-416b-a86d-9bda4a752a87) | Task Manager devam (büyük oturum) |
| [450c839b-1f4f-4322-bb6f-cbf031fb2412](450c839b-1f4f-4322-bb6f-cbf031fb2412) | TM planlama girişi |
| [ce0fcd65-8977-4650-80cf-744058ff5a41](ce0fcd65-8977-4650-80cf-744058ff5a41) | TM planning hatırlatma |
| [5824c027-1f81-46d3-bdc7-045e7cafce4d](5824c027-1f81-46d3-bdc7-045e7cafce4d) | TM durum hatırlatma |
| [1186f7b7-7d32-49e4-8792-6938ac47bc15](1186f7b7-7d32-49e4-8792-6938ac47bc15) | TM devam |
| [298df9ba-18d2-489e-8454-319b2903beeb](298df9ba-18d2-489e-8454-319b2903beeb) | TM devam |
| [a1ae1bf8-ce76-474c-8746-de0cb9eca5c9](a1ae1bf8-ce76-474c-8746-de0cb9eca5c9) | TM devam |

Odak motor sohbetlerinin çoğu doğrudan transcript başlığında “workflow” geçmeden `docs/odak/workflow` dosyalarına yazılmış; o dönem için **kaynak gerçeklik = DEVAM.md + Implementation Plan**.

---

## 8. Sonraki adım (bu klasör için)

1. Bugünkü oturum hedeflerini `planning/` altına yazmak.  
2. Eksik seed’leri `seeds/` altına üretmek / taşımak.  
3. Gerekirse bu özeti kullanıcıyla birlikte düzeltmek (eksik sohbet varsa eklemek).
