# AS9100D Kalite Süreçleri — MonitraNG Bilgilendirme Özeti

**Amaç:** Havacılık / uzay / savunma sektöründeki kuruluşlara, AS9100D kapsamındaki kalite süreçlerini MonitraNG platformu ile nasıl dijitalleştirebileceğimizi anlatmak.  
**Bu doküman:** Teklif veya fiyatlandırma değildir; amaç, mevcut yetenekler ve planlanan genişleme hakkında ortak bir çerçeve sunmaktır.  
**Son güncelleme:** 9 Haziran 2026

---

## 1. Amaç

### MonitraNG bu projede neyi hedefliyor?

AS9100D, kuruluşların kalite yönetim sistemini (KYS) tanımlamasını, uygulamasını, kayıt altına almasını ve sürekli iyileştirmesini ister. Standart belirli bir yazılım satmaz; denetçinin asıl baktığı şey **kayıtların bütünlüğü, izlenebilirliği ve kanıtlanabilirliğidir**.

MonitraNG'nin rolü:

- Kalite süreçlerini **tek platformda** yönetmek (uygunsuzluk, düzeltici faaliyet, denetim, izlenebilirlik)
- Her kaydın **numaralı, durum akışlı ve onaylı** olmasını sağlamak
- **Kim, ne zaman, ne değiştirdi** sorusuna otomatik cevap vermek (denetim izi)
- İlgili kayıtları birbirine **bağlamak** (ör. NCR → CAPA)

### Ne vaat etmiyoruz?

- AS9100 sertifikası veya sertifikasyon garantisi
- Kalite politikasının, prosedür içeriğinin veya denetim kuruluşu seçiminin yerini almak

### Ne vaat ediyoruz?

Kalite süreçlerinizi sistematik biçimde yönetmenize ve denetimde **kanıt göstermenize** yardımcı olacak bir dijital altyapı kurmak.

**Müşteriye tek cümleyle:**

> *"Kalite kayıtlarınızı — uygunsuzluk, düzeltici faaliyet, denetim, izlenebilirlik — tek sistemde yönetir; denetçiye kayıtlarınızı ve izlerinizi gösterirsiniz."*

---

## 2. AS9100D ne istiyor? (kısa çerçeve)

AS9100D = ISO 9001:2015 + havacılık/uzay/savunma sektörüne özgü ek gereksinimler.

Dijital sistemden beklenen temel süreçler:

| Süreç | Standart referansı | Sistemden beklenen |
|-------|-------------------|-------------------|
| Uygunsuzluk yönetimi (NCR) | Madde 8.7 | Numaralı kayıt, sınıflandırma, disposition, onay, ek |
| Düzeltici / önleyici faaliyet (CAPA) | Madde 10.2 | Kök neden, aksiyon, etkinlik doğrulama, kapanış |
| İç denetim | Madde 9.2 | Plan, kapsam, bulgu → NCR bağlantısı |
| İzlenebilirlik | Madde 8.5.2 | Parça – lot/seri – iş emri – kayıt zinciri |
| Doküman ve kayıt kontrolü | Madde 7.5 | Onaylı revizyon, erişim, saklama |
| İlk madde muayenesi (FAI) | Madde 8.5.1.3 | AS9102 formu, ölçüm, onay |
| Tedarikçi kontrolü | Madde 8.4 | Onaylı tedarikçi, performans, sertifika |
| Operasyonel risk | Madde 8.1.1 | Risk kaydı, değerlendirme, aksiyon |
| Konfigürasyon yönetimi | Madde 8.1.2 | Revizyon, değişiklik etkisi, baseline |
| Performans izleme (OTD/OTQ) | Madde 9.1 | KPI, trend, raporlama |

**Organizasyonel (yazılım dışı) konular:** kalite politikası, yönetim gözden geçirmesi, fiziksel üretim ekipmanı, çalışan yetkinlik eğitiminin içeriği, sertifikasyon kuruluşu seçimi.

---

## 3. Şu anda neleri yapabiliyoruz?

Aşağıdaki yetenekler MonitraNG platformunda **halihazırda çalışır durumdadır**. AS9100 kalite süreçleri, bu altyapı üzerine yapılandırılarak kurulacaktır.

### 3.1 Operation Core — Süreç yönetim motoru

| Yetenek | Açıklama | AS9100 karşılığı |
|---------|----------|------------------|
| **İş kaydı (WorkItem)** | Otomatik numaralı kayıt (`NCR-00001` gibi önek tanımlanabilir) | NCR, CAPA, denetim kaydı |
| **Durum akışı (state machine)** | Tanımlı adımlar; geçişlerde zorunlu alan ve onay kontrolü | MRB, disposition, kapanış onayları |
| **Dinamik formlar** | Sürece özel alanlar (parça no, lot, sınıf, disposition vb.) | AS9100 form gereksinimleri |
| **Rol ve yetki yönetimi** | Grup/rol bazlı görüntüleme, düzenleme, geçiş izni | Yetkinlik ve yetkilendirme (Madde 7.x) |
| **Denetim izi (audit trail)** | Her değişiklik: kim, ne zaman, hangi alan, eski → yeni | Denetim kanıtı |
| **Kayıt ilişkileri** | Üst-alt bağlantı (ör. NCR → CAPA) | İzlenebilirlik |
| **Yorum ve aktivite geçmişi** | Kayıt üzerinde tartışma ve alan bazlı değişiklik logu | Süreç şeffaflığı |
| **Panolar** | Kanban ve liste görünümü | Kalite ekibi iş kuyruğu |
| **Dashboard** | Özet kart, liste ve grafik widget'ları | Açık NCR sayısı, geciken CAPA vb. |
| **SLA takibi** | Yanıt ve çözüm süreleri | Zamanında kapanış |
| **Bildirim** | E-posta uyarıları (sorumlu atama, geçiş vb.) | Aksiyon sahiplerinin bilgilendirilmesi |
| **Etiketleme** | Kayıtları sınıflandırma ve filtreleme | Kategori, öncelik, alan |
| **Dosya ekleri** | Fotoğraf, muayene raporu, form eki | Kanıt dokümanları |
| **Zamanlanmış işler** | Periyodik kontrol veya hatırlatıcı kayıt oluşturma | İç denetim planı hatırlatıcıları |

**Referans:** Aynı motor IT Destek ve demo workspace'lerde canlı çalışmaktadır. Kalite workspace'i bu altyapının üzerine kurulacaktır; yeni bir yazılım geliştirmesi değil, **yapılandırma ve özelleştirme** işidir.

### 3.2 Doküman Yönetimi (Document Intelligence)

| Yetenek | Açıklama | AS9100 karşılığı |
|---------|----------|------------------|
| Klasör yapısı | Prosedür, talimat, form arşivi | Dokümante bilgi (Madde 7.5) |
| Markdown editör | SOP / talimat yazımı ve düzenleme | Prosedür yönetimi |
| Sürüm geçmişi | Hangi revizyon, ne zaman değişti | Revizyon kontrolü |
| Taslak / yayınla | Onay öncesi taslak, onaylı yayın | Kontrollü doküman dağıtımı |
| Dosya yükleme | PDF, Word, Excel ekleri | Form ve talimat arşivi |
| Arama | Doküman bulma | Hızlı erişim |
| Grup bazlı yetki | Kim hangi dokümana erişir | Erişim kontrolü |
| Denetim kaydı | Doküman erişim ve değişiklik izi | Kayıt kontrolü kanıtı |

### 3.3 Platform altyapısı

| Yetenek | Açıklama |
|---------|----------|
| On-premise kurulum | İnternet bağımsız, kurum içi deployment |
| Çok kiracılı mimari | Veri ve kimlik izolasyonu |
| Rol bazlı erişim (Keycloak) | Merkezi kimlik ve yetkilendirme |
| API Gateway | Güvenli servis erişimi |
| Monitoring / alarm | Operasyonel görünürlük (kalite dışı, isteğe bağlı modül) |

---

## 4. Neler eklenecek?

Platform hazır; AS9100 kalite süreçlerine özel **yapılandırma ve genişletme** yapılacaktır. Aşağıdaki tablo, planlanan işleri fazlara göre ayırır.

### Özet: Mevcut ↔ Eklenecek

```
┌──────────────────────────────────┬────────────────────────────────────┐
│  MEVCUT (platform)               │  EKLENECEK (kalite projesi)        │
├──────────────────────────────────┼────────────────────────────────────┤
│  Süreç motoru                    │  NCR / CAPA iş tipleri ve formları │
│  Durum akışı + onay              │  AS9100 alanları ve akış adımları  │
│  Denetim izi                     │  MRB / disposition onay kuralları  │
│  Kayıt ilişkileri                │  NCR → CAPA bağlantı modeli        │
│  Panolar + dashboard             │  Kalite KPI widget'ları            │
│  Doküman modülü (temel)          │  SOP ↔ kayıt entegrasyonu          │
│  Rol bazlı erişim                │  Müşteriye özel rol haritası       │
│  Bildirim + SLA                  │  Kalite bildirim kuralları         │
│                                  │  FAI, tedarikçi (ihtiyaca göre)    │
└──────────────────────────────────┴────────────────────────────────────┘
```

---

### Faz 1 — Kalite yönetimi temeli (önerilen başlangıç)

Her AS9100 denetiminde konuşulan iki süreç: **NCR** ve **CAPA**.

| # | Eklenecek | Açıklama |
|---|-----------|----------|
| 1 | **Kalite Workspace** | AS9100 süreçleri için ayrı çalışma alanı |
| 2 | **NCR iş tipi** | Form alanları + durum akışı (Bölüm 5) |
| 3 | **CAPA iş tipi** | Form + akış + NCR bağlantısı (Bölüm 5) |
| 4 | **Rol tanımları** | Kalite mühendisi, üretim, MRB, yönetim rolleri |
| 5 | **Kalite panosu** | Açık NCR/CAPA, geciken aksiyonlar, özet kartlar |
| 6 | **Demo ve eğitim** | Örnek kayıtlarla canlı gösterim |
| 7 | **Kısa kullanım rehberi** | Operatörler için temel kullanım kılavuzu |

**Faz 1 kapsam dışı:** FAI, tedarikçi modülü, ERP/MES entegrasyonu.

---

### Faz 2 — Yatay genişleme

| # | Eklenecek | Açıklama |
|---|-----------|----------|
| 1 | **İç denetim (Audit) iş tipi** | Denetim planı, kapsam, bulgular → NCR |
| 2 | **Değişiklik yönetimi (Change)** | Konfigürasyon değişikliği, etki analizi, onay |
| 3 | **Doküman ↔ iş kaydı entegrasyonu** | NCR/CAPA'ya prosedür ve talimat bağlama |
| 4 | **Risk kaydı (Risk Register)** | Operasyonel risk değerlendirme ve aksiyon |
| 5 | **Kalite KPI dashboard** | OTD/OTQ ve süreç performans göstergeleri |
| 6 | **Denetim kanıtı dışa aktarım** | Kayıt ve iz raporlarının export'u |

---

### Faz 3 — Havacılık derinliği (ihtiyaca göre)

| # | Eklenecek | Açıklama |
|---|-----------|----------|
| 1 | **FAI (İlk Madde Muayenesi)** | AS9102 form şablonu, ölçüm sonuçları, onay |
| 2 | **Tedarikçi yönetimi** | Onay durumu, performans, sertifika geçerlilik |
| 3 | **Sahte parça önleme akışı** | Tedarik zinciri doğrulama kontrolleri |
| 4 | **Lot/seri izlenebilirlik** | Üretim kayıtlarıyla derin entegrasyon |
| 5 | **ERP/MES entegrasyonu** | İş emri, parça, lot verisi çekimi |

---

## 5. NCR ve CAPA — önerilen kayıt yapısı (Faz 1 taslağı)

Aşağıdaki tasarım AS9100D denetim diline göre hazırlanmıştır. Kurulum öncesinde müşterinin mevcut prosedürleri ve form şablonları ile kesinleştirilecektir.

### NCR — Uygunsuzluk Kaydı

**Önerilen alanlar:**

- Otomatik kayıt numarası (örn. `NCR-00001`)
- Başlık / özet
- Tespit tarihi, tespit eden
- Kaynak / aşama: girdi muayene · proses içi · final · müşteri iadesi · tedarikçi · denetim
- Parça numarası, parça adı
- Lot / parti / seri numarası
- Etkilenen / muayene edilen adet
- İş emri / proje / sözleşme referansı
- Tedarikçi (tedarikçi kaynaklıysa)
- Uygunsuzluk tanımı
- İhlal edilen şartname / gereksinim
- Sınıf: minör / majör / kritik
- Acil kontrol (containment) aksiyonu
- Disposition: kullan · yeniden işle · tamir · hurda · tedarikçiye iade · yeniden derecelendir
- Disposition gerekçesi + MRB (Malzeme İnceleme Kurulu) onayı
- Müşteri bildirimi gerekli mi?
- Bağlı CAPA
- Ekler (fotoğraf, muayene raporu)

**Durum akışı:**

```text
Açık → Kontrol altına alındı → MRB değerlendirme → Disposition onaylandı → Kapandı
```

### CAPA — Düzeltici / Önleyici Faaliyet

**Önerilen alanlar:**

- Otomatik kayıt numarası (örn. `CAPA-00001`)
- Bağlı NCR(ler) / tetikleyen kaynak
- Problem tanımı
- Kök neden analizi (5-neden / balık kılçığı)
- İnsan faktörü kategorisi (AS9100 özel gereksinim)
- Düzeltme (immediate correction)
- Düzeltici faaliyet (tekrarı önler)
- Önleyici faaliyet (başka alanları korur)
- Sorumlu sahip
- Hedef tarih
- Etkinlik doğrulaması (CAPA'nın işe yaradığını kanıtlama)
- Doğrulama tarihi / sonucu
- Kapanış onayı

**Durum akışı:**

```text
Açık → Kök neden → Aksiyon planı → Uygulama → Etkinlik doğrulama → Kapandı
```

**İlişki:** NCR (üst kayıt) → CAPA (alt kayıt). Her geçiş ve değişiklik otomatik denetim izine yazılır.

---

## 6. AS9100 madde eşlemesi (özet)

| Standart | Gereksinim | Platform durumu |
|----------|------------|-----------------|
| 8.7 | NCR | 🟡 Motor hazır → Faz 1'de şablon kurulacak |
| 10.2 | CAPA | 🟡 Motor hazır → Faz 1'de şablon kurulacak |
| 8.5.2 | İzlenebilirlik | ✅ Kayıt ilişkileri + timeline mevcut |
| 7.5 | Doküman / kayıt kontrolü | 🟡 Doküman modülü mevcut → iş kaydı entegrasyonu Faz 2 |
| 9.2 | İç denetim | 🔴 Faz 2 |
| 8.5.1.3 | FAI | 🔴 Faz 3 |
| 8.4 | Tedarikçi | 🔴 Faz 3 |
| 8.1.1 | Operasyonel risk | 🔴 Faz 2 (Risk Register) |
| 8.1.2 | Konfigürasyon yönetimi | 🔴 Faz 2 (Change) |
| 9.1 | OTD/OTQ performans | 🟡 Dashboard altyapısı mevcut → Faz 2 KPI |

**Durum kodları:** ✅ Mevcut · 🟡 Kısmen var, yapılandırma gerekli · 🔴 Henüz yok, geliştirme gerekli

---

## 7. Kurulum için müşteriden beklenenler

| Girdi | Neden gerekli |
|-------|---------------|
| Mevcut kalite prosedürleri (NCR, CAPA) | Form alanları ve akış adımlarının kesinleştirilmesi |
| Rol listesi (kalite, üretim, MRB, yönetim) | Yetki ve onay kurallarının tanımı |
| Pilot kullanıcı grubu (3–5 kişi) | İlk canlı kullanım ve geri bildirim |
| Sunucu / altyapı bilgisi | On-premise kurulum planlaması |
| Süreç sahibi (kalite müdürü veya vekili) | Karar noktalarında erişilebilirlik |

---

## 8. Kapsam dışı

- AS9100 sertifikasyon danışmanlığı ve denetim kuruluşu seçimi
- ERP/MES entegrasyonu (Faz 3, ayrı kapsam)
- Çalışan yetkinlik eğitiminin içeriği (kayıt tutulabilir; eğitim verilmez)
- ISO 27001 / SIEM modülü (ayrı kapsam, isteğe bağlı)

---

## İlgili dokümanlar (iç kullanım)

- [DEVAM.md](./DEVAM.md) — Devam noktası ve sıradaki adımlar
- [AS9100_PLAN.md](./AS9100_PLAN.md) — Detaylı standart eşleme ve boşluk analizi
- [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md) — Fazlı yol haritası ve stratejik kararlar
- [README.md](./README.md) — Uyum planlama metodolojisi

**PDF yeniden üretim:** `cd docs/odak/compliance && npm run pdf:as9100`
