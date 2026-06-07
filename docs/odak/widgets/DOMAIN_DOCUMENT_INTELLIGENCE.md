# Document Intelligence — Widget kütüphanesi kapsamı

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama — implementasyon yok (paralel DI chat ile karışmaması için)  
**İlişkili:** [ARCHITECTURE.md](./ARCHITECTURE.md) · [MANIFEST_SCHEMA.md](./MANIFEST_SCHEMA.md) · [../document_intelligence/DEVAM.md](../document_intelligence/DEVAM.md)

---

## 1. Modül özeti

**Document Intelligence (DI)** MonitraNG’nin kurumsal **kaynak ağacı** modülüdür — klasik DMS + wiki birleşimi.

| Özellik | Durum (Faz 1) |
|---------|----------------|
| Tek kaynak ağacı (klasör / markdown / dosya) | ✅ Canlı |
| Markdown editör, preview, sürüm geçmişi | ✅ |
| Dosya yükleme (MinIO via DG), inline preview | ✅ |
| Grup bazlı yetki + miras (SharePoint benzeri) | ✅ |
| Taslak / yayınla (`draft` / `published`) | ✅ |
| Arama (ad, başlık, markdown içeriği) | ✅ |
| System / Öğreticiler seed içerik | ✅ |
| OperationCore WorkItem ↔ doküman | 🔲 Faz 2 |
| AI özet / benzer doküman / Q&A | 🔲 Faz 3+ |

**UI:** `/apps/document-intelligence` — master-detail tek sayfa  
**Backend:** **MngDocument** (`/documents/api/v1/resources/...`)  
**Kalıcılık:** DG dataset’leri `dm_resources`, `dm_resource_permissions`, `dm_resource_versions`  
**Ana plan:** [MonitraNG_Document_Intelligence_Planning.md](../document_intelligence/MonitraNG_Document_Intelligence_Planning.md)

### 1.1 İçerik ağacı (seed örneği)

```text
(kök)
├── MonitraNG/
│   └── Öğreticiler/          → OC, SIEM kurulum rehberleri
│       └── Manager/          → yönetici rehberleri (kısıtlı ACL)
└── System/
    ├── Sürüm Notları
    └── Diagnostic Raporu
```

Bu içerik **operasyon ekiplerine onboarding** sağlar; widget kütüphanesi bu dokümanlara dashboard’dan kısayol ve özet sunabilir.

### 1.2 Diğer modüllerle ilişki

| Modül | İlişki |
|-------|--------|
| **Operation Core** | Faz 2: WorkItem eklerinde DI’dan doküman seçimi; workspace panelinde ilişkili doküman listesi |
| **MngLLM / Moni** | Markdown/tutorial içeriği chatbot dokümantasyon aramasında (ayrı hat) |
| **Widget kütüphanesi** | Dashboard / welcome / workspace panelinde **okuma odaklı** DI widget’ları |
| **Reporting** | Markdown/dosya listesi snapshot (ileride) |

---

## 2. Widget kütüphanesine neden dahil?

DI verisi “izleme metrikleri” değil; yine de operasyon panellerinde sık ihtiyaç duyulur:

- “Son güncellenen rehberler”
- “Bekleyen taslak dokümanlar”
- “System sürüm notları” hızlı erişim
- Workspace’e bağlı runbook / talimat listesi (Faz 2 sonrası)
- Welcome / modül dashboard’larında **quick-access kartları**

Alarm / SIEM / **MO (Operation Core)** widget’larından farklı olarak DI widget’ları **bilgi erişimi ve yönlendirme** odaklıdır; grafik ağırlığı düşüktür.

---

## 3. Veri kaynağı: DG değil, MngDocument (kilitli planlama kararı)

### 3.1 Neden doğrudan `@dm_resources` queryRef değil?

| Risk | Açıklama |
|------|----------|
| **Yetki bypass** | Klasör ACL + miras `PermissionService` içinde çözülür; ham DG sorgusu bunu atlar |
| **İş kuralları** | Taslak filtresi, `published` görünürlük, tree filtreleme API’de |
| **Binary içerik** | Dosya blob’ları DG/MinIO üzerinden; liste metadata MngDocument DTO |

**Karar (planlama):** DI domain widget’ları **`serviceRef`** ile **MngDocument API** kullanır; birincil yol DG `queryRef` değildir.

Bu, genel widget mimarisinin **istisna değil, genişlemesidir** — bkz. [ARCHITECTURE.md §5.5](./ARCHITECTURE.md#55-serviceref-di-ve-diger-api-kaynaklari).

### 3.2 serviceRef sözleşmesi (taslak)

```
mngdocument:{endpointAlias}
```

| endpointAlias | HTTP | Açıklama |
|---------------|------|----------|
| `resources/search` | GET `/search?q=&folderId=&limit=` | Arama listesi |
| `resources/recent` | GET *(Faz W-DI-0 — henüz yok)* | Son güncellenen yayınlanmış kaynaklar |
| `resources/drafts` | GET *(Faz W-DI-0 — henüz yok)* | Taslak markdown listesi |
| `resources/folder-summary` | GET *(Faz W-DI-0 — henüz yok)* | Klasör altı sayım (folder/file/md) |
| `resources/children` | GET `/children?folderId=` | Belirli klasör içeriği (kısa liste) |

Mevcut API: `tree`, `bootstrap`, `browse`, `search`, `children` — widget’lar **hafif liste** için `search` ve `children` ile başlayabilir; aggregate için MngDocument’e **read-only stats uçları** eklenmesi widget fazına paralel planlanır (**DI chat’inde**, widget chat’inde değil).

### 3.3 UI proxy

Mng.Ui zaten `server/api/documents/[...path].ts` ile MngDocument’e proxy eder. WidgetHost DI verisini **aynı proxy** üzerinden çeker; ayrı widget backend yok (D0 kararı geçerli).

---

## 4. Domain tanımı

Manifest `domain` enum’una eklenir:

```
document-intelligence
```

Kategori önerisi (`@widget_categories` seed):

| name | Açıklama |
|------|----------|
| `di-lists` | Doküman / klasör listeleri |
| `di-kpi` | Sayım kartları |
| `di-quick-access` | Sabit klasör/doküman kısayolları |
| `di-embed` | Markdown snippet / banner |

---

## 5. Öntanımlı widget şablonları (seed backlog)

### 5.1 P0 — Düşük API bağımlılığı (mevcut uçlarla)

| templateId | kind | presentation | serviceRef / parametre | Yüzey |
|------------|------|--------------|------------------------|-------|
| `di.recent-search-list` | list | `list-activity` | `search` — `q=""`, `limit` | dashboard, welcome |
| `di.folder-children-table` | table | `table-compact` | `children` — `folderId` (param) | dashboard, workspace |
| `di.quick-link-banner` | banner | `banner-info` | statik — route `/apps/document-intelligence` | welcome, dashboard |
| `di.tutorial-folder-cards` | card | `stat-simple` × N | statik linkler → Öğreticiler alt klasörleri | welcome |

### 5.2 P1 — MngDocument stats uçları gerekir (DI chat’inde API)

| templateId | kind | Açıklama |
|------------|------|----------|
| `di.recent-updates-list` | list | Son N güncellenen `published` kaynak |
| `di.draft-count-stat` | stat | Taslak markdown sayısı (edit yetkisi) |
| `di.folder-stats-donut` | chart | Seçili klasör: md / file / folder dağılımı |
| `di.system-release-notes-snippet` | banner / embed | System/Sürüm Notları’ndan excerpt + “tam metin” link |

### 5.3 P2 — Operation Core entegrasyonu sonrası

| templateId | kind | Açıklama |
|------------|------|----------|
| `di.workitem-linked-docs` | table | Workspace WorkItem’a bağlı dokümanlar |
| `di.workspace-runbooks` | list | Workspace policy ile sabitlenmiş klasör içeriği |

---

## 6. Presentation preset eşlemesi

| DI ihtiyacı | preset | Bileşen |
|-------------|--------|---------|
| Doküman listesi | `list-activity` / `table-compact` | TableWidget (+ satır → DI detay) |
| Klasör / taslak sayısı | `stat-simple` | StatCard |
| Tip dağılımı | `chart-donut-breakup` | ChartWidget |
| “Rehbere git” | `banner-info` | BannerWidget |
| Markdown alıntı | *(yeni)* `embed-markdown` | Faz 2 — `DiMarkdownViewer` read-only embed |

**Drill-down:** neredeyse tüm DI widget’larında `interactions.drillDown.path = "/apps/document-intelligence"` + `resourceId` / `folderId` query param.

---

## 7. Surface policy notları

| Yüzey | DI widget uygunluğu |
|-------|---------------------|
| `dashboard` | ✅ Ana kullanım |
| `welcome` / ana sayfa modül kartları | ✅ quick-access, sürüm notları |
| `workspace-panel` | ✅ P2 — runbook / linked docs |
| `siem-center` / `alarm-center` | ⚠️ Yalnızca IT wiki / runbook banner (ör. rsyslog rehberi linki) |
| `report` | ✅ Liste snapshot; embed markdown PDF’e dönüşüm ayrı iş |

**Context variables önerisi:**

| Variable | Kullanım |
|----------|----------|
| `folderId` | Klasör scoped liste widget’ları |
| `workspaceId` | OC runbook eşlemesi (Faz 2) |
| `tags` | Arama filtresi |

---

## 8. MngLLM / chatbot ile ayrım

| | Widget | Moni (chatbot) |
|---|--------|----------------|
| Amaç | Görsel panel, tıklanabilir liste/kart | Doğal dil soru-cevap |
| Veri | MngDocument API list/search | MngLLM DocumentationProvider + DI markdown |
| Kullanıcı | Dashboard tüketicisi | Sohbet arayüzü |

Aynı **MonitraNG/Öğreticiler** içeriği her iki kanaldan tüketilir; widget seed’leri sabit klasör/doküman ID’lerine referans verebilir (tenant seed sonrası `folderId` parametresi).

---

## 9. Implementasyon sınırı (bu chat)

| Yapılır (dokümantasyon) | Yapılmaz (diğer chat / sonra) |
|-------------------------|-------------------------------|
| Domain + template backlog | MngDocument yeni endpoint |
| `serviceRef` şema notu | WidgetHost kodu |
| ARCHITECTURE / DEVAM güncelleme | DI Faz 2 WorkItem |
| queryRef vs serviceRef kararı | `@widget_templates` seed |

**DI geliştirme chat’i** Faz 2 ve stats API’yi sahiplenir; **widget chat’i** template seed + WidgetHost DI adapter’ını Faz 1 widget implementasyonunda alır.

---

## 10. Açık sorular (widget planlama)

| # | Soru | Öneri |
|---|------|-------|
| W-DI-1 | Stats uçları MngDocument’te mi, DG aggregate mi? | **MngDocument** — yetki filtresi şart |
| W-DI-2 | `embed-markdown` preset ayrı bileşen mi? | Evet — Faz 2 widget; XSS için mevcut `DOMPurify` yolu |
| W-DI-3 | System/Sürüm Notları otomatik banner? | Seed template + periyodik seed script (DI ops pattern) |

Karar kaydı: [DEVAM.md](./DEVAM.md)
