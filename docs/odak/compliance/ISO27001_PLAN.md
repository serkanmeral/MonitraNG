# ISO/IEC 27001:2022 — MonitraNG Uyum Planı

**Standart:** ISO/IEC 27001:2022 — Information Security Management System (ISMS)
**Tamamlayıcı:** ISO/IEC 27002:2022 (kontrol uygulama rehberi)
**Bu dokümanın amacı:** Standardın gereksinimlerini (Clause 4–10) ve Annex A kontrollerini MonitraNG platform özellikleriyle eşlemek, boşlukları görünür kılmak.

> Durum kodları: ✅ var · 🟡 kısmi · 🔴 yok · ⚪ ürün kapsamı dışı (organizasyonel)
> Modül kısaltmaları için bkz. [README.md §3](./README.md).

---

## 1. Yaklaşım: "ISMS aracı" vs "ISMS sahibi"

ISO 27001'in Clause 4–10 maddeleri büyük ölçüde **organizasyonel yönetim sistemi** gereksinimleridir (politika, risk değerlendirme süreci, yönetim gözden geçirme). Bir yazılım bunları **tamamen** "karşılayamaz" ama **destekleyebilir/kolaylaştırabilir**.

Bu nedenle MonitraNG için iki katman ayırıyoruz:

- **A. Müşteriye ISMS işletme aracı sunmak** (ürün hedefi) — politika/doküman yönetimi, risk kaydı, varlık envanteri, olay yönetimi, denetim kanıtı toplama.
- **B. Annex A teknik kontrollerini ürünün kendisinde uygulamak** — erişim kontrolü, loglama, şifreleme, yedekleme (hem ürünü güvenli kılar hem müşteriye örnek olur).

---

## 2. Ana maddeler (Clause 4–10) — eşleme

| Clause | Gereksinim | MonitraNG katkısı | Durum | Not / boşluk |
|--------|------------|-------------------|-------|--------------|
| **4** Bağlam | Kapsam, ilgili taraflar | Doküman yönetimi (§4.6) ile kapsam dokümanı tutulur | ⚪/🟡 | Çoğu organizasyonel |
| **5** Liderlik | Politika, roller, sorumluluklar | Politika doküman saklama + RBAC ile rol atama | 🟡 | Politika onay akışı eklenebilir |
| **6** Planlama | Risk değerlendirme & işleme, hedefler | **Risk kaydı modülü gerek** | 🔴 | Risk register: yeni modül (WorkItem tipi olarak modellenebilir) |
| **7** Destek | Kaynak, yetkinlik, farkındalık, **dokümante bilgi** | §4.6 Doküman yönetimi (versiyon, yetki, arama) | 🟡 | Eğitim/farkındalık kaydı boşluk |
| **8** Operasyon | Risk işleme uygulaması | Workflow (§4.8) ile aksiyon takibi | 🟡 | Risk → aksiyon bağı kurulmalı |
| **9** Performans | İzleme, **iç denetim**, yönetim gözden geçirme | Monitoring (§4.1), Dashboard/Reporting (§4.3) | 🟡 | İç denetim planlama/bulgu modülü gerek |
| **10** İyileştirme | Uygunsuzluk + düzeltici faaliyet | WorkItem (uygunsuzluk tipi) + state machine | 🟡 | CAPA şablonu tanımlanmalı |

---

## 3. Annex A kontrolleri (2022 — 93 kontrol, 4 tema)

Aşağıda her tema için **MonitraNG'nin teknik olarak doğrudan etkileyebileceği** kontrollere odaklanılır. Tam liste doldurulacak; bu sürüm önceliklendirilmiş çekirdeği içerir.

### A.5 Organizasyonel kontroller (37)

| Kontrol | Başlık | MonitraNG katkısı | Durum |
|---------|--------|-------------------|-------|
| A.5.1 | Bilgi güvenliği politikaları | Doküman yönetimi (§4.6) | 🟡 |
| A.5.7 | Threat intelligence | Cyber Security Visibility (§4.5) | 🔴 (Faz 2+) |
| A.5.15 | Erişim kontrolü | Keycloak RBAC + OC permission merge | ✅ |
| A.5.16 | Kimlik yönetimi | Keycloak identity | ✅ |
| A.5.17 | Kimlik doğrulama bilgisi | Keycloak (parola politikası, MFA) | 🟡 |
| A.5.18 | Erişim hakları | RBAC + group-based permissions | ✅ |
| A.5.23 | Bulut servis güvenliği | On-premise öncelikli (§3.4) | 🟡 |
| A.5.24–28 | Olay yönetimi (incident) | Alarm Engine (§4.2) → WorkItem (§4.8) | 🟡 |
| A.5.30 | İş sürekliliği için ICT hazırlığı | Monitoring + alarm | 🟡 |

### A.6 İnsan kaynaklı kontroller (8)

| Kontrol | Başlık | MonitraNG katkısı | Durum |
|---------|--------|-------------------|-------|
| A.6.3 | Farkındalık/eğitim | Doküman + (eğitim kaydı boşluk) | 🟡 |
| A.6.8 | Olay raporlama | WorkItem oluşturma / from-origin | 🟡 |

### A.7 Fiziksel kontroller (14)

| Kontrol | Başlık | MonitraNG katkısı | Durum |
|---------|--------|-------------------|-------|
| A.7.x | Fiziksel güvenlik (genel) | Çoğunlukla organizasyonel | ⚪ |
| A.7.4 | Fiziksel güvenlik izleme | Industrial/sensor monitoring (Faz 2) ile dolaylı | 🟡 |

### A.8 Teknolojik kontroller (34) — **ürün için en kritik tema**

| Kontrol | Başlık | MonitraNG katkısı | Durum | Boşluk / aksiyon |
|---------|--------|-------------------|-------|------------------|
| A.8.1 | Uç nokta cihaz | Asset monitoring (§4.1) | 🟡 | |
| A.8.2 | Ayrıcalıklı erişim | RBAC privileged roles | 🟡 | Ayrıcalıklı erişim ayrımı netleştir |
| A.8.3 | Bilgiye erişim kısıtı | OC field-level visible/readonly/masked | ✅ | |
| A.8.5 | Güvenli kimlik doğrulama | Keycloak (MFA, token) | 🟡 | MFA zorunluluğu doğrula |
| A.8.8 | Teknik açıklık yönetimi | (Dependency scan) | 🔴 | CI'da SCA/SAST entegrasyonu |
| A.8.9 | Yapılandırma yönetimi | Runtime config + metadata | 🟡 | Config değişiklik audit'i |
| A.8.10 | Bilgi silme | Veri yaşam döngüsü politikası | 🔴 | Retention/erasure özelliği |
| A.8.12 | Veri sızıntı önleme (DLP) | — | 🔴 | Faz 2+ |
| A.8.15 | **Loglama** | `op_activities`, timeline, audit | ✅ | Merkezi log → SIEM (§4.4) |
| A.8.16 | İzleme faaliyetleri | Monitoring (§4.1) + alarm | ✅ | |
| A.8.18 | Ayrıcalıklı yardımcı program kullanımı | RBAC | 🟡 | |
| A.8.23 | Web filtreleme | — | ⚪ | |
| A.8.24 | **Kriptografi kullanımı** | TLS, MongoDB/MinIO encryption-at-rest | 🟡 | At-rest şifreleme doğrula + key mgmt |
| A.8.25–29 | Güvenli geliştirme yaşam döngüsü | CI/CD süreçleri | 🟡 | Secure SDLC dokümante et |
| A.8.31 | Ortam ayrımı (dev/test/prod) | Odak/prod ayrımı | 🟡 | |
| A.8.32 | Değişiklik yönetimi | Workflow + deploy süreci | 🟡 | Change request WorkItem tipi |

---

## 4. Boşluk özeti (ISO 27001)

**Yeni geliştirme gerektiren (🔴) öncelikli kalemler:**

1. **Risk Register modülü** (Clause 6 + 8) — varlık/tehdit/zafiyet/risk skoru/işleme planı. WorkItem tipi veya ayrı dataset olarak modellenebilir.
2. **İç denetim & bulgu yönetimi** (Clause 9.2) — denetim planı, bulgu, CAPA bağı.
3. **Veri saklama/silme (retention/erasure)** politikası (A.8.10) — DG/Mongo seviyesinde TTL + manuel silme akışı.
4. **Encryption-at-rest + key management** doğrulaması (A.8.24).
5. **Açıklık yönetimi** (A.8.8) — CI'da bağımlılık taraması (SCA/SAST).
6. **Statement of Applicability (SoA)** üretimi — bu doküman onun temeli; UI'da raporlanabilir hale getirilebilir.

**Güçlü olduğumuz (✅) alanlar:** Erişim kontrolü/RBAC, loglama/audit trail, izleme (monitoring), kimlik yönetimi.

---

## 5. Sonraki adım

Annex A'nın kalan kontrollerinin (özellikle A.5 ve A.8 tam listesi) doldurulması ve her 🔴/🟡 kalemin [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md)'a epik olarak taşınması.
