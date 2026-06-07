# Document Intelligence (MngDocument) — Devam noktası (checkpoint)

> ## 🚀 Yeni chat başlangıç prompt'u (kopyala-yapıştır)
>
> ```
> MonitraNG / Document Intelligence (MngDocument) modülünde çalışıyoruz.
> Repo: c:\Users\monitra\Dev\MonitraNG\MonitraNG
>
> Başlamadan önce şu checkpoint dosyasını oku ve bana kısa bir "kaldığımız yer" özeti ver:
> docs/odak/document_intelligence/DEVAM.md
> (Detaylı plan: docs/odak/document_intelligence/MonitraNG_Document_Intelligence_Planning.md)
>
> DURUM: Faz 1 TAMAMLANDI ve test sunucusuna (Odak) deploy edildi. Tüm özellikler canlı:
> resources/tree, klasör CRUD, markdown editör/preview/sürüm geçmişi, dosya yükle/indir/inline
> önizleme, arama, audit, grup bazlı yetkilendirme + miras, taslak/yayınla (draft/published).
> Hem mngdocument hem mngui Odak'ta healthy.
>
> ÇALIŞMA KURALLARI:
> - Yanıtlar Türkçe.
> - Backend (MngDocument vb.) değişiklikleri sormadan otomatik deploy edilebilir.
> - UI (mngui) deploy'u YALNIZCA ben açıkça isteyince yapılır.
> - npm run dev'i ben yerelde (port 3000) çalıştırıyorum; sen yeni bir dev süreci başlatma.
>
> ORTAM / DEPLOY:
> - Test sunucusu (Odak): 192.168.20.20, gateway :5040.
>   DG route: /data/api/v1/...  ·  MngDocument: /documents/api/v1/...
> - Deploy: scripts/odak/sync-odak-source.ps1 -Paths <X> + scripts/odak/deploy-odak-apps.ps1 -Services <svc>
>   Token: docs/odak/operationcore/scripts/load-operationcore-token.ps1 (süresi dolarsa yenile).
>
> SIRADAKİ İŞ (bana seçtir):
> 1. Faz 2 — OperationCore entegrasyonu: WorkItem ↔ doküman ilişkisi (çift yönlü, yetki kontrollü).
> 2. Non-admin canlı doğrulama: gerçek (admin olmayan) kullanıcıyla tree/children filtreleme + 403
>    testi (Faz 1'de admin bypass nedeniyle yapılmadı).
> 3. (Ops.) dm_resource_versions dataset'inde DG logging'i açıp tam audit (IP/değişiklik izi).
>
> DEVAM.md'yi okuyup özetle ve hangi seçenekle devam edeceğimi sor.
> ```

**Son güncelleme:** 5 Haziran 2026 (**System dokümanları** — Öğreticiler, Sürüm Notları, Diagnostic Raporu; manager bypass; prod seed)
**Durum:** **🎉 Faz 1 TAMAMLANDI ✅** — tüm özellikler (resources/tree/markdown/dosya/arama/audit/sürüm geçmişi + yetkilendirme + miras + inline preview + taslak) uçtan uca **canlıda** (`mngdocument` + `mngui` deploy edildi). Sıradaki: Faz 2 (OperationCore entegrasyonu).

> **⭐ KALDIĞIMIZ YER (1 Haz ~21:00) — yeni chat buradan devam edecek:** **Faz 1 kapatıldı.** Bu turda **"taslak olarak kaydet"** eklendi: `dm_resources.status` (draft/published), `CreateMarkdownRequest.IsDraft` + `UpdateMarkdownRequest.IsDraft?` (null=koru), `ResourceDto.Status`; UI'da oluşturma + editörde **taslak/yayınla** butonları ve **taslak rozeti**. **Hem `mngdocument` hem `mngui` Odak'a deploy edildi ve canlı** (gateway 200, ui :3000 200; backend draft yaşam döngüsü doğrulandı). Faz 1'in tüm özellikleri (resources/tree/markdown/dosya/arama/audit/VH + PERM + miras + PREVIEW + DRAFT) artık test sunucusunda. **Sıradaki: Faz 2 — OperationCore WorkItem ↔ doküman ilişkisi.** (Ops.: non-admin canlı filtreleme/403 doğrulaması yapılmadı — admin bypass.)

**Ana plan:** [MonitraNG_Document_Intelligence_Planning.md](MonitraNG_Document_Intelligence_Planning.md) · **Faz 1 dataset'leri:** [datasets/documentintelligence_datasets_phase1.json](datasets/documentintelligence_datasets_phase1.json) · **Widget kütüphanesi kapsamı (planlama):** [../widgets/DOMAIN_DOCUMENT_INTELLIGENCE.md](../widgets/DOMAIN_DOCUMENT_INTELLIGENCE.md)

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

- **Üretim:** `192.168.20.8`, gateway `:5040`.
- **Test sunucusu (Odak):** `192.168.20.20`, gateway `:5040`. DG route: `/data/api/v1/...`; MngDocument route: `/documents/api/v1/...`.
- **`mngdocument`:** tüm Faz 1 + VH + AUDIT + **PERM** + **DRAFT** → **canlı (healthy)** (1 Haz ~20:52 yeniden deploy).
- **`mngui`:** VH + audit + **PERM** (+ gösterim düzeltmesi) + **PREVIEW** + **DRAFT** → **canlı (healthy)** (1 Haz ~20:57 deploy; ui :3000 = 200).
- **DG dataset:** `dm_resource_permissions` Odak DG'de oluşturuldu (`setup-document-intelligence-datasets.ps1`).
- Deploy: `scripts/odak/sync-odak-source.ps1 -Paths <X>` + `scripts/odak/deploy-odak-apps.ps1 -Services <svc>`. Token: `docs/odak/operationcore/scripts/load-operationcore-token.ps1`.

---

## Sıradaki iş (öncelik kullanıcı seçimine bağlı)

1. **Faz 2 — OperationCore entegrasyonu**: WorkItem ↔ doküman ilişkisi (çift yönlü, yetki kontrollü).
2. **Non-admin canlı doğrulama** — gerçek (admin olmayan) kullanıcıyla tree/children filtreleme + 403.
3. (Ops.) `dm_resource_versions` dataset'inde DG logging'i açıp tam audit (IP/değişiklik izi).
4. (Ops.) Diagnostic: JSON → markdown otomasyon script'i; müşteri rapor periyodu tanımı.
