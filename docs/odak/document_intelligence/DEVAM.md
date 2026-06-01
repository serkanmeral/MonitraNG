# Document Intelligence (MngDocument) — Devam noktası (checkpoint)

**Son güncelleme:** 1 Haziran 2026 gece (**DI-VH + DI-AUDIT** — sürüm geçmişi + oluşturan/güncelleyen audit; `mngdocument` Odak'ta canlı, `mngui` UI değişiklikleri **yerelde**, test sunucusuna **deploy edilmedi**)
**Durum:** **Faz 1** fonksiyonel çekirdek ✅ — tek anlamlı boşluk **yetkilendirme** (grup bazlı + miras), plan gereği Faz 1'de minimum bırakıldı.

> **⭐ KALDIĞIMIZ YER (1 Haz ~03:18) — yeni chat buradan devam edecek:** Bu oturumda **dosya yükleme/indirme**, **sürüm geçmişi** (listele/önizle/geri yükle) ve **audit** (doküman oluşturan/güncelleyen + sürüm bazında yazar/tarih) tamamlandı. `mngdocument` Odak'ta **canlı**. **UI (`mngui`) değişiklikleri yalnızca yerelde** (`npm run dev`, port 3000) — kullanıcı UI deploy'unu kendisi tetikleyecek. **Sıradaki seçenekler:** (1) UI'ı test sunucusuna deploy edip Faz 1'i kapatmak, (2) yetkilendirme (grup bazlı klasör yetkisi + miras), (3) dosya inline preview (PDF/görsel/metin).

**Ana plan:** [MonitraNG_Document_Intelligence_Planning.md](MonitraNG_Document_Intelligence_Planning.md) · **Faz 1 dataset'leri:** [datasets/documentintelligence_datasets_phase1.json](datasets/documentintelligence_datasets_phase1.json)

---

## Çalışma kuralları (kullanıcı tercihi)

- **Backend (MngDocument vb.) değişiklikleri**: sormadan **otomatik deploy** edilebilir.
- **UI (`mngui`) deploy'u**: yalnızca kullanıcı açıkça isteyince yapılır.
- Yanıtlar **Türkçe**.

---

## Faz 1 durum tablosu

| Alan | Durum | Not |
|------|-------|-----|
| Resources ana ekranı (master-detail) | ✅ | Yeniden boyutlandırılabilir sol panel (`useResizableTreePanel`) |
| Tree + klasör/alt klasör oluşturma | ✅ | `DiResourceTree.vue` |
| Yeniden adlandırma / taşıma / silme | ✅ | Taşımada alt ağaç `ancestorIds` reindex; silmede boş-değil zorunlu onay |
| Klasör içeriği listeleme + breadcrumb | ✅ | "Klasöre dön" + tıklanabilir breadcrumb |
| Markdown oluşturma/editör/preview | ✅ | `DiMarkdownEditor` / `DiMarkdownViewer` (`marked`+`DOMPurify`) |
| Markdown kaydetme/düzenleme | ✅ | Optimistic concurrency (409 çakışma yönetimi) |
| **Sürüm geçmişi** (listele/önizle/geri yükle) | ✅ | Plan üstü; `dm_resource_versions` |
| Dosya yükleme/metadata/indirme | ✅ | base64 → DG → MinIO; tip ikonları + boyut |
| Temel arama | ✅ | Ad/başlık/açıklama + markdown içeriği full-text |
| **Temel audit** (oluşturan/güncelleyen + sürüm yazar/tarih) | ✅ | Bu oturumda tamamlandı (aşağıya bak) |
| i18n (tr/en) | ✅ | `documentIntelligence.*` |
| Grup bazlı klasör yetkileri | ⬜ | Faz 1 minimum ("domain içi açık") — ertelendi |
| Yetki mirası | ⬜ | Yetki sistemiyle birlikte |
| Dosya inline preview (PDF/görsel/metin) | ⬜ | Şu an indirme var; plan "temel preview hazırlığı" diyordu |
| "Taslak olarak kaydet" | ⬜ | Doğrudan kaydetme var |
| Ayrı rotalar (create/upload/detail/[id]) | ↔ | Tek sayfa master-detail ile karşılandı (ayrı route yok) |

---

## DI-VH — Sürüm geçmişi (bu oturum)

**Backend (MngDocument):**
- `GET /resources/markdown/{id}/versions` → sürüm listesi (no, not, boyut, yazar, tarih, güncel mi)
- `GET /resources/markdown/{id}/versions/{versionNumber}` → sürüm içeriği
- `POST /resources/markdown/{id}/versions/{versionNumber}/restore` → eski sürümü **yeni sürüm olarak** geri yükler ("restore from vN")
- Modeller: `DmResourceVersion`, DTO'lar `MarkdownVersionDto` / `MarkdownVersionContentDto`.

**UI:** Doküman görünümünde **"Geçmiş"** butonu → master-detail diyalog: sol sürüm listesi (avatar `vN`, tarih, yazar, "Güncel" rozeti), sağ canlı markdown önizleme; eski sürümlerde **"Bu sürüme dön"**. Kaydetme/geri yükleme sonrası `openDoc` dönen kaynakla tazelenir.

---

## DI-AUDIT — Oluşturan/güncelleyen (bu oturum) — ⚠️ önemli teknik ders

**Kök bulgu:** Bu DG örneği veri kayıtlarında audit'i `__createInfo`/`__lastUpdateInfo` ile **tutmuyor**; audit `__history` dizisinde (`operation`, `userId`, `userEmail`=görünen ad, `timestamp`, `changes`) ve **yalnızca `?showHistory=true` ile** döner. Ayrıca `/query` endpoint'i inclusion projeksiyonunda audit alanlarını döndürmez. **`dm_resource_versions` dataset'inde DG logging KAPALI** → sürüm kayıtlarında hiç `__history` yok.

**Çözüm:**
- `DmResource`/`DmResourceVersion` → `__history` (`DmHistoryEntry`) okur; tüm okuma sorgularına `showHistory=true` eklendi (`ListQuery`, search, `GetByIdAsync`).
- Oluşturan = ilk `create`, son güncelleyen = son `update` girdisinden türetilir (`ToDto`). `ResourceDto.CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` doldurulur.
- Sürüm yazarken **açık** `createdBy` (`_ctx.Username`) + `createdAt` (UTC) gömülür (`WriteVersionAsync`) → yeni sürümler.
- **Eski sürümlerin telafisi:** kaynağın `__history`'sinden `BuildVersionAuditMap` (create→v1; her update'in `changes.currentVersionNumber`'ı hedef sürüm) ile yazar/tarih doldurulur.

**UI:** Doküman başlığı altında **Oluşturan** ve **Son güncelleyen** (isim · tarih) satırı; geçmiş panelinde sürüm bazında yazar/tarih. Locale: `documentIntelligence.metaCreated/metaUpdated`.

**Canlı doğrulama (Doc2):** v1–v3 `serkan meral` + tarih (history telafisi), v4 `odak_admin` (açık audit); doküman header created/updated dolu. ✅

---

## Deploy & ortam

- **Test sunucusu (Odak):** `192.168.20.20`, gateway `:5040`. DG route: `/data/api/v1/...`; MngDocument route: `/documents/api/v1/...`.
- **`mngdocument`:** tüm Faz 1 + VH + AUDIT → **canlı (healthy)**.
- **`mngui`:** VH + audit UI değişiklikleri **deploy edilmedi** (yalnızca yerel `npm run dev`).
- Deploy: `scripts/odak/sync-odak-source.ps1 -Paths <X>` + `scripts/odak/deploy-odak-apps.ps1 -Services <svc>`. Token: `docs/odak/operationcore/scripts/load-operationcore-token.ps1`.

---

## Sıradaki iş (öncelik kullanıcı seçimine bağlı)

1. **UI deploy** (`mngui`) → Faz 1'i test sunucusunda kapatmak.
2. **Yetkilendirme** — grup bazlı klasör yetkisi + miras (Faz 1'in tek büyük boşluğu).
3. **Dosya inline preview** — PDF/görsel/düz metin.
4. (Ops.) `dm_resource_versions` dataset'inde DG logging'i açıp tam audit (IP/değişiklik izi).
