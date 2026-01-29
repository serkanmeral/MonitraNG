# MkDocs Yapılandırması — Backend Servisleri ile Başlama Planı

**Tarih:** 26 Ocak 2026  
**Amaç:** MkDocs'u baştan ele alırken önce backend servislerinin dokümantasyonunu net bir yapıya kavuşturmak.

---

## 1. Kapsam: Hangi servisler "backend"?

Aşağıdaki servisler **backend** kabul edilir (DOCUMENTATION_STANDARDS §3.5 ile uyumlu):

| Servis | Açıklama (kısa) |
|--------|------------------|
| **MngKeeper** | Domain/tenant, auth, lisans yönetimi |
| **MngDataGateway** | Veri/ dataset API'leri |
| **MngHub** | Event/mesajlaşma merkezi |
| **MngGateway** | Tek giriş noktası, yönlendirme |
| **MngReactor** | Reaksiyon/ workflow motoru |
| **MngEngine** | İş mantığı/ motor |
| **MngNotifier** | Bildirim (mail vb.) |
| **MngScheduler** | Zamanlanmış işler |
| **MngLLM** | LLM/ chatbot servisi |
| **MngAdmin** | Admin/ backup vb. |

**Bu planda:** Mng.Ui, MngDomainUI, MngMobile **dahil değildir**; backend yapısı oturduktan sonra ayrı bir "Frontend / UI" planıyla ele alınabilir.

---

## 2. Mevcut durum özeti

- **İçerik:** Backend'ler için `docs/content/{ServiceName}/main/` ve `support/` yapısı zaten var; DOCUMENTATION_STANDARDS ile uyumlu.
- **Nav:** `mkdocs.yml` içinde "Services" altında 10+ servis, her birinde onlarca satır ve derin hiyerarşi var; okunması ve güncellenmesi zor.
- **API:** `api/overview.md` + `api/mngkeeper/`, `api/mngdatagateway/`, `api/mnghub/` mevcut; backend'lerle ilişki net değil.

Bu plan, **sadece backend** için nav'ı sadeleştirip tutarlı hale getirmeyi ve ileride genişletmeyi kolaylaştırmayı hedefliyor.

---

## 3. Önerilen hedef yapı

### 3.1 Klasör yapısı (değişmesin)

Mevcut konumlar aynen kalsın:

- `docs/content/{ServiceName}/main/` → CHANGELOG, ROADMAP, TECHNICAL_SPECS  
- `docs/content/{ServiceName}/support/` → architecture, guides, setup, troubleshooting, specs  

Ek olarak (isteğe bağlı):

- `docs/content/backend/INDEX.md` → "Backend servislerine giriş" sayfası (nav'da "Backend" veya "Backend'e genel bakış" olarak kullanılır).

### 3.2 Nav şablonu — servis başına

Her backend için nav'da **aynı şablon** kullanılır; böylece okuyucu nerede neyi bulacağını hemen anlar.

Önerilen şablon (alt başlıklar, gerektiğinde "yok" ise nav'a eklenmez):

```yaml
- {ServiceName}:
    - Genel bakış / Özet:   {ServiceName}/main/OVERVIEW.md   # isteğe bağlı; yoksa ilk satır Technical Specs veya Roadmap olabilir
    - Changelog:            {ServiceName}/main/CHANGELOG.md
    - Roadmap:              {ServiceName}/main/ROADMAP.md
    - Technical Specs:      {ServiceName}/main/TECHNICAL_SPECS.md
    - Mimari:               {ServiceName}/support/architecture/...
    - Rehberler:            {ServiceName}/support/guides/INDEX.md   # veya en kritik 3–5 rehber
    - Kurulum:              {ServiceName}/support/setup/...         # varsa
    - Sorun giderme:        {ServiceName}/support/troubleshooting/... # varsa
    - Specs:                {ServiceName}/support/specs/...         # varsa
```

"Rehberler" için iki seçenek:

- **A)** Her serviste `support/guides/INDEX.md` tutulur; bu sayfa tüm guide'ları listeler. Nav'da sadece "Rehberler → INDEX" olur.
- **B)** Nav'da doğrudan en sık kullanılan 3–5 rehber (örn. GATEWAY_INTEGRATION, CONFIGURATION) listelenir; diğerleri INDEX'te toplanır.

Başlangıç için **A** daha az bakım gerektirir; **B** kullanım kolaylığı sağlar. Önce A ile gidip, ihtiyaç olursa B'ye geçilebilir.

### 3.3 API dokümantasyonu ile ilişki

- **Seçenek 1:** "API Documentation" ayrı üst bölüm kalsın; `api/overview` ve servis bazlı `api/mngkeeper`, `api/mngdatagateway`, `api/mnghub` orada dursun. Backend sayfalarından "İlgili API detayları için bkz. [API → MngKeeper](api/mngkeeper/index.md)" gibi link verilir.
- **Seçenek 2:** API'yi backend'in parçası say: "Backend Servisleri" altında her serviste "Technical Specs" yanına "API docs" linki eklenir; `api/` sayfaları nav'da ya Backend altında ya da "API (Backend)" adıyla gruplanır.

Öneri: **Seçenek 1** ile başlayıp, API sayfalarının içeriğini backend'lerle senkron tutmak. Gerekirse ileride Seçenek 2'ye geçilebilir.

---

## 4. Uygulama adımları (sırayla)

### Adım 1 — Backend nav şablonunu ve INDEX'i netleştir

- [ ] `docs/content/backend/INDEX.md` oluştur (veya "Backend'e genel bakış" için mevcut bir sayfayı buraya taşıyıp isimlendir).
- [ ] Yukarıdaki nav şablonunu, mevcut dosya yollarına göre tek bir backend (örn. MngKeeper) için mkdocs.yml'e elle yaz; build alıp menüyü kontrol et.

### Adım 2 — Tüm backend'ler için nav'ı şablona çek

- [ ] MngDataGateway, MngHub, MngReactor, MngEngine, MngNotifier, MngScheduler, MngLLM, MngGateway, MngAdmin için nav'ı aynı şablona göre güncelle.
- [ ] Olmayan bölümleri (örn. setup, troubleshooting) nav'a ekleme; her serviste sadece **var olan** dosya/klasörlere link ver.
- [ ] "Services" üst başlığını "Backend Servisleri" yap ve sadece bu 10 servisi listele.

### Adım 3 — Rehberler için ortak kural

- [ ] Karar ver: "Rehberler" tek INDEX sayfası mı (A), yoksa kritik rehberler nav'da açık mı (B)?
- [ ] Her backend'te `support/guides/` altında INDEX.md yoksa, en azından MngKeeper ve MngDataGateway için örnek bir INDEX ekle; diğer servislerde yavaş yavaş açılır.

### Adım 4 — Diğer bölümleri geçici olarak sadeleştir

- [ ] "API Documentation" bölümü sadece `overview` + mngkeeper, mngdatagateway, mnghub kalsın; backend ile çapraz linkleri (içerikten) ekleyebilirsin.
- [ ] Mng.Ui, MngDomainUI, MngMobile'ı nav'da "Services" dışına al: örn. "Frontend / UI" veya "Diğer uygulamalar" gibi ayrı bir üst başlık; ya da bu aşamada nav'dan tamamen çıkarıp sadece backend'e odaklan.

### Adım 5 — Build ve link kontrolü

- [ ] `mkdocs build` ve `mkdocs serve` ile derleme hatasız çalışsın.
- [ ] Backend sayfalarından birbirine ve API sayfalarına giden linkleri kontrol et; kırık link varsa düzelt.

### Adım 6 — DOCUMENTATION_STANDARDS ve ORGANIZATION_PLAN güncellemesi

- [ ] DOCUMENTATION_STANDARDS'ta "MkDocs nav" ile ilgili kısa bir alt bölüm ekle: "Backend servisleri nav şablonu" ve bu plana atıf.
- [ ] DOCS_ORGANIZATION_PLAN'da "MkDocs'u baştan ele alma" ve "önce backend" maddelerini bu planla uyumlu hale getir.

---

## 5. Nav'da önerilen üst seviye (sadece backend odaklı ara aşama)

Ara hedefte üst seviye şöyle olabilir; diğer bloklar (PRD, DevOps, MkDocs kullanımı vb.) sonradan eklenir veya sadeleştirilir.

```yaml
nav:
  - Home: index.md
  - Backend'e genel bakış: backend/INDEX.md
  - Backend Servisleri:
      - MngKeeper: ...
      - MngDataGateway: ...
      - MngHub: ...
      - MngGateway: ...
      - MngReactor: ...
      - MngEngine: ...
      - MngNotifier: ...
      - MngScheduler: ...
      - MngLLM: ...
      - MngAdmin: ...
  - API Documentation:
      - Overview: api/overview.md
      - MngKeeper: api/mngkeeper/index.md
      - MngDataGateway: api/mngdatagateway/index.md
      - MngHub: api/mnghub/index.md
  # - DevOps: ...
  # - Frontend / UI: ... (sonra)
  # - MkDocs: ...
```

Böylece önce backend + API netleşir; sonra DevOps, Frontend, PRD/Roadmap eklenerek nav genişletilir.

---

## 6. Özet

| Ne yapılıyor? | Nasıl? |
|----------------|--------|
| **Backend kapsamı** | MngKeeper, MngDataGateway, MngHub, MngGateway, MngReactor, MngEngine, MngNotifier, MngScheduler, MngLLM, MngAdmin. |
| **Klasör yapısı** | Mevcut `main/` + `support/` yapısı korunur. |
| **Nav** | Her backend için aynı şablon; "Backend Servisleri" altında sadece bu 10 servis. |
| **Rehberler** | Önce servis bazlı INDEX (A); ihtiyaç olursa kritik rehberler açık (B). |
| **API** | Ayrı bölüm; backend sayfalarından çapraz link. |
| **Sonraki adım** | Backend ve API oturduktan sonra Frontend/UI ve DevOps için benzer şablonlar uygulanır. |

Bu plan, MkDocs yapılandırmasını "önce backend" diyerek adım adım, tekrarlanabilir bir şablona oturtmayı hedefler. İstersen bir sonraki adımda doğrudan `backend/INDEX.md` taslağı ve MngKeeper için örnek nav parçasını birlikte çıkarabiliriz.
