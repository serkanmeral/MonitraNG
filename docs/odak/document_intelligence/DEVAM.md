# Document Intelligence (MngDocument) — Devam noktası (checkpoint)

**Son güncelleme:** 7 Temmuz 2026 gece (**D-E ✅ · D2 ✅ · sırada D4**)
**Durum:** **Faz P ✅** · **D-BR1 ✅** · **D-META/CREATE/FILE-PREV ✅** · **D-PERF ✅** · **D-E ✅** · **D2 ✅** · **Test deploy ✅**

> ## 🚀 Yeni chat başlangıç prompt'u (kopyala-yapıştır)
>
> ```
> MonitraNG Document Intelligence — devam.
> Repo: c:\Users\monitra\Dev\MonitraNG\MonitraNG
>
> Önce oku:
> - docs/MngDocument/current_status.md
> - docs/odak/document_intelligence/DEVAM.md
> - docs/odak/document_intelligence/DI_PRODUCT_ROADMAP.md
>
> Durum (7 Tem 2026 gece):
> - D-META / D-CREATE / D-FILE-PREV ✅ (c5880739) — prod smoke 9/9
> - D-PERF lazy tree + permission cache + pagination ✅ (dff51a2c)
> - D-E1–E3 + döküman kilidi ✅
> - D2 döküman sürüm UX ✅ — changeNote, kapat-kaydet-not, PostMessageOrigin
> - Test deploy: mngdocument + mngui @ 192.168.20.20
> - Sırada: **D4** (manuel üretim / merge / PDF) → D-BR2 → CoC smoke
>
> Test: 192.168.20.20:5040 | Prod: 192.168.20.8:5040
> Token prod: load-operationcore-token-prod.ps1
> Smoke: scripts/tests/MngDocument/smoke-di-meta-create-preview-prod.ps1
> ```

> **⭐ KALDIĞIMIZ YER (7 Tem 2026 gece):**
> - **D-E + D2** tamamlandı — editör oturum/kilitleme + Collabora sürüm UX (kaydet, not, kapat akışı)
> - Test ortamına `mngdocument` + `mngui` deploy edildi
> - Sırada **D4** (manuel şablondan üretim, merge, PDF), sonra **D-BR2**, CoC smoke
> - Roadmap §25: [DI_PRODUCT_ROADMAP.md](./DI_PRODUCT_ROADMAP.md)

**Ana plan:** [MonitraNG_Document_Intelligence_Planning.md](MonitraNG_Document_Intelligence_Planning.md) · **Ürün roadmap (birleşik):** [DI_PRODUCT_ROADMAP.md](DI_PRODUCT_ROADMAP.md) · **Antet prod migration:** [LETTERHEAD_CATALOG_MIGRATION_PROD.md](LETTERHEAD_CATALOG_MIGRATION_PROD.md) · **MO vs Workflow (Odak):** [ODAK_MO_VS_WORKFLOW_SCENARIOS.md](../workflow/ODAK_MO_VS_WORKFLOW_SCENARIOS.md) · **Prod / taşıma:** [PROD_OPERATIONS_AND_MIGRATION.md](PROD_OPERATIONS_AND_MIGRATION.md)

---

## D1-PAGESTRUCTURE — Sayfa yapısı + footer tablo (26 Haziran 2026)

**Amaç:** Antet/altbilgi/kenar boşluklarını tek “Sayfa yapısı” panelinde yönetmek; footer’ı header gibi **2 sütunlu tablo** ile Collabora’da hizalanabilir yapmak; belge adını `{{documentName}}` parametresi olarak kullanmak.

**Backend:**
| Bileşen | Açıklama |
|---------|----------|
| `PUT /templates/{id}/page-structure` | `pageLayout` + `letterhead` + `footer` |
| `PageLayoutInjector` | `sectPr` margin / header-footer distance (ODK referans twips) |
| `FooterInjector` | Tablo tabanlı altbilgi (form revizyon + ofis sütunları) |
| `LetterheadInjector` | 3 sütun antet; orta sütun `{{documentName}}` |
| `TemplateModelSerializer` | schema **1.3**, `EnsureLetterheadParameters` / `documentName` |

**UI:** `DiTemplatePageStructureForm.vue`, `diPageLayout.ts`, designer diyalogları → `diUpdateTemplatePageStructure`.

**Prod (26 Haz):**
```powershell
.\scripts\odak\sync-odak-prod.ps1 -PathsCsv "MngDocument,Mng.Ui,docs/odak/document_intelligence"
.\scripts\odak\deploy-odak-prod.ps1 -Services "mngdocument,mngui" -NoCache
.\docs\odak\document_intelligence\scripts\update-coc-template-prod.ps1 -SkipParameterize
```
- COC-STANDARD id: `a5a7c41f-47b7-4cc1-920b-3d485874c362` · 18 placeholder · `pageLayout` ODK defaults.

**Bilininen:** Scanner uyarısı — `docNo` XML run bölünmesi (17–18 key tanınıyor, fonksiyonel).

---

## D1-DESIGNER — Belge tasarımcısı (D1-alpha, 23 Haziran 2026)

**Amaç:** "From Template" akışının ilk dilimi — mevcut DOCX kaynağından şablon oluşturma, paragraf seçimi, parametre kaydı. Sample referans: `docs/odak/document_intelligence/sample/ODK-COC-23-202.docx`, `ODK-COC-B-23-109.docx` (antetli, merge field yok).

**Dataset:** `dm_document_templates` — `documentintelligence_datasets_phase1.json` + `setup-document-intelligence-datasets.ps1` (Odak'ta ✅).

**Backend (MngDocument) — API** (`/documents/api/v1/templates`, `[Authorize]`):

| Metot | Uç | Açıklama |
|-------|-----|----------|
| GET | `/templates` | Şablon listesi |
| GET | `/templates/{id}` | Detay + parametreler |
| POST | `/templates/from-source` | DOCX kaynaktan şablon oluştur |
| GET | `/templates/source/{resourceId}/structure` | OOXML paragraf parse |
| PUT | `/templates/{id}/parameters` | Parametre kaydı |

**Yeni/ değişen dosyalar (backend):**
- `MngDocument/.../Controllers/DocumentTemplatesController.cs`
- `MngDocument/.../Services/DocumentTemplateService.cs`, `DocxStructureParser.cs`
- `MngDocument/.../Contracts/Templates/*`, `Models/DmDocumentTemplate.cs`
- `DmDatasets.DocumentTemplates`, `IMngDataGatewayClient.DownloadFileAsync`

**UI (Mng.Ui — yerel, deploy bekliyor):**
- `pages/apps/document-intelligence/designer/index.vue` — liste, paragraf seçimi, parametre ekleme
- `services/documentIntelligenceService.ts` — `diListTemplates`, `diGetTemplate`, `diCreateTemplateFromSource`, `diGetDocxStructure`, `diUpdateTemplateParameters`
- `types/apps/documentIntelligence.ts`, `utils/locales/tr.json` + `en.json` (`documentIntelligence.designer.*`)
- Ana sayfa: `document-intelligence/index.vue` → "Belge tasarımcısı" butonu

**Parametre modeli (kayıt anı):** `parameters[]` — `valueSource.mode`: `manual`, `incremental` (format string, örn. `{0:D3}`, `{yy}`); runtime numara üretimi **henüz yok** (D2).

**Build:** `dotnet build MngDocument.sln -c Release` ✅ (local).

**Odak deploy (23 Haz):**
```powershell
pwsh -File .\scripts\odak\sync-odak-source.ps1 -Server 192.168.20.20 -Paths @('MngDocument','ApplicationResources/mng_apps','docs/odak/document_intelligence')
pwsh -File .\scripts\odak\deploy-odak-apps.ps1 -Server 192.168.20.20 -Services mngdocument -NoCache
$env:DI_TOKEN = (Get-Content "$env:TEMP\operationcore_dg_token.txt" -Raw).Trim()
.\docs\odak\document_intelligence\scripts\setup-document-intelligence-datasets.ps1
```

**Smoke:** `scripts/tests/MngDocument/smoke-templates-odak.ps1` (token dosyasından).

**Bilinçli kapsam dışı (sonraki dilimler):**
- D2: DG `@__counters` ile runtime incremental numara
- D3: tablo/liste parametreleri (CoC-B boyama tabloları)
- D4: DOCX merge + PDF indirme
- D5: OperationCore work item bağlantısı (Faz 2 kısmen mevcut: `ResourceLinkService`, `OcWorkItemDocumentsTab`)
- Şablona SDT/content control enjekte (merge anında)

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
| i18n (tr/en) | ✅ | `documentIntelligence.*` (+ `permissions.*`) |
| **Grup bazlı klasör yetkileri** | ✅ | `dm_resource_permissions`; grup adı eşleştirme; açık varsayılan; UI izin editörü (bu oturum) |
| **Yetki mirası** (kır/geri yükle) | ✅ | ACL anchor (`permissionsBroken`); en yakın anchor zinciri; backend zorlama + tree filtreleme (bu oturum) |
| **Dosya inline preview** (PDF/görsel/metin) | ✅ | `DiFilePreviewDialog` (img/iframe/text); `diFilePreview` util; satır tıkla/önizle butonu |
| **"Taslak olarak kaydet"** | ✅ | `dm_resources.status` (draft/published); oluşturma + editörde taslak/yayınla butonları; taslak rozeti (bu oturum) |
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

## DI-PERM — Grup bazlı klasör yetkilendirmesi + miras (bu oturum)

**Model (SharePoint benzeri ACL anchor):**
- `dm_resources`'a `permissionsBroken` (bool). false/yok = üstten miras; true = kendi ACL'i olan **anchor**.
- Yeni `dm_resource_permissions` dataset'i (`logging: self` → izin audit'i `__history`): `resourceId` (anchor klasör), `groupId` (görsel), `groupName` (**eşleştirme anahtarı**, JWT `user_groups` ↔), `permissions[]` (verilen aksiyonlar). DG'de **oluşturuldu**.
- Aksiyonlar (8): `view, create, edit, delete, upload, download, move, share` (`share` modelde/UI'da var, henüz gate etmiyor — Faz 2 link paylaşımı için).
- **Etkin yetki:** kaynak R'nin kendisi + `ancestorIds` tabandan yukarı en yakın anchor bulunur; izin kayıtları kullanıcının gruplarıyla eşleştirilir. Zincirde anchor yoksa → **açık varsayılan** (tüm aksiyonlar serbest). **Admin** (`IRequestContext.IsAdmin`) → bypass. **Manager** (`IsManager`, JWT): mirası kırık anchor altında `view` yetkisi varsa → tam yetki (Manager klasörü menü/CRUD).

**Backend (MngDocument):**
- `IPermissionService` + `PermissionService` + `PermissionSnapshot` (tüm klasör + izin kayıtları tek seferde yüklenip bellekte çözüm).
- `ResourceService` tüm okuma/yazma yollarında `EnsureCan`/filtreleme: tree/children/search **view filtreleme**; create→`create`/upload→`upload` (parent), edit/rename/restore→`edit`, move→`move`+hedefte `create`, delete→`delete` (+ klasör silinince izin kayıtları temizlenir). `ResourceDto.Permissions` (etkin yetki) → UI gating tek çağrıda.
- Uçlar: `GET /resources/{id}/permissions`, `PUT /resources/{id}/permissions`, `POST /resources/{id}/permissions/break-inheritance`, `POST .../restore-inheritance`. İzin yönetimi: admin ya da etkin `share`.
- **Kilitlenme koruması:** açık klasörde mirası kıran admin-olmayan kullanıcının kendi gruplarına tam yetki tohumlanır.

**UI (`mngui`, yerel):** `DiPermissionsDialog.vue` (grup × aksiyon matrisi, miras durumu, kır/geri yükle, kaydet) + `index.vue` üst bar/menülerde **"İzinler"** butonu ve etkin yetkiye göre **buton gating** (yeni klasör/doküman/yükle/düzenle/sil/indir/taşı). Servis: `diGetPermissions/diSetPermissions/diBreakInheritance/diRestoreInheritance`. Locale: `documentIntelligence.permissions.*` (tr/en).

**Canlı doğrulama (Odak, admin):** izin yaşam döngüsü — open default → break (anchor=self) → PUT (admins grubu view/edit/create/download) → GET (kalıcı) → restore (kayıtlar temizlendi). Admin etkin yetki Full (bypass). ✅

**Düzeltme (gösterim hatası):** İzin editöründe **"tanımlanan izinler listede görünmüyor"** raporlandı. Kök neden UI: matris satırları yalnızca `filteredGroups` (MngKeeper) üzerinden render ediliyordu; `authStore.isAdmin` JWT `is_admin` claim'inden türediğinden (grup üyeliği değil), token'da `is_admin` yoksa **`admins` grubu listeden çıkıyor** → o gruba verilen izin DG'de olsa bile görünmüyor. Backend PUT→GET round-trip'i canlı doğrulandı, **doğru** çalışıyordu. Çözüm (`DiPermissionsDialog.vue`, salt UI): (1) yeni `displayGroups` = **keeper grupları ∪ izin kaydı olan gruplar** → her kayıtlı grup her zaman görünür; (2) keeper grup id eşlemesi `groupId`'yi okuyacak şekilde düzeltildi (eskiden boş gidiyordu); (3) checkbox değişiminde `matrix` nesnesi yeniden atanarak reaktivite garanti edildi. Not: artık admin-olmayan kullanıcı da kayıtlı `admins` iznini **görebilir** (gerekirse o satırlar salt-okunur yapılabilir).

**Açık iş:** non-admin kullanıcıyla tree/children **filtreleme** + 403 canlı doğrulaması yapılmadı (admin bypass nedeniyle).

---

## DI-PREVIEW — Dosya inline önizleme (bu oturum)

**Amaç:** Faz 1 "dosya görüntüleme için temel preview hazırlığı" maddesi. Yüklenen dosyalar için indirmeden satır içi önizleme.

**UI (`mngui`, yerel):**
- `utils/diFilePreview.ts`: uzantı→tür (`image`/`pdf`/`text`/`none`) + MIME eşlemesi + boyut tavanı (görsel/PDF 25 MB, metin 2 MB). `isDiPreviewable`, `diPreviewKind`, `diPreviewMime`.
- `DiFilePreviewDialog.vue`: görsel `<img>`, PDF `<iframe>`, metin `<pre>` (blob.text); OperationCore `OcAttachmentPreviewDialog` deseninden uyarlandı. DG octet-stream dönerse blob doğru MIME ile yeniden sarılır. Mevcut `diFetchFileBlob` kullanılır (binary MngDocument'ten geçmez, DG'den).
- `index.vue`: önizlenebilir dosya satırına tıklayınca (veya **göz** ikonu/menü "Önizle") diyalog açılır; değilse indirir. Önizleme `canDownload` ile gate'li. Diyalogdaki "İndir" mevcut `downloadFile`'a bağlanır.
- Locale: `documentIntelligence.preview` / `previewUnavailable` / `errors.preview` (tr/en).

**Not:** Salt UI; backend değişmedi. Office (docx/xlsx/pptx) ve zip için inline önizleme yok (indir) — tarayıcı yerel render etmediğinden bilinçli kapsam dışı.

---

## DI-DRAFT — Taslak olarak kaydet (bu oturum)

**Amaç:** Faz 1 "taslak olarak kaydet" maddesi. Markdown dokümanları taslak/yayınlanmış durumuyla işaretlenir.

**Model:** `dm_resources.status` (text; `draft`/`published`). Yok/eski kayıt = `published` (geriye dönük). `ResourceStatus.Normalize`. Yalnızca markdown için anlamlı. Dataset şemasına `status` alanı eklendi (`forceSchema:false` → re-provision zorunlu değil).

**Backend (MngDocument, canlı):**
- `CreateMarkdownRequest.IsDraft` (bool) → create payload `status`. `UpdateMarkdownRequest.IsDraft` (bool?) → **null ise mevcut durum korunur**, true=draft, false=publish.
- `ResourceDto.Status` (default `published`); `ToDto` → `ResourceStatus.Normalize`.

**UI (`mngui`, yerel):**
- Oluşturma diyaloğu: **"Taslak olarak kaydet"** + **Oluştur** butonları (`submitDoc(asDraft)`).
- Editör (düzenleme): **"Taslak olarak kaydet"** + **Kaydet/Yayınla** (taslaksa "Yayınla") butonları (`saveEdit(asDraft)`).
- **Taslak rozeti** (warning chip): doküman listesinde ve açık doküman başlığında.
- Tip/servis: `DiResource.status`, `DiCreate/UpdateMarkdownRequest.isDraft`, `mapResource` status.
- Locale: `documentIntelligence.saveAsDraft/draft/publish/published/draftSaved` (tr/en).

**Canlı doğrulama (Odak, admin):** create=draft → publish=published → back-to-draft=draft → bayraksız update **durumu korur**. ✅

---

## DI-PERF — İlk açılış / gezinme hızlandırma (3 Haziran 2026)

**Sorun:** Az kayıtta bile sayfa yavaş; her API çağrısı `PermissionSnapshot` için tüm klasörler + izinler DG'den (`showHistory=true`) çekiliyordu; UI ilk açılışta `tree` + `children` paralel → çift snapshot.

**Çözüm (MngDocument + mngui):**
- `PermissionService`: HTTP isteği başına snapshot önbelleği; snapshot sorgularında `showHistory=false`; mutasyonlarda `InvalidateSnapshotCache()`.
- `GET /resources/bootstrap?folderId=` → ağaç + içerik (+ isteğe bağlı breadcrumb/seçili klasör), tek snapshot.
- `GET /resources/browse?folderId=` → gezinme paketi (ağaç hariç), tek snapshot.
- UI: `onMounted` → `diGetBootstrap`; `selectFolder` → `diGetBrowseContext`; yenileme → `refreshWorkspace` / `refreshListing`.

**Diagnostic:** `docs/odak/diagnostic/scripts/diagnostic-document-intelligence-pages.ps1` (bootstrap vs eski paralel karşılaştırma).

---

## DI-SYSTEM — System klasörü + öğreticiler + manager bypass (5 Haziran 2026)

**Document Intelligence içerik ağacı (prod + test seed):**

```
(kök)
├── MonitraNG/
│   └── Öğreticiler/
│       ├── Operasyon Merkezi — Kullanıcı Rehberi
│       ├── IT ve Güvenlik/
│       │   └── Güvenlik Merkezi — Linux rsyslog kurulumu
│       └── Manager/                    (MonitraNG Users — view+download)
│           └── Operasyon Merkezi — Yönetici Rehberi
└── System/                             (MonitraNG Users — view+download)
    ├── Sürüm Notları
    └── Diagnostic Raporu
```

**İçerik (repo):**
- `docs/odak/document_intelligence/tutorials/` — OC kullanıcı/yönetici rehberleri (Haziran 2026 güncel)
- `docs/odak/document_intelligence/tutorials/guvenlik-merkezi-linux-rsyslog-kurulumu.md` — SIEM Linux rsyslog (IT wiki)
- `docs/odak/document_intelligence/system/surum-notlari.md` — modül changelog + platform baseline (TR)
- `docs/odak/document_intelligence/system/diagnostic-raporu.md` — prod API diagnostic + metodoloji (IT)

**Seed script'leri:**
- `scripts/seed-monitrang-tutorials.ps1` — MonitraNG/Öğreticiler/Manager
- `scripts/seed-siem-it-guides.ps1` — IT ve Güvenlik / Linux rsyslog (test + prod)
- `scripts/seed-system-release-notes.ps1` — System/Sürüm Notları
- `scripts/seed-system-diagnostic-report.ps1` — System/Diagnostic Raporu

**Manager menü sorunu (DI):** Kısıtlı klasörlerde yalnızca `view`+`download` → satır menüsü boş görünüyordu. **Çözüm (MngDocument):** `IRequestContext.IsManager` + `PermissionSnapshot`: mirası kırık anchor altında `view` yetkisi olan manager → tam yetki (admin bypass ile aynı mantık, kapsam dar). **Deploy:** `mngdocument` prod + test (5 Haz).

**Müşteri raporlama:** Diagnostic sonuçları periyodik olarak prod'da script koşulur → `diagnostic-raporu.md` güncellenir → `seed-system-diagnostic-report.ps1`. Detay: `docs/odak/diagnostic/DEVAM.md`.

---

## Deploy & ortam

- **Üretim:** `192.168.20.8`, gateway `:5040`, UI `:3000`, WOPI `:5095`, Collabora `:9980`.
- **Test sunucusu (Odak):** `192.168.20.20`, gateway `:5040`.
- **`mngdocument` + `mngui` + `gotenberg` + `collabora`:** ✅ prod.
- **Belge tasarımcısı:** ✅ prod — antet katalog, COC/LINE-ACTIVITY şablonları, Collabora WOPI, logo fix.
- **Sync prod:** `scripts/odak/sync-odak-prod.ps1` · **Deploy:** `scripts/odak/deploy-odak-prod.ps1`
- **Token prod:** `docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1`

---

## Logo / header medya onarımı (7 Tem 2026)

| Belirti | Kök neden | Çözüm (commit `b11fccd4`) |
|---------|-----------|---------------------------|
| Collabora «Başlatılıyor...» | WOPI GetFile 401 — antet lookup Bearer yerine session token gerekir | `TemplateEditorService` → `_dg` + session token |
| Şablonda logo var, üretimde «LetterheadLogo» | Storage'da header XML var, `word/media/*` yok; üretim WOPI onarımını kullanmıyordu | `EnsureHeaderWithMediaFromDesign` + `DocumentGenerationService` |

**Not:** Daha önce üretilmiş DOCX dosyaları otomatik düzelmez; yeniden generate edilmelidir.

---

## Faz P — Sayfa UX (6–7 Temmuz 2026) ✅

**Commit:** `1441ac90` — keşif, changeNote, backlink, etiket UI, alan giriş sayfası, sürüm geçmişi; aramada taslak hariç.

**Backend (MngDocument):**
- `changeNote` (versiyon kaydına not)
- `GET .../markdown/{id}/backlinks`
- `GET .../recent`, `GET .../drafts` (yalnızca yayınlanmış / taslak)
- Arama: markdown taslakları hariç

**UI (Mng.Ui):**
- `DiDiscoveryHome` — keşif ana ekranı (son, taslak, alan kısayolları, arama)
- `DiMarkdownEditor` — split önizleme, sayfa şablonları, tablo, görsel yükleme, iç link picker
- `DiResourceTagsEditor`, `DiBacklinksPanel`, `DiSavePageDialog` (changeNote)
- `DiAreaIndexBanner` — Sayfalar / Dökümanlar alan giriş sayfası
- **Kaldırıldı:** WYSIWYG «Zengin» editör modu

**Erteli (Faz P+):** Sayfa yorumu, izle/bildirim.

**Prod checklist:** [LETTERHEAD_CATALOG_MIGRATION_PROD.md §11](./LETTERHEAD_CATALOG_MIGRATION_PROD.md)

---

---

## BUGUN-2026-07-07 — D-META · D-CREATE · D-FILE-PREV ✅

**Detay:** [DI_PRODUCT_ROADMAP.md §24](./DI_PRODUCT_ROADMAP.md) · **Commit:** `c5880739` (özellikler) · **D-PERF:** `dff51a2c`

### Kararlar

| Konu | Karar |
|------|--------|
| Sayfa / Döküman / Dosya | UI `origin` + extension ile türetilir |
| Yüklenen docx | **Dosya** — Collabora yok, PDF preview (Gotenberg) |
| Native docx | **Döküman** — antet + kod; Collabora edit |
| Döküman lifecycle | **Minimal** — taslak/yayın yalnızca Sayfa; docx → sürüm geçmişi; **Faz M** ertelendi |
| `documentNo` (#16) | Domain geneli benzersiz ✅ |
| Create → Collabora (#17) | `r/[id]` otomatik editör ✅ |
| Üretim dialog antet | Yok (şablonda `defaultLetterheadId`) |

### Prod smoke (7 Tem akşam)

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
.\scripts\tests\MngDocument\smoke-di-meta-create-preview-prod.ps1
```

9/9 geçti. **Not:** Karmaşık harici MS Word docx Gotenberg’de dönüşmeyebilir; DI minimal docx OK.

### Prod operasyon

- **`dm_tags` dataset** prod’da eksikti → `setup-document-intelligence-datasets.ps1 -BaseUrl http://192.168.20.8:5040`
- **`mngdocument` + `mngui`** deploy tamamlandı

---

## D-PERF — Performans (7 Temmuz 2026) ✅

**Commit:** `dff51a2c`

| ID | Özet |
|----|------|
| D-PERF-1 | Lazy tree: `tree/roots`, `tree/children`, `tree/path`, `tree/search` |
| D-PERF-1b | Klasör picker arama (`DiFolderPickerList`) |
| D-PERF-2 | Permission snapshot domain TTL cache; sayfalı ACL yükleme |
| D-PERF-3 | Bootstrap / browse / children `skip/limit` pagination; UI footer |

**Prod indeks:** `patch-dm-resources-indexes.ps1` (`idx_parentId_type`, vb.)

---

---

## D-E — Editör oturumları ve döküman kilidi (7 Temmuz 2026) ✅

**Hedef:** Collabora limitlerine çarpmadan önce WOPI oturumlarını saymak, yönetmek ve eşzamanlı düzenlemeyi kontrol etmek.

### Backend (MngDocument — test deploy ✅)

| Bileşen | Açıklama |
|---------|----------|
| `EditorLimitsSettings` | 18 bağlantı / 9 döküman / kullanıcı başına 3 / 30 dk idle |
| `EditorSessionService` | `BeginSession`, `EndSession`, `GetStats`, limit gate (429) |
| `EditorSessionsController` | `stats`, `{token}/end`, revoke |
| `EditorLockSettings` | Uyarı, sert kilit, manager bypass, aynı kullanıcı çift sekme engeli |
| `GET …/editor-lock-status` | Aktif düzenleyiciler + kilit bayrakları |
| `GET …/editor-session` | `readOnly`, `bypassLock` query; sunucu tarafı kilit zorlaması |

### UI (Mng.Ui — lokal dev, deploy bekliyor)

| Bileşen | Açıklama |
|---------|----------|
| `DiEditorSessionsPanel` | Toolbar chip, modal (liste, yenile, revoke), poll + broadcast |
| `useDiEditorSessionCleanup` | `pagehide` keepalive `end` |
| `editor/resource/[id].vue` | Tam ekran editör (yeni sekme) |
| `DiEditorLockDialog` | Başka kullanıcı / aynı sekme uyarısı; salt okunur / bypass |
| `diEditorSessionBroadcast` | Sekmeler arası oturum değişikliği |

### Doğrulama

- Test: `192.168.20.20:5040` — `mngdocument` deploy (7 Tem akşam ×2)
- UI: lokal `npm run dev` → **7 Tem gece:** test `mngui` deploy (D-E + D2)

---

## D2 — Döküman sürüm UX (7 Temmuz 2026 gece) ✅

**Hedef:** Native DOCX için Collabora kayıt → yeni sürüm, sürüm notu, geçmiş UX; kapatırken kaydet akışında da not modalı.

### Backend (MngDocument)

| Bileşen | Açıklama |
|---------|----------|
| `PATCH /resources/{id}/versions/{n}` | Sürüm `changeNote` güncelleme |
| `UpdateFileVersionChangeNoteAsync` | `ResourceService` |
| WOPI `PostMessageOrigin` | Oturum bazlı + statik config; `CheckFileInfo` |
| `WopiCollaboraHelper` | Collabora postMessage origin çözümlemesi |

### UI (Mng.Ui)

| Bileşen | Açıklama |
|---------|----------|
| `useDiEditorVersionWatch` | Kayıt sonrası sürüm poll; paylaşımlı save-check promise (race fix) |
| `DiSaveVersionNoteDialog` | Kayıt toast + sürüm notu girişi |
| `DiEditorCloseConfirmDialog` | Kaydet / kaydetmeden kapat / iptal |
| `useDiEditorCloseGuard` | Modified algısı; kapat-kaydet → not → kapat |
| `DiResourceEditorDialog` | Toolbar `vN`, Geçmiş, Collabora entegrasyonu |
| `r/[id].vue` | DOCX detay sayfası; `?edit=1` otomatik editör |
| `editor/resource/[id].vue` | Tam ekran editör (yeni sekme) |
| `diGetResourceEditorSession` | `postMessageOrigin=window.location.origin` |

### Doğrulama

- [x] Collabora kayıt → toast + sürüm notu modalı
- [x] Kapat → Kaydet → not modalı → editör kapanır
- [x] Sürüm geçmişi önizle / geri yükle
- [x] `changeNote` PATCH kalıcı

**Teknik not:** `documentSaved` ile `finishCloseAfterSave` aynı anda `checkVersionAfterSave` çağırıyordu; ikinci çağrı erken çıkıp editörü kapatıyordu → paylaşımlı promise ile düzeltildi.

---

## Sıradaki iş (genel backlog)

1. ~~**D-META + D-CREATE + D-FILE-PREV**~~ ✅
2. ~~**D-PERF-1/2/3**~~ ✅
3. ~~**D-E1–E3 + kilitleme**~~ ✅
4. ~~**D2** — döküman sürüm UX~~ ✅
5. **D4** — merge/PDF, manuel üretim UX ← **sıradaki**
6. **D-BR2** — kapak sayfası kataloğu
7. **CoC/Activity uçtan uca smoke**
8. **D-N1** — `document.generated` bildirim maili
9. **Faz M** — döküman lifecycle (onaylı yayın, arşiv)
10. **Sprint B** — `dm_document_context_types` dataset
11. **D-E4** — Redis WOPI store (opsiyonel)

**Son commit (DI):** D-E + D2 — editör oturum/kilitleme + sürüm UX (7 Tem gece)
