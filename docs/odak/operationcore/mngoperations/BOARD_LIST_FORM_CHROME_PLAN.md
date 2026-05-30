# MngOperations — Board liste sütunları + Form chrome planı (planlama belgesi)

**Son güncelleme:** 30 Mayıs 2026
**Durum:** **Planlama** (kodlama başlamadı) — tasarım onaylandı
**İlgili:** [DEVAM.md](./DEVAM.md) · [RUNTIME_CONTEXT.md](./RUNTIME_CONTEXT.md) · [API_SURFACE.md](./API_SURFACE.md) · [SLA_FAZ1_PLAN.md](./SLA_FAZ1_PLAN.md) · [DG_INTEGRATION.md](./DG_INTEGRATION.md)

> Bu belge iki büyük başlığı kapsar: (1) **Board liste sütun zenginleştirme** (audit/SLA + dinamik sütunlar), (2) **Form chrome** (yorum, SLA paneli, politika bilgisi, attachments, mention). Tasarım kararları kullanıcı onayıyla sabittir; uygulama henüz başlamadı.

---

## 0. Onaylanan kararlar (özet)

| Konu | Karar |
|------|-------|
| `createdBy` | Sadece **forward-only** damga (MO create `UserId` yazar); eski kayıtlar `—`. Backfill yok. |
| SLA chip mantığı | **Akıllı faz** — proxy: item initial state'te ise response, çıktıysa resolve; closed ise tamamlandı. (`respondedAt` alanı yok.) |
| "Geçen süre" (age) | **until_closed** — açık: created→şimdi; kapalı: created→closedAt (çözüm süresi). |
| Sistem sütunları sort/filter | **Evet** — gerçek DB alanı oldukları için server-side BLF ile entegre. |
| Form chrome kapsamı | **Form tasarımından bağımsız**, her zaman var. |
| Profil yerleşimi | **Hibrit**: sağ sidebar (SLA + meta + politika özeti) + ana kolonda sekmeler (`Detay | Yorum[N] | Ekler[N]`). |
| Edit modal | **Sade**: form + SLA önizleme + Ekler. Yorum/politika detayı profilde. |
| Attachments | **DG `file` (isArray) alanına yaslan** — yeni MinIO backend yok. |

---

## 1. Mevcut durum envanteri (kod doğrulaması)

| Bileşen | Backend | UI | Not |
|---------|---------|----|----|
| `createdAt` | ✅ MO create yazıyor (`WorkItemCommandService` ~671) | — | Gerçek alan, server-side sıralanır |
| `createdBy` | ❌ Tutulmuyor (DG audit damgası ROADMAP `[ ]`) | — | MO create damgalayacak |
| SLA snapshot | ✅ `op_work_items.sla` = `{responseDueAt, resolveDueAt, responseBreached, resolveBreached, calculatedAt}` (+`slaPolicyId`) | — | `ProfileRuntimeContext.Sla` var; `WorkItemCardDto` yüzeye çıkarmıyor |
| Yorum | ✅ `POST /{id}/comments`, `CommentDto` (threaded `ParentCommentId`), `op_comments` | ❌ | `Permissions.CanComment` var |
| Timeline | ✅ `GET /work-items/{id}/timeline` sayfalı, comment+activity birleşik (`TimelineEntryDto`) | ❌ | |
| Politikalar | ✅ Veri var (RuleEngine, `op_sla_policies`, `FieldBehaviors`) | ❌ | Bilgilendirme amaçlı |
| Attachments | ⚠️ **DG `file` field tipi tam yönetiyor** (aşağıda) | ❌ | MO'da yeni altyapı gerekmez |
| Mention | ❌ Sıfırdan | ❌ | Notification altyapısı + person directory üstüne kurulur |

### 1.1 DG `file` alanı — neden attachments için yeterli

- Şema alan tipi `file`, opsiyonel **`isArray: true`** (geçerli tipler: `DatasetService` ~835).
- **Veri create/update sırasında DG otomatik işliyor** (`DataController.ProcessFileFieldsFromJsonElementAsync`, create ~126 / update ~459):
  - Değer `{ content: "<base64>", file_name, ... }` ise → MinIO'ya yükler (sıkıştırma/şifreleme), alanı **kalıcı path nesnesiyle değiştirir**.
  - `content` yoksa (mevcut path nesnesi) → korur. `isArray`'de yeni+mevcut karışık olabilir.
- İndirme: `GET /api/v1/files/download?filePath=...` (domain + permission, decrypt/decompress otomatik). Metadata: `/files/metadata`.
- **Sonuç:** create asimetrisi yok — file alanı record create ile aynı çağrıda işlenir (recordId yoksa DG üretir). Staged upload gerekmez.

---

## 2. Grup 1 — Board liste sütun zenginleştirme

### A · Audit/SLA backend (MO)
- `WorkItemCardDto`'ya ekle: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `LastStateChangeAt`, `ClosedAt`, `Sla` (`SlaSnapshotDto`).
- `MapWorkItemCard` bu alanları doldursun; core projeksiyona `sla`, `createdBy`, `closedAt`, `lastStateChangeAt` dahil et.
- **MO create**: `payload["createdBy"] = requestContext.UserId` (forward-only).
- `createdBy`'ı `QueryExecuteResponse.People` çözümlemesine ekle (assignee gibi id→ad).
- Server-side sort/filter path eşlemesi: `createdAt/createdBy/updatedAt/lastStateChangeAt` (BLF `$and` ile uyumlu).

### B · Format katmanı (UI, paylaşılan)
- `formatCellValue(value, format)` — `text | number | money | date | relativeTime`.
- `relativeTime` "geçen süre"yi karşılar; kapalı item'da anchor `closedAt`.
- Hem sistem sütunları hem dinamik sütunlar (D) bu katmanı paylaşır.

### C · Sistem sütunları (UI)
- Liste sütun seçicisine eklenebilir: `createdAt`, `createdBy` (kişi chip), `updatedAt`, `lastStateChangeAt`, `age` (relativeTime/createdAt), `sla`.
- **`OcSlaStatusChip`**: akıllı faz (initial-state proxy) + canlı kalan/gecikme + renk (yeşil/sarı/kırmızı). Profil SLA paneliyle (F2) ortak.
- Admin board tasarım editöründe ekleme + `sortable`/`filterable`/`format` ayarları (mevcut `OpBoardListColumnConfig` genişletilir).

### D · Dinamik (computed) sütunlar (UI, ayrı PR)
- `expr-eval` tabanlı **whitelisted** ifade + `{fieldKey}` token (çözülmüş etiketleri referanslar) + B'deki format katmanı.
- Faz 1: **display-only** (sort/filter yok). Veri modeli: `board.config.listColumns[]` içine `computed: true`, `expression`, `inputs`, `format`.
- İzinli fonksiyonlar whitelist'i (text/number/logic/date — `date-fns`). Admin editörü: etiket + ifade (token yardımcısı) + format seçici.

---

## 3. Grup 2 — Form chrome (profil/edit)

### Yerleşim (hibrit)
- **Profil (detay) sayfası — 2 sütun.** `ProfileRuntimeContext`'in `Header/Sidebar/Panels/Layout` yapısıyla örtüşür.
  - **Ana kolon → sekmeler (chip'li):** `Detay` (form, read-only) · `Aktivite & Yorum [N]` · `Ekler [N]`.
  - **Sağ sidebar → her zaman görünür:** SLA widget (canlı timer + renk) · oluşturan/oluşturma/güncelleme/kapanış · atanan + watchers · politika özeti (açılır detay).
- **Edit modal — sade:** form + SLA önizleme satırı + Ekler. Yorum/timeline/politika profile yönlendirir.
- Dar ekran: sidebar otomatik alta/sekmeye düşer.

### F1 · Yorum + timeline UI  *(backend hazır)*
- Birleşik timeline akışı (`GET /timeline`) + yorum kutusu (threaded `ParentCommentId`). `CanComment` ile gate.

### F2 · SLA paneli  *(backend hazır)*
- Sidebar canlı widget: response/resolve timer, renk, gecikme — `OcSlaStatusChip` (C) mantığının büyük hali.
- Create'te: seçilen type/priority'ye göre **hangi politika uygulanacak** önizleme (read-only; `MetadataCache.ResolveSlaPolicy`).

### F3 · Politika/kural bilgilendirme paneli
- Sidebar özet: uygulanan SLA politikası, aktif kural sayısı; "neden bu alan zorunlu/readonly" (`FieldBehaviors` zaten context'te). Açılır detay.

### F4 · Mention
- Yorum body'sinde `@kişi` autocomplete (mevcut `OcPersonPicker`).
- Comment'e `mentions: [userId]` yaz → notification altyapısıyla bildirim. Opsiyonel: mention edilen kişi otomatik watcher.
- **Açık detay:** bildirim tetik politikası (her zaman vs notification policy'ye bağlı) F4 başında netleşecek.

### F5 · Attachments = DG `file` alanı  *(yeni backend yok)*
- Form tasarımından bağımsız sabit **"Ekler"** sekmesi. Modelleme: work item type dataset'inde `attachments: file[]` (isArray) alanı.
- UI bileşeni: dosya seç → base64 → normal create/update payload'una göm (mevcut path nesneleri listelenir + indirme).
- **Doğrulanacak entegrasyon noktaları:**
  1. **MO passthrough** — work item create/update pool/extra alanları DG'ye olduğu gibi iletmeli; `CollapseRelationValue` `file` nesnesini **bozmamalı** (assignee/watchers id-collapse mantığı file'a uygulanmasın).
  2. **İndirme proxy** — UI→MO→DG akışı için MO'da `GET /runtime/work-items/{id}/files?path=` → DG `/files/download` proxy (permission tutarlılığı).

---

## 4. Önerilen uygulama sırası

1. **A → B → C** — audit/SLA sütunları; ortak format katmanı + `OcSlaStatusChip` üretir.
2. **F-grup profil refaktörü** + **F1 (yorum)** + **F2 (SLA panel, chip'i tekrar kullanır)** + **F5 (attachments)** — yüksek değer, backend/DG hazır.
3. **F3 (politika)** + **F4 (mention)**.
4. **D (computed columns)** — en son.

---

## 5. Açık sorular / sonra netleşecek

- F4 mention bildirimi: her zaman mı, notification policy'ye bağlı mı? + otomatik watcher davranışı.
- F5 attachments: type başına tek `attachments` alanı mı, yoksa OC genel ortak şema mı; izin/permission görünürlüğü.
- D computed: izinli fonksiyon whitelist'inin son listesi; server-side faz 2 (sort/filter) gerekecek mi.
- Eski work item'larda `createdBy` boş — UI'da `—` gösterimi yeterli mi, yoksa "Sistem"/ilk activity actor'ı mı?
