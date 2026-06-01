# MonitraNG — Uyum Yol Haritası (ISO 27001 + AS9100)

> **Bu doküman bir "devam noktası" / referanstır.** İki standardın boşluk analizini ([ISO27001_PLAN.md](./ISO27001_PLAN.md), [AS9100_PLAN.md](./AS9100_PLAN.md)) tek, fazlı bir plana bağlar ve bu konuda verilen stratejik kararları kaydeder. Yeni bir oturumda buradan devam edilir.

**Son güncelleme:** 1 Haziran 2026

---

## 0. Bağlam & verilen kararlar (1 Haz 2026)

Bu yol haritası, bir keşif sohbetinden çıktı. Önemli kararlar:

| # | Karar | Gerekçe |
|---|-------|---------|
| K1 | **Hedef = ürün-kolaylaştırıcı (product-as-enabler).** Müşteriye sertifika vaadi yok; standartları *işletmesine ve denetimde kanıt göstermesine* yardımcı oluyoruz. | Dürüst + satılabilir konumlandırma. Yazılım tek başına sertifika getiremez. |
| K2 | **AS9100 önce, ISO 27001 yatay zemin.** | İlk müşteri **havacılık** sektöründe; gerçek, canlı ihtiyaç. ISO 27001 ise tüm müşterilere değer katan olgunluk zemini. |
| K3 | **Standartlar yeni kapsam değil, mevcut işin paketlenmesi.** | Müşteri zaten **task manager + SIEM** istedi. Task manager = AS9100 zemini (WorkItem/akış); SIEM = ISO 27001 zemini (log/iz). Major plan'a evrildi ve **müşteri onayı alındı**. |
| K4 | **İlk somut adım = NCR + CAPA şablonları** (Operation Core WorkItem tipleri). | Her AS9100 denetiminde konuşulur; mevcut motorun üstünde, ağır kod gerektirmez; gerçek müşteriye doğrudan hitap eder. |
| K5 | **Müşteri NCR/CAPA'yı bugün Excel'de yönetiyor** (yazılım yok). | Rakip yok = kolay giriş. Excel'in acıları (iz/erişim/versiyon yok) tam bizim güçlü yanlarımız. **Müşterinin Excel sütunları = gerçek spec.** |
| K6 | **Danışman + kodlama asistanı rolü.** Altın kaplamadan kaçın; satışı kapatmayan işi standardı bahane ederek yapma. | Odak ve hız. |

**Satış mesajı (müşteriye):** *"AS9100 sertifikalıyız"* DEME. *"Kalite süreçlerinizi — uygunsuzluk, düzeltici faaliyet, denetim, izlenebilirlik — sistemde yönetir ve denetimde kanıt gösterirsiniz"* DE.

---

## 1. Stratejik çerçeve: iki istek, iki standardın zemini

| Müşterinin istediği | Aslında nedir | Hangi standardın zemini |
|---------------------|---------------|--------------------------|
| **Task manager** | Operation Core (WorkItem + akış + onay + audit timeline) | **AS9100** — NCR, CAPA, FAI, denetim hepsi "iş kaydı" |
| **SIEM** | Log toplama + güvenlik olayları + erişim/iz | **ISO 27001** — A.8.15 loglama, A.8.16 izleme, olay yönetimi |

Sonuç: standart desteği bir yan yol değil, mevcut yatırımın **konumlandırılması**.

---

## 2. Ortak modül stratejisi

İki standardın boşlukları büyük oranda aynı çekirdeğe işaret eder; ortakları bir kez geliştirmek her ikisini de besler.

| Ortak yetenek | ISO 27001'i besler | AS9100'ü besler |
|---------------|--------------------|------------------|
| **WorkItem tabanlı süreç** (NCR/CAPA/Audit/Change) | A.5.24–28 olay, Clause 10 | 8.7 NCR, 10.2 CAPA, iç denetim |
| **Risk Register** modülü | Clause 6/8 risk işleme | 8.1.1 operasyonel risk |
| **Doküman/SOP yönetimi** (versiyon + onay) | Clause 7.5, A.5.1 | 7.5 kayıt kontrolü |
| **Audit trail / timeline** | A.8.15 loglama | 8.5.2 izlenebilirlik |
| **Erişim kontrolü (RBAC)** | A.5.15–18, A.8.x | 7.x yetkinlik/yetki |
| **KPI/raporlama** | Clause 9 + SoA | 9.1 OTD/OTQ performans |

---

## 3. Fazlı plan

### Faz C1 — NCR + CAPA pilotu (ŞU ANKİ HEDEF)
> Mevcut Operation Core motoru üstünde, ağırlıklı olarak metadata (tip + form + state flow). Gerçek havacılık müşterisine yönelik.

- [ ] Müşterinin **Excel sütunlarını** al (gerçek spec) — **bekleyen girdi**
- [ ] **NCR** WorkItem tipi: form alanları + durum akışı (§4 taslak)
- [ ] **CAPA** WorkItem tipi: form alanları + durum akışı (§4 taslak)
- [ ] **NCR → CAPA** parent-child ilişkisi
- [ ] Çalışan **demo** (Excel'den sisteme göç hikâyesiyle birlikte)
- [ ] Audit trail'in "denetim kanıtı" kalitesinde olduğunu doğrula

### Faz C2 — Yatay zemin & ortak çekirdek
- [ ] **Audit trail / erişim izi**ni denetim-kanıtı seviyesine getir (ISO 27001 doğal kazanım, SIEM ile)
- [ ] **Risk Register** modülü (her iki standart — tek modül ikisine hizmet)
- [ ] **Doküman yönetimi** onay & versiyon akışı (major_plan §4.6)
- [ ] **İç denetim (Audit)** + **Değişiklik (Change)** WorkItem tipleri

### Faz C3 — Derinleşme
- [ ] **AS9100:** FAI (AS9102) süreci, Tedarikçi yönetimi, sahte parça önleme, lot/serial izlenebilirlik
- [ ] **ISO 27001:** açıklık yönetimi (CI'da SCA/SAST), encryption-at-rest + key mgmt, retention/erasure
- [ ] **KPI/rapor:** OTD/OTQ, SoA raporu, denetim dashboard'u

---

## 4. NCR + CAPA taslak tasarımı (Faz C1)

> AS9100 denetim diline göre hazırlandı (`disposition`, `MRB`, `etkinlik doğrulama`, `insan faktörü`). Müşteri Excel'i gelince kesinleştirilecek. İkisi de Operation Core'da WorkItem tipi.

### NCR — Uygunsuzluk Kaydı (Nonconformance Report)

**Önerilen alanlar:**
- Otomatik anahtar (örn. `NCR-00001`)
- Başlık / özet
- Tespit tarihi, tespit eden
- Kaynak/aşama: girdi muayene · proses içi · final · müşteri iadesi · tedarikçi · denetim
- Parça no · parça adı
- Lot / parti / **seri no** (izlenebilirlik)
- Etkilenen / muayene edilen adet
- İş emri / proje / sözleşme referansı
- Tedarikçi (tedarikçi kaynaklıysa)
- Uygunsuzluk tanımı
- İhlal edilen şartname / gereksinim
- Sınıf: minör / majör / kritik
- **Acil kontrol (containment) aksiyonu**
- **Disposition:** kullan (use-as-is) · yeniden işle (rework) · tamir (repair) · hurda (scrap) · tedarikçiye iade · yeniden derecelendir
- Disposition gerekçesi + **MRB (Malzeme İnceleme Kurulu) onayı**
- Müşteri bildirimi gerekli mi? (havacılıkta çoğu zaman sözleşme gereği)
- Bağlı CAPA
- Ekler (foto, muayene raporu)

**Durum akışı:**
`Açık → Kontrol altına alındı (containment) → MRB değerlendirme → Disposition onaylandı → Kapandı`
(red/geri dönüş döngüleri eklenebilir)

### CAPA — Düzeltici / Önleyici Faaliyet

**Önerilen alanlar:**
- Otomatik anahtar (örn. `CAPA-00001`)
- Bağlı NCR(ler) / tetikleyen kaynak
- Problem tanımı
- **Kök neden analizi** (5-neden / balık kılçığı)
- **İnsan faktörü kategorisi** (AS9100 özel ister)
- Düzeltme (immediate correction)
- Düzeltici faaliyet (tekrarı önler)
- Önleyici faaliyet (başka alanlar)
- Sorumlu sahip
- Hedef tarih
- **Etkinlik doğrulaması** (AS9100: CAPA'nın işe yaradığını doğrula)
- Doğrulama tarihi / sonucu
- Kapanış onayı

**Durum akışı:**
`Açık → Kök neden → Aksiyon planı → Uygulama → Etkinlik doğrulama → Kapandı`

**İlişki:** NCR (üst) → CAPA (alt). Operation Core parent-child yapısı destekliyor; geçişler `op_activities` + `op_work_item_timelines`'a yazılarak doğal denetim izi oluşur.

---

## 5. İzlenebilirlik matrisi (kontrol ↔ özellik ↔ kanıt)

| ID | Standart | Madde/Kontrol | MonitraNG özelliği | Durum | Kanıt (evidence) | Faz |
|----|----------|---------------|--------------------|-------|------------------|-----|
| C-001 | ISO 27001 | A.5.15 Erişim kontrolü | Keycloak RBAC + OC permission | ✅ | Rol/permission konfigürasyonu | — |
| C-002 | ISO 27001 | A.8.15 Loglama | `op_activities` + timeline | ✅ | Activity kayıtları | C2 |
| C-003 | ISO 27001 | Clause 6 Risk | Risk Register modülü | 🔴 | — | C2 |
| C-006 | AS9100 | 8.7 NCR | WorkItem (NCR tipi) | 🟡 | NCR kayıtları + timeline | **C1** |
| C-007 | AS9100 | 10.2 CAPA | WorkItem (CAPA) + parent-child | 🟡 | CAPA kapanış + etkinlik doğrulama | **C1** |
| C-008 | AS9100 | 8.5.2 İzlenebilirlik | WorkItem key + parent-child + lot/serial | 🟡 | Lot/serial + ilişki | C1/C3 |
| C-009 | AS9100 | 8.5.1.3 FAI | WorkItem (FAI) + AS9102 form | 🔴 | — | C3 |
| C-010 | AS9100 | 8.4 Tedarikçi | Supplier modülü | 🔴 | — | C3 |
| C-011 | Ortak | Risk yönetimi | Risk Register | 🔴 | — | C2 |
| C-012 | Ortak | Doküman/SOP kontrolü | Doküman yönetimi (§4.6) | 🟡 | Versiyon + onay kaydı | C2 |

---

## 6. Açık sorular / bekleyen girdiler

| Soru | Durum |
|------|-------|
| Birincil hedef (enabler vs sertifikasyon)? | ✅ Karar K1 (enabler) |
| Hangi standart önce? | ✅ Karar K2 (AS9100) |
| Müşteri NCR/CAPA'yı nasıl yönetiyor? | ✅ Excel (K5) |
| **Müşterinin Excel sütunları (NCR/CAPA alanları)** | 🔲 **Bekleniyor — Faz C1 başlangıç girdisi** |
| Risk Register: ayrı modül mü, WorkItem tipi mi? | 🔲 C2'de karara bağlanacak |
| Hedef tarih / sertifikasyon takvimi? | 🔲 Müşteriyle netleşecek |
| Kanıt (evidence) export'u otomatik mi olmalı? | 🔲 C2 |

---

## 7. Kaldığımız yer (devam noktası)

- Strateji ve roller netleşti (§0).
- **Sıradaki aksiyon:** Müşteriden NCR/CAPA Excel sütunlarını almak → §4 taslağını kesinleştirmek → NCR/CAPA tiplerini Operation Core'da (tip + form + state flow + parent-child) kurmak.
- Excel gelene kadar §4 taslağı varsayılan olarak kullanılabilir; geldiğinde alanlar buna göre güncellenir.
- İlgili kaynaklar: Operation Core motoru → [../operationcore/mngoperations/](../operationcore/mngoperations/README.md); platform vizyonu → [../operationcore/major_plan.md](../operationcore/major_plan.md).
