# AS9100D — MonitraNG Uyum Planı

**Standart:** AS9100D (2016) — Quality Management Systems – Requirements for Aviation, Space, and Defense Organizations
**Temel:** ISO 9001:2015 + sektöre özgü ek gereksinimler
**Bu dokümanın amacı:** AS9100D gereksinimlerini, özellikle ISO 9001'in üzerine eklenen sektöre özgü maddeleri, MonitraNG platform özellikleriyle eşlemek.

> Durum kodları: ✅ var · 🟡 kısmi · 🔴 yok · ⚪ ürün kapsamı dışı (organizasyonel)
> Modül kısaltmaları için bkz. [README.md §3](./README.md).

---

## 1. Yaklaşım

AS9100, **kalite yönetim sistemi** standardıdır. MonitraNG'nin buradaki rolü, AS9100 işleten bir üretici/MRO/distribütörün **operasyonel kalite süreçlerini dijitalleştiren araç** olmaktır. Bu, doğrudan Operation Core (§4.8 Workflow & Work Management) ve Doküman Yönetimi (§4.6) modülleriyle örtüşür.

AS9100'ün kalbi şu kavramlardır ve hepsi **WorkItem + state machine + metadata** modeliyle ifade edilebilir:

- Uygunsuzluk (NCR) yönetimi
- Düzeltici/önleyici faaliyet (CAPA)
- İlk Madde Muayenesi (FAI)
- Konfigürasyon yönetimi
- İzlenebilirlik (traceability)
- Tedarikçi yönetimi
- Risk & fırsat yönetimi

---

## 2. ISO 9001 ortak maddeleri (Clause 4–10) — eşleme

| Clause | Gereksinim | MonitraNG katkısı | Durum |
|--------|------------|-------------------|-------|
| **4** Kuruluş bağlamı | Süreç yaklaşımı, kapsam | Doküman yönetimi (§4.6) | 🟡 |
| **5** Liderlik | Müşteri odağı, politika | Doküman + dashboard KPI | 🟡 |
| **6** Planlama | Risk & fırsat, hedefler | **Risk modülü gerek** (ISO27001 ile ortak) | 🔴 |
| **7** Destek | Kaynak, yetkinlik, dokümante bilgi | §4.6 Doküman + (yetkinlik kaydı boşluk) | 🟡 |
| **8** Operasyon | Ürün/hizmet gerçekleştirme | Operation Core (§4.8) çekirdeği | 🟡 |
| **9** Performans | İzleme, ölçüm, iç denetim | Reporting (§4.3) + Monitoring (§4.1) | 🟡 |
| **10** İyileştirme | Uygunsuzluk + sürekli iyileştirme | WorkItem + state machine + SLA | 🟡 |

---

## 3. AS9100'e özgü gereksinimler — **ürün için kritik**

Bunlar ISO 9001'de olmayan, AS9100'ün eklediği maddelerdir. MonitraNG'nin asıl değer kattığı yer.

| Gereksinim | AS9100 referans | MonitraNG katkısı | Durum | Boşluk / aksiyon |
|------------|-----------------|-------------------|-------|------------------|
| **Konfigürasyon yönetimi** | 8.1.2 | Metadata + versiyonlama + WorkItem | 🟡 | Config item dataset + baseline takibi |
| **Ürün güvenliği (product safety)** | 8.1.3 | WorkItem (güvenlik tipi) + alarm | 🟡 | Safety flag alanı + eskalasyon |
| **Sahte parça önleme** | 8.1.4 | İzlenebilirlik + tedarikçi kaydı | 🔴 | Counterfeit kontrol akışı |
| **Risk yönetimi (operasyonel)** | 8.1.1 | Risk modülü (ortak boşluk) | 🔴 | Risk register |
| **İlk Madde Muayenesi (FAI)** | 8.5.1.3 | WorkItem (FAI tipi) + form + ekler | 🔴 | FAI raporu şablonu (AS9102 form) |
| **Özel süreç yönetimi** | 8.5.1.2 | WorkItem + yetkinlik/onay | 🟡 | Özel süreç onay alanları |
| **İzlenebilirlik (traceability)** | 8.5.2 | WorkItem key + parent-child + timeline | ✅ | Lot/serial alanları eklenecek |
| **İnsan faktörleri** | 10.2 | NCR kök neden alanında HF kategorisi | 🟡 | HF sınıflandırma alanı |
| **Tedarik zinciri kontrolü** | 8.4 | Tedarikçi dataset + onay akışı | 🔴 | Supplier mgmt modülü |
| **Zamanında teslim/kalite (OTD/OTQ)** | 9.1 | SLA tracking (§4.8) + Dashboard | 🟡 | OTD/OTQ KPI widget |
| **Uygunsuzluk yönetimi (NCR)** | 8.7 | WorkItem (NCR tipi) + state machine | 🟡 | NCR şablonu + disposition alanları |
| **Düzeltici faaliyet (CAPA)** | 10.2 | WorkItem + parent-child (NCR→CAPA) | 🟡 | CAPA şablonu + etkinlik doğrulama |
| **Kayıt kontrolü (dokümante bilgi)** | 7.5 | §4.6 Doküman + DG kayıtları | 🟡 | Retention + onaylı revizyon |

---

## 4. Önerilen WorkItem tipleri (AS9100 için)

Operation Core'un dinamik tip/form yapısı sayesinde aşağıdaki kalite süreçleri **kod değişikliği olmadan** metadata ile tanımlanabilir:

| WorkItem tipi | Standart süreç | Anahtar alanlar (öneri) |
|---------------|----------------|--------------------------|
| `NCR` | Uygunsuzluk | parça no, lot/serial, uygunsuzluk tanımı, disposition, kök neden |
| `CAPA` | Düzeltici faaliyet | bağlı NCR, kök neden analizi, aksiyon, doğrulama, kapanış |
| `FAI` | İlk madde muayenesi | parça no, AS9102 form ekleri, ölçüm sonuçları, onay |
| `Audit` | İç denetim | denetim planı, kapsam, bulgular (→ NCR) |
| `Risk` | Risk & fırsat | olasılık, etki, skor, işleme planı (ISO27001 ortak) |
| `Supplier` | Tedarikçi yönetimi | onay durumu, performans, sertifika geçerlilik |
| `Change` | Değişiklik yönetimi | etki analizi, onay, konfigürasyon baseline |

> Bunların tümü **state machine + transition + requiredFields + permission merge** ile yönetilir; AS9100'ün talep ettiği onay/izlenebilirlik doğal olarak `op_activities` + `op_work_item_timelines`'a yazılır.

---

## 5. Boşluk özeti (AS9100)

**Yeni geliştirme gerektiren (🔴) öncelikli kalemler:**

1. **FAI (İlk Madde Muayenesi)** süreci + AS9102 form şablonu.
2. **Tedarikçi yönetimi** modülü (onay, performans, sertifika takibi).
3. **Risk register** (ISO 27001 ile ortak — tek modül her ikisine hizmet eder).
4. **Sahte parça önleme** kontrol akışı + izlenebilirlik alanları (lot/serial).

**Hazır altyapıyla hızlı kazanım (🟡 → ✅):** NCR/CAPA/Audit/Change WorkItem tipleri — sadece metadata (tip + form + state flow) tanımıyla başlatılabilir.

**Güçlü olduğumuz (✅) alan:** İzlenebilirlik (WorkItem key + parent-child + timeline) ve onay akışları (state transitions).

---

## 6. Sonraki adım

Her 🔴/🟡 kalemin metadata tasarımı (dataset alanları + state flow) detaylandırılması ve [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md)'a fazlanması. Öncelik: ortak "Risk register" modülü (iki standarda da hizmet eder).
