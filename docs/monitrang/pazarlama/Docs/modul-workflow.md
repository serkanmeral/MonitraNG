# Workflow — Modül özellik envanteri

**Kod:** `workflow` · **Durum:** Canlı (backend + admin UI); görsel editör ve dış entegrasyon genişletmesi devam ediyor  
**UI:** `/apps/automation-center/workflows` *(Otomasyon Merkezi — hedef ad: Workflow)* · **Backend:** MngWorkflow.Api · MngWorkflow.Worker

**Referanslar:** [Workflow DEVAM (iç)](../../odak/workflow/DEVAM.md) · [Backend Implementation Plan v1](../../odak/workflow/Workflow%20Backend%20Implementation%20Plan%20v1.md) · [MO vs Workflow karar matrisi](../../odak/workflow/ODAK_MO_VS_WORKFLOW_SCENARIOS.md)

> **Bu dosyanın amacı:** Workflow **ürün kimliği**, **OC / Alarm / DI ile sınır**, müşteri perspektifi ve fonksiyon envanteri. Broşür **ertelendi**.

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Workflow**, kurum içi ve dış sistemler arasında **çok adımlı, versiyonlu iş akışlarını** çalıştıran platform **orkestrasyon katmanıdır** — onay bekleme, gecikme, HTTP çağrısı, belge üretimi, operasyon kaydı ve bildirimleri tek zincirde birleştirir.

### 1.2 Hub rolü (DI ile ayrım)

| Hub | Rol | Müşteri cümlesi |
|-----|-----|-----------------|
| **Döküman Zekası** | **Belge üretim** hedefi | «Resmi Word/Excel çıktısı» |
| **Workflow** | **Orkestrasyon + dış HTTP kapısı** | «Onay al, ERP’ye yaz, sonra belge üret» |

### 1.3 Dış entegrasyon ilkesi

**SDK yok.** Dış taraflar iki yüzeyden sürece girer:

| Yüzey | Kim | Nasıl |
|-------|-----|--------|
| **HTTP flow** | Partner / ERP / dış sistem | Tanımlı webhook endpoint (HMAC) |
| **Kanal Akışları** | Müşteri, vatandaş, saha personeli | WhatsApp, Telegram, … — mesajla diyalog |

İç modüller olay veya zamanlama ile akışı tetikler.

```text
Dış partner ──HTTP──► Workflow endpoint ──► DI / OC / Raporlama / HTTP…
WhatsApp/Telegram ──► Kanal kapısı ──► Workflow (channel.* adımları) ──► aynı zincir
Modül olayı ────────► Event trigger ────► aynı zincir
```

### 1.4 Workflow ne değildir?

| Beklenti | Gerçek |
|----------|--------|
| OC’nin yerine geçer | OC = **insan süreci**; Workflow = **otomasyon zinciri** |
| Alarm tespiti | **Alarm Engine / SIEM** — Workflow **müdahale** |
| Tek mail / tek WI | **OC kuralı** veya **Notifier** yeterli |
| Cron → tek kontrol listesi WI | **Scheduler → OC** — Workflow ile ikileme yok |
| WhatsApp «bot modülü» ayrı ürün | **Kanal Akışları** — Workflow alt yeteneği; veri/kimlik flow tasarımında |
| Notifier = diyalog botu | Notifier **push**; Kanal Akışları **oturum + Workflow resume** |

---

## 2. Müşteri perspektifi

### 2.1 Tek paragraf

**Workflow**, «e-postayla onay iste, üç gün bekle, onaylanınca belgeyi üret ve arşive taşı» gibi **birden fazla adım ve sistem** içeren senaryoları kod yazmadan tanımlanabilir akışlara dönüştürür. Operasyon ekibi tek kayıtta çalışmaya devam eder; arka planda platform adımları sırayla işler. Dış tedarikçi veya ERP, güvenli HTTP uç noktası üzerinden sürece dahil olabilir.

### 2.2 Günlük deneyim

| Adım | Müşteri dili |
|------|----------------|
| 1 | Süreç sahibi akışı tasarlar / yayınlar |
| 2 | Olay (belge, alarm, OC) veya HTTP isteği akışı başlatır |
| 3 | Adımlar sırayla çalışır — koşul, bekleme, onay |
| 4 | Gerekirse **Operasyon Merkezi** kaydı açılır veya güncellenir |
| 5 | **Döküman Zekası** belgesi üretilir |
| 6 | Bildirim gider; hata olursa yeniden deneme / DLQ |

### 2.3 Ne zaman Workflow, ne zaman OC?

| Senaryo | Önerilen katman |
|---------|-----------------|
| Geçişte «açıklama zorunlu» | **OC kuralı** |
| Atanınca mail | **OC kuralı** + Notifier |
| Her Pazartesi tek checklist WI | **Scheduler → OC** |
| CoC üret + mail *(tek adım)* | **DI + Notifier** |
| CoC üret → **onay** → arşiv → WI kapat | **Workflow** |
| Kritik alarm → onay → firewall aksiyonu → incident WI | **Workflow** |
| Haftalık rapor DOCX *(tek dosya)* | **DI zamanlama** |
| Rapor üret → dağıtım listesi → onay → çok modül | **Workflow** |
| WhatsApp «faturamın durumu?» → bilgi iste → sorgu → yanıt | **Workflow — Kanal Akışları** |
| Telegram «sıradaki işim ne zaman?» → kimlik adımı → OC/HTTP | **Workflow — Kanal Akışları** |

> Detaylı matris: iç referans [MO vs Workflow senaryoları](../../odak/workflow/ODAK_MO_VS_WORKFLOW_SCENARIOS.md) — ürün dili **sektörden bağımsızdır**.

### 2.4 Alarm × Workflow × SIEM

```text
Log / metrik → SIEM / Alarm Engine (tespit)
                    ↓ alarm.raised
              Workflow (müdahale playbook)
                    ↓
              OC · Engine · Notifier · DI
```

---

## 3. Temel kavramlar

| Kavram | Tanım |
|--------|--------|
| **Workflow definition** | Akış tanımı (versiyonlu) |
| **Run / instance** | Tek çalıştırma örneği |
| **Node** | Adım türü: HTTP, If, Log, delay, approval, workitem… |
| **Trigger** | Manual, event (`oc.events`, alarm…), schedule, webhook, **kanal mesajı** |
| **Context** | Adımlar arası veri (Jint expression) |
| **Correlation id** | Yeniden deneme / idempotency |
| **Kanal oturumu** | `phone` / `chatId` + channel ↔ aktif instance *(Kanal Akışları)* |

---

## 4. Fonksiyon envanteri

### 4.1 Motor ve persistence

| Yetenek | Durum | Not |
|---------|-------|-----|
| MngWorkflow.Api + Worker | ✅ | Faz 0–6+ |
| Per-node execution + Mongo context | ✅ | |
| Retry bucket + DLQ | ✅ | |
| Jint expression (sandbox) | ✅ | |
| Definition/version CRUD, publish | ✅ | |
| Graph validator | ✅ | |

### 4.2 Node ve trigger türleri

| Tür | Durum | Not |
|-----|-------|-----|
| Manual start | ✅ | |
| If / branch | ✅ | |
| HTTP request | ✅ | 4xx/5xx retry ayrımı |
| Log | ✅ | |
| Event trigger (`oc.events`…) | ✅ | Faz 4 |
| `delay.wait` | ✅ | Kısa: bucket; uzun: Scheduler |
| Schedule trigger | ✅ | MngScheduler sync |
| Webhook + HMAC | ✅ | `@workflow_secrets` |
| `approval.wait` + resume | ✅ | Faz 5 |
| `workitem.create` / `transition` / `update` | ✅ | Faz 6 — MO `from-origin` |
| MO `op_rules` → `startWorkflow` | ✅ | Köprü |
| AI node | 📋 | Spec var — ⏸️ platform AI kararı |
| Canvas görsel editör (W2) | 🔲 | W1 form editör ✅ |
| **Kanal Akışları** — `channel.send` | 🔲 | WhatsApp / Telegram / … |
| **Kanal Akışları** — `channel.wait` | 🔲 | Kullanıcı cevabı → context; `WaitingChannelInput` |
| **Kanal Akışları** — inbound webhook + oturum | 🔲 | Channel Gateway *(Notifier genişlemesi)* |
| **Kanal Akışları** — intent / flow eşlemesi | 🔲 | Keyword veya flow başına routing |

### 4.3 UI ve operasyon

| Yetenek | Durum | Not |
|---------|-------|-----|
| Otomasyon Merkezi — workflow listesi | ✅ | `/apps/automation-center/workflows` |
| Form tabanlı editör (W1) | ✅ | |
| Run history | ✅ | |
| Görsel canvas (W2) | 🔲 | Sıradaki UI |

### 4.4 Modül entegrasyonları

| Hedef | Durum | Not |
|-------|-------|-----|
| Operasyon Merkezi WorkItem | ✅ | create/transition/update |
| OC event tüketimi | ✅ | RabbitMQ |
| Alarm → workflow | ✅ | `mng.alarms` seam |
| Döküman Zekası belge | 🔲 | Event + HTTP — D-WF |
| Raporlama HTTP/DB köprüsü | 🔲 | RPT plan |
| Monitoring aksiyon | 🔲 | P4 mqtt vb. |
| Kanal Akışları (WhatsApp, Telegram…) | 🔲 | §7 — planlama |

### 4.5 Scheduler ile sınır

| İş | Kim yapar |
|----|-----------|
| Cron → **tek** OC WorkItem | **Scheduler** → `from-origin` |
| Cron → **çok adımlı** akış | **Workflow** schedule trigger |
| Uzun gecikme (onay beklerken) | Workflow + Scheduler one-shot |

---

## 5. Gerçek hayat örnekleri

| # | Senaryo |
|---|---------|
| 1 | Gizli sözleşme yüklendi → onay zinciri → onaylanınca arşiv klasörü |
| 2 | Kritik SIEM alarmı → playbook → SOC WorkItem |
| 3 | ERP webhook → stok kontrolü → DI irsaliye |
| 4 | Aylık rapor DOCX (DI) → dağıtım onayı → e-posta listesi |
| 5 | NCR açılışı *(tek adım)* → **OC otomasyon**, Workflow değil |
| 6 | WhatsApp «borcum / fatura durumu» → vergi/fatura no iste → HTTP/DG → yanıt | **Kanal Akışları** |
| 7 | Telegram saha personeli «sıradaki iş» → kimlik adımı → OC sorgu → saat bildir | **Kanal Akışları** |

---

## 7. Kanal Akışları *(Channel Flows)*

**Kod:** `channel-flows` · **Konum:** Workflow **alt yeteneği** — ayrı broşür modülü değil  
**Durum:** 🔲 Planlandı *(mimari karar; implementasyon yok)*

### 7.1 Tek cümle

**Kanal Akışları**, müşteri, vatandaş veya saha personelinin **WhatsApp, Telegram** ve ileride diğer mesajlaşma kanallarından başlattığı **self-servis diyalogları** aynı Workflow motorunda çalıştırır — veri nereden gelir, kimlik nasıl doğrulanır, kaç tur soru sorulur **flow tasarımına** bağlıdır.

### 7.2 Ne değildir?

| Beklenti | Gerçek |
|----------|--------|
| Sabit «borç botu» / «fatura botu» | **Genel flow motoru** — senaryo flow’da tanımlanır |
| Sadece WhatsApp | **Kanal-agnostik** — provider: `whatsapp`, `telegram`, … |
| MngLLM uygulama içi chatbot | LLM yalnızca *niyet yönlendirme* için opsiyonel; cevap flow adımlarından |
| Notifier push bildirimi | Push = tek yön olay; Kanal Akışları = **çok tur diyalog + resume** |
| OC WorkItem arayüzü | OC = iç operasyon; kanal = **dış self-servis yüzeyi** |

### 7.3 Tetik ve akış modeli

```text
[Kanal: WhatsApp / Telegram / …]
        │
        ▼ inbound webhook
[Channel Gateway]  ── oturum: channel + address + domainId
        │
        ├─ flow seçimi (keyword / intent / tek flow)
        ▼
[Workflow instance]
        │
        ├─ channel.send     «Fatura numaranızı yazın»
        ├─ channel.wait     → context (invoiceNo, taxNo, phone…)
        ├─ if / validate    format, Keeper eşleşmesi, OTP adımı…
        ├─ http.request     ERP / DG / Raporlama
        ├─ workitem.create  «şikâyet aç» gibi iç süreç
        └─ channel.send     sonuç metni / link
```

**Veri ve kimlik:** platform sabit kural değil — flow adımları (`channel.wait`, HTTP, If) ile tasarlanır.

### 7.4 Kanal envanteri (hedef)

| Kanal | Outbound | Inbound diyalog | Not |
|-------|----------|-----------------|-----|
| **Telegram** | ✅ push + bağlama | 🔲 Kanal Akışları | Webhook altyapısı mevcut |
| **WhatsApp** | 🔲 push (plan) | 🔲 Kanal Akışları | Meta WABA / template kısıtları |
| **SMS** | 🔲 | 🔲 | İleride aynı `IChannelProvider` |
| **Web chat** | — | 🔲 | Embed widget *(ileride)* |

### 7.5 Notifier ile sınır

| | **Notifier (push)** | **Kanal Akışları** |
|--|---------------------|---------------------|
| Yön | Platform → kullanıcı | **İki yönlü** diyalog |
| Tetik | OC / alarm / DI olayı | **Kullanıcı mesajı** |
| Oturum | Yok | `phone` / `chatId` ↔ workflow instance |
| İş mantığı | Template + context | **Workflow adımları** |

Teknik hedef: **Channel Gateway** (inbound webhook, oturum store, resume) + Notifier **provider** katmanı (outbound gönderim). İş kuralları Workflow’da kalır.

**İç referans:** [MESSAGING_CHANNELS.md](../../odak/notifications/MESSAGING_CHANNELS.md) *(push-only faz; Kanal Akışları genişleme notu ile uyumlu)*

### 7.6 Örnek senaryolar *(sektörden bağımsız)*

| Mesaj / niyet | Flow adımları (özet) |
|---------------|----------------------|
| «Borcum nedir?» | Kimlik/vergi no iste → HTTP sorgu → tutar yanıtla |
| «Faturamın durumu?» | Fatura no iste → DG/ERP → durum metni |
| «Sıradaki işim ne zaman?» | Telefon/Keeper eşleşmesi → OC/üretim API → saat |
| «Belgem hazır mı?» | Referans no → DI arama → deep link |
| «Şikâyet yazmak istiyorum» | Kısa form (channel.wait) → OC WorkItem aç → onay mesajı |

### 7.7 Broşür dili *(ertelendi)*

**Kısa:** *«WhatsApp ve Telegram üzerinden self-servis sorular — Workflow ile tasarlanır.»*

**Workflow § ile birlikte:** HTTP flow dış sistemden; **Kanal Akışları** müşteri/vatandaştan.

---

## 6. Platform bağlantıları

| Bileşen | Rol |
|---------|-----|
| **Keeper** | Yetki, service token |
| **Scheduler** | Schedule trigger, uzun delay |
| **Notifier** | Mail adımı; Kanal Akışları **outbound + gateway** *(plan)* |
| **DataGateway** | Veri okuma/yazma adımları |
| **Alarm Engine** | Tetik kaynağı |
| **RabbitMQ** | Async execution |

---

## Broşür (ertelendi)

Taslak: [platform-tanitimi.md § Workflow](./platform-tanitimi.md) · Kanal Akışları: §7

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · v0.2*
