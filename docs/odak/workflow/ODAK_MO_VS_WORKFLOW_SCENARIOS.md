# Odak — MO vs Workflow Karar Matrisi ve DI Entegrasyonu

**Durum:** Planlama (ürün + mimari)  
**Son güncelleme:** 3 Temmuz 2026  
**Kapsam:** Odak Savunma / Üretim senaryoları · Document Intelligence ↔ Workflow  
**İlişkili:** [planing.md](./planing.md) · [Workflow Backend Implementation Plan v1.md](./Workflow%20Backend%20Implementation%20Plan%20v1.md) · [WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md) · [DI_PRODUCT_ROADMAP.md](../document_intelligence/DI_PRODUCT_ROADMAP.md)

---

## 1. Karar prensipleri (özet)

| Soru | MO / Alarm / Notifier | MngWorkflow |
|------|------------------------|-------------|
| Tek HTTP isteği içinde mi? | ✅ `op_rules` | ❌ |
| Tek adım mı (mail / yeni WI / tek API)? | ✅ otomasyon / schedule | Gereksiz |
| Günlerce onay veya bekleme var mı? | ❌ | ✅ |
| 3+ modül / 3+ adım zinciri mi? | ❌ | ✅ |
| Harici sistem (ERP, webhook) zinciri mi? | ❌ | ✅ |
| Platform geneli playbook (versiyonlu) mi? | ❌ | ✅ |
| Alarm **tespiti** mi? | ✅ Alarm Engine | ❌ |
| Alarm **müdahalesi** (onay + Engine + TTL) mi? | ❌ | ✅ |
| Cron → **tek** WI şablonu mu? | ✅ `op_work_item_schedules` | ❌ ikileme yok |
| Cron → **çok adımlı** rapor/onay zinciri mi? | ❌ | ✅ (veya DI schedule + workflow) |

**Köprü:** `op_rules` action `startWorkflow` — basit tetik MO’da, ağır playbook Workflow’da.

---

## 2. Odak senaryo matrisi (15 örnek)

Açıklamalar Odak Üretim workspace’i, sipariş/kalite ve mevcut platform parçalarına göre yazılmıştır.

| # | Senaryo | Önerilen katman | Gerekçe | Not / faz |
|---|---------|-----------------|---------|-----------|
| **1** | ODF `hold_quality` + uygunsuz → **NCR WI aç** (parent=ODF) | **MO — Otomatik işler** (`op_workspace_automations`) | Tek olay → tek aksiyon; aynı workspace; şablon + field mapping yeterli | SW-A0… implementasyon backlog |
| **2** | NCR kapanınca parent ODF **`resume_from_hold`** | **MO — `op_rules`** (validation veya automation) | Geçiş sonrası senkron; rollback yok | İş kuralı müşteri ile netleşecek |
| **3** | NCR disposition geçişinde **mail + bildirim** kalite ekibine | **MO — `op_rules`** automation | Inline side-effect; gecikme düşük | Mevcut action seti |
| **4** | Geçişte **validation** (ör. uygunsuz iken `approve_quality` engeli) | **MO — `op_rules`** validation | Pipeline durdurur; workflow değil | ✅ mevcut |
| **5** | Her Pazartesi 08:00 **kontrol listesi WI** (sabit board) | **MO — Zamanlanmış işler** | Cron → `from-origin`; tek kayıt | `op_work_item_schedules` |
| **6** | SLA ihlali → **escalation WI** + mail + atama değişimi | **MO tetik + Workflow** veya **Workflow** | SLA-7 E2E: `startWorkflow` pattern; çok adım olursa playbook | `op_rules` → `startWorkflow` |
| **7** | Metrik eşiği / korelasyon → **alarm üret** | **Alarm Engine** | Tespit workflow işi değil | ✅ MngAlarm |
| **8** | Kritik alarm → **onay bekle** → Engine firewall blok → 1 saat sonra unblock → **incident WI** | **Workflow** | Onay + delay + Engine + WI + audit zinciri | SIEM §8 eşlemesi |
| **9** | Alarm **bildirim politikası** (mail, gruplar) | **Alarm Merkezi — bildirim politikaları** | Alarm yaşam döngüsü; workflow değil | ✅ Alarm Center |
| **10** | Sipariş kalemi → **CoC DOCX üret** (generation profile) | **DI — MngDocument** | Veri → şablon merge; tek adım üretim | ✅ `odak.coc.fromLine` |
| **11** | CoC üretildi → **kaliteye mail** + deep link | **DI — D-N1** (Notifier) | Tek olay → tek mail; workflow şart değil | Roadmap D-N |
| **12** | CoC üretildi → **kalite onayı** → onaylanınca resmi klasöre taşı + WI kapat | **Workflow** (+ DI event) | İnsan onayı + çok adım; günler sürebilir | DI Faz D-WF |
| **13** | Şablon **publish/unpublish** (tasarımcı) | **DI — Belge Tasarımcısı** | Şablon yaşam döngüsü; workflow değil | ✅ unpublish API |
| **14** | Haftalık **operasyon durum raporu** DOCX (schedule + tablo parametreleri) | **DI — D-S** (üretim) | Zamanlanmış merge; tek dosya çıktısı | Roadmap D-S |
| **15** | Haftalık rapor üretildi → dağıtım listesi + **onay** + arşiv + ilgili WI güncelle | **Workflow** (DI event tetik) | Üretim DI’da; sonrası orkestrasyon | DI schedule → `document.generated` → workflow |

### 2.1 Özet dağılım

| Katman | Senaryo # |
|--------|-----------|
| MO (`op_rules` / otomasyon / schedule) | 1, 2, 3, 4, 5, 6 (kısmen) |
| Alarm Engine + Alarm bildirim | 7, 9 |
| DI (MngDocument) | 10, 11, 13, 14 |
| Workflow | 6 (ağır), 8, 12, 15 |
| Hibrit (MO/DI tetik → Workflow) | 6, 12, 15 |

---

## 3. Senaryo detayları (seçilmiş)

### 3.1 #1 — NCR otomatik açılış (MO)

```text
WHEN  ODF → hold_quality (veya kalite uygunsuz koşulu)
AND   workspace = Odak Üretim, type = ODF
THEN  createWorkItem → NCR Kuyruğu, parentItemId = ODF
```

**Neden workflow değil:** Tek spawn; `WORKSPACE_AUTOMATION_PLANNING.md` MVP tam hedefi.

---

### 3.2 #8 — Onaylı güvenlik müdahalesi (Workflow)

```text
WHEN  alarm.raised (severity=critical, rule=…)
THEN  If riskScore > X
      → Approval (SecurityAdmin)
      → Block IP (Engine)
      → Delay 1h (Scheduler)
      → Unblock IP
      → Create WorkItem (Güvenlik incident)
      → Notification
```

**Neden MO değil:** `approval.wait`, `delay.wait`, Engine komutu, correlation — playbook.

---

### 3.3 #12 — CoC onaylı yayın (Workflow + DI)

```text
WHEN  document.generated (profile=odak.coc.fromLine, origin=system)
THEN  Notification (kalite — opsiyonel, D-N1 ile overlap)
      → Approval (Kalite müdürü)
      → [onay] UpdateWorkItem (kalem alanları zaten writeback)
      → [onay] Move resource / mark lifecycle=published
      → Notification (müşteri/hazırlık ekibi)
```

**DI sorumluluğu:** merge, dosya, versiyon, writeback.  
**Workflow sorumluluğu:** onay, bekleme, çok taraflı bildirim sırası, audit.

---

### 3.4 #15 — Haftalık rapor dağıtımı (DI + Workflow)

```text
[DI D-S]  Cron → generateDocument → dm_resources
              → publish document.generated event

[Workflow]  Event Trigger filter: template=HAFTALIK-DURUM
            → Approval (opsiyonel)
            → Notification (dağıtım listesi)
            → Create WorkItem (review task, opsiyonel)
```

**Neden ikisi:** DI schedule **dosya üretir**; workflow **kurumsal dağıtım/onay** zincirini yönetir.

---

## 4. Document Intelligence — Workflow’a yaslanılacak yerler

DI roadmap ([DI_PRODUCT_ROADMAP.md](../document_intelligence/DI_PRODUCT_ROADMAP.md)) içinde **doğrudan MngDocument / Notifier / Scheduler** yeterli olanlar ile **Workflow gerektirenler** ayrılmalıdır.

### 4.1 DI’da MO/Notifier/Scheduler yeterli (Workflow yok)

| DI roadmap | Neden |
|------------|-------|
| **Faz P** — Sayfa editör, etiket | İçerik yönetimi; olay yok |
| **Faz D** — Collabora, upload, manuel üretim | Tek kullanıcı aksiyonu |
| **Faz D-E** — Editör oturum limiti | Operasyonel gate; workflow değil |
| **Faz D-P** — Parametre/query merge | Üretim motoru |
| **Faz S / Pr** — Sheet/Sunum Collabora | D ile aynı |
| **D-N1** — `document.generated` **tek mail** | Notifier orchestrator; basit bildirim |
| **D-S** — Zamanlanmış **dosya üretimi** | MngScheduler → MngDocument job |

### 4.2 DI’da Workflow **gerekli veya güçlü önerilen**

| DI roadmap | Workflow rolü | Tetikleyici (öneri) |
|------------|---------------|---------------------|
| **Kontrollü doküman yayını (Faz M)** | Onay → Yayında → Arşiv; revizyon notu zorunlu | `document.submittedForReview` |
| **CoC / resmi belge onay hattı** | Kalite onayı bekle → yayınla / reddet | `document.generated` + filter profile |
| **Şablon publish (kurumsal)** | İkinci onay (kalite + IT) — opsiyonel | `template.submittedForPublish` |
| **D-N2 — klasör aboneliği + onaylı dağıtım** | Sadece mail değil; onaylı dağıtım listesi | `document.published` |
| **Haftalık/aylık rapor (D-S + dağıtım)** | Üretim sonrası çok alıcı + onay | `document.generated` + scheduleId |
| **DI ↔ OC (D5)** — kanıt / output WI | WI oluştur + döküman ilişkilendir + gecikmeli hatırlatma | `document.generated` veya WI event |
| **Faz AI — düşük güven skoru** | AI tag/summary → insan onayı → yayın | `document.aiProcessed` + `confidence < X` |
| **Reddedilen / superseded belge** | Eski sürüm arşiv + ilgili WI güncelle + bildirim | `document.lifecycleChanged` |

### 4.3 DI olay sözleşmesi (Workflow Event Trigger için — taslak)

MngDocument üretim/tamamlanma noktalarında RabbitMQ (veya mevcut domain exchange) üzerinden **workflow-dostu** zarf:

```json
{
  "eventType": "document.generated",
  "domainId": "…",
  "occurredAt": "…",
  "correlationId": "…",
  "payload": {
    "resourceId": "…",
    "templateId": "…",
    "generationProfile": "odak.coc.fromLine",
    "origin": "system|manual|upload",
    "documentNo": "…",
    "folderId": "…",
    "contextType": "odak.siparis.line",
    "contextId": "…",
    "lifecycle": "draft|active",
    "hasParameterWarnings": false
  }
}
```

**Planlanan event türleri:**

| eventType | Ne zaman |
|-----------|----------|
| `document.generated` | Merge + kaynak oluşturma tamam |
| `document.published` | Sayfa yayın / döküman lifecycle active |
| `document.submittedForReview` | Taslak → incelemede (Faz M) |
| `document.approved` / `document.rejected` | Onay node kararı (workflow çıktısı → DI API) |
| `document.schedule.failed` | D-S job hata |
| `template.published` | Şablon üretime alındı |

Workflow **filterExpression** örneği:

```text
event.generationProfile == 'odak.coc.fromLine' && !event.hasParameterWarnings
```

### 4.4 DI roadmap’e eklenecek faz: **D-WF**

| Dilim | Kapsam | Bağımlılık |
|-------|--------|------------|
| **D-WF0** | DI → event publish (`document.generated`, `document.published`) | MngDocument + RabbitMQ |
| **D-WF1** | Referans playbook: **CoC kalite onayı** (Approval → notify → lifecycle) | Workflow Faz 5+6, D-N1 |
| **D-WF2** | `document.approved` / reject → DI lifecycle API (workflow node) | Faz M lifecycle |
| **D-WF3** | Haftalık rapor: D-S + workflow dağıtım zinciri | D-S2, D-WF0 |
| **D-WF4** | AI düşük güven → onay playbook | Faz AI2+ |

**Sınır:** D-N basit mail = Notifier; **onay + çok adım + günler** = Workflow.

---

## 5. Çakışma önleme checklist

Yeni bir otomasyon tasarlanırken:

1. **Kaç adım?** 1 → MO; 2+ modül → Workflow adayı.
2. **Bekleme/onay var mı?** Evet → Workflow.
3. **Sadece WI mı?** Evet → otomasyon veya schedule.
4. **Sadece mail mi?** Evet → `op_rules` veya Alarm/DI Notifier.
5. **Sadece dosya üretimi mi?** Evet → DI (+ isteğe bağlı tek mail).
6. **Dosya + onay + dağıtım mı?** Evet → DI üret + Workflow orchestrate.
7. **Alarm tespiti mi?** Alarm Engine.
8. **Alarm müdahalesi mi?** Workflow.
9. **Zaten `startWorkflow` var mı?** MO’da tetik bırak, playbook Workflow’da genişlet.

---

## 6. Uygulama önceliği (Odak)

| Öncelik | İş | Katman |
|---------|-----|--------|
| P0 | NCR otomasyonu (SW-A0) | MO |
| P0 | CoC üretim + D-N1 mail | DI |
| P1 | SLA breach → `startWorkflow` (mevcut E2E genişlet) | MO + Workflow |
| P1 | D-WF0 event publish + CoC onay playbook taslağı | DI + Workflow |
| P2 | Haftalık rapor D-S + D-WF3 | DI + Workflow |
| P2 | Kritik alarm müdahale playbook (Odak IT) | Workflow |
| P3 | Faz M kontrollü doküman + D-WF2 | DI + Workflow |

---

## 7. Referanslar

| Doküman | Konu |
|---------|------|
| [Workflow Backend Plan §13](./Workflow%20Backend%20Implementation%20Plan%20v1.md) | MO sınırı, `from-origin`, `startWorkflow` |
| [WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md) | Otomatik işler vs workflow |
| [RULE_ENGINE.md](../operationcore/mngoperations/RULE_ENGINE.md) | Inline automation |
| [DI_PRODUCT_ROADMAP.md](../document_intelligence/DI_PRODUCT_ROADMAP.md) | DI fazları + D-WF |
| [alarm/DEVAM.md](../alarm/DEVAM.md) | Alarm ↔ workflow seam |
| [is_surecleri/DEVAM.md](../is_surecleri/DEVAM.md) | Odak NCR/CAPA |
