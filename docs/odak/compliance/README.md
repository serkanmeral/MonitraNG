# MonitraNG — Standart Uyumluluğu (Compliance) Planı

**Amaç:** MonitraNG platformunun **ISO/IEC 27001:2022** (Bilgi Güvenliği Yönetim Sistemi) ve **AS9100D** (Havacılık/Uzay/Savunma Kalite Yönetim Sistemi) standartlarının gereksinimlerini **ürün özellikleriyle karşılayabilir** hale gelmesini planlamak.

> **Önemli ayrım:** Burada iki ayrı hedef vardır ve karıştırılmamalıdır:
> 1. **Ürün olarak uyum (product-as-enabler):** MonitraNG'nin, bu standartları uygulamak/sürdürmek isteyen **müşterilere** araç sunması (audit trail, doküman/SOP yönetimi, erişim kontrolü, NCR/uygunsuzluk yönetimi, izlenebilirlik vb.). **Bu planın birincil odağı budur.**
> 2. **Kurum olarak sertifikasyon (organization-as-certified):** MonitraNG'yi geliştiren şirketin kendi süreçlerini sertifikalandırması. Bu plan bunu kapsam dışında tutar ama dokümanlarda ayrıca işaretlenir.

---

## Bu klasör

| Doküman | İçerik | Durum |
|---------|--------|--------|
| [DEVAM.md](./DEVAM.md) | **Devam noktası** — checkpoint, sıradaki adımlar, yeni chat prompt'u | Güncel |
| [README.md](./README.md) | Bu dosya — vizyon, metodoloji, modül haritası, sözlük | Taslak |
| [AS9100_MUSTERI_OZET.md](./AS9100_MUSTERI_OZET.md) | AS9100D — müşteri bilgilendirme özeti (amaç, mevcut, eklenecek) | Taslak |
| [AS9100_MUSTERI_OZET.pdf](./AS9100_MUSTERI_OZET.pdf) | Müşteri özeti — yazdırılabilir PDF (`npm run pdf:as9100`) | Güncel |
| [ISO27001_PLAN.md](./ISO27001_PLAN.md) | ISO/IEC 27001:2022 — clause + Annex A kontrol eşleme, boşluk analizi | Taslak |
| [AS9100_PLAN.md](./AS9100_PLAN.md) | AS9100D — sektöre özgü gereksinim eşleme, boşluk analizi | Taslak |
| [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md) | Birleşik fazlı yol haritası + izlenebilirlik matrisi + açık sorular | Taslak |

---

## 1. Neden bu iki standart birlikte?

İki standart farklı eksenlere bakar ama MonitraNG'nin platform vizyonuyla ([major_plan.md](../operationcore/major_plan.md)) büyük oranda örtüşür:

| Standart | Ekseni | MonitraNG'de karşılığı (major_plan) |
|----------|--------|--------------------------------------|
| **ISO 27001** | Bilgi güvenliği (gizlilik/bütünlük/erişilebilirlik) | §4.4 Log/SIEM, §4.5 Cyber Security Visibility, §7.3 Keycloak/RBAC, §3.2 multi-tenant izolasyon |
| **AS9100** | Kalite + uçuş güvenliği + izlenebilirlik | §4.6 Doküman/SOP yönetimi, §4.8 Operational Workflow & Work Management (Operation Core) |

**Ortak zemin (her iki standart da talep eder):**

- Erişim kontrolü ve yetkilendirme (RBAC) → Keycloak (§7.3)
- Değişmez denetim izi (audit trail / timeline) → Operation Core `op_activities` + `op_work_item_timelines`
- Doküman ve sürüm yönetimi → §4.6 Internal Document Management
- Olay/uygunsuzluk yönetimi (incident / NCR) → §4.2 Alarm Engine + §4.8 Workflow
- Risk yönetimi → ortak modül ihtiyacı (henüz yok — boşluk)
- Onay akışları (approval flows) → §4.8 state machine + transitions

---

## 2. Metodoloji

Her iki standart için aynı 5 adımlı çevrim uygulanır:

```text
1. Gereksinim çıkarımı   → standardın her clause/kontrolünü maddele
2. Eşleme (mapping)      → her gereksinimi MonitraNG modül/özelliğine bağla
3. Boşluk analizi (gap)  → durum: ✅ var | 🟡 kısmi | 🔴 yok | ⚪ kapsam dışı
4. Backlog                → boşlukları somut geliştirme epiklerine dönüştür
5. İzlenebilirlik         → kontrol ↔ özellik ↔ kanıt (evidence) matrisi
```

### Boşluk durum kodları

| Kod | Anlam |
|-----|-------|
| ✅ | Mevcut özellik karşılıyor |
| 🟡 | Kısmen var, geliştirme gerekli |
| 🔴 | Yok, yeni geliştirme gerekli |
| ⚪ | Ürün kapsamı dışı (kurumsal süreç / organizasyonel kontrol) |

---

## 3. Platform modül haritası (referans)

Uyum eşlemesinde kullanılan MonitraNG bileşenleri:

| Bileşen | Teknoloji / Konum | Uyum açısından rolü |
|---------|-------------------|---------------------|
| **Identity** | Keycloak (realm/tenant, RBAC, token) | Erişim kontrolü, kimlik doğrulama, yetkilendirme |
| **Operation Core (MngOperations)** | .NET 9, `MngOperations/` | Workflow, state machine, audit timeline, onay akışları, NCR/incident |
| **MngDataGateway** | Dynamic dataset (`op_*`) | Metadata, kayıt saklama, kayıt yönetimi |
| **Storage** | MongoDB + MinIO | Veri ve dosya/ek saklama (şifreleme, yedekleme noktası) |
| **Messaging** | RabbitMQ | Olay yayını (`oc.workitem.*`), bildirim pipeline |
| **Bildirim** | MngNotifiers | E-posta/uyarı kanalları |
| **UI** | Mng.Ui (Nuxt 3) | Doküman yönetimi, dashboard, raporlama yüzeyi |
| **Monitoring/SIEM** | major_plan §4.1/4.4 (Faz 1+); planlama: [../monitoring/SIEM_PLANNING.md](../monitoring/SIEM_PLANNING.md) | Log toplama, güvenlik olayları, denetim kaynağı |

---

## 4. Sözlük (standart ↔ ürün terimi)

| Standart terimi | MonitraNG karşılığı |
|-----------------|---------------------|
| Asset (varlık) | Monitoring asset / WorkItem |
| Audit trail | `op_activities` + `op_work_item_timelines` |
| Access control | Keycloak RBAC + Operation Core permission merge |
| Document / record control | §4.6 Document Management + DG kayıtları |
| Nonconformity (NCR) | WorkItem (uygunsuzluk tipi) + state machine |
| Incident | Alarm/Event → WorkItem |
| Approval flow | State transition + `requiredFields` |
| Traceability | WorkItem key + parent-child relations + timeline |
| Statement of Applicability (SoA) | [ISO27001_PLAN.md](./ISO27001_PLAN.md) eşleme tablosu |

---

## 5. Durum & verilen kararlar

- **Oluşturulma:** 31 May 2026 — başlangıç iskeleti. **Güncelleme:** 9 Haz 2026 — müşteri bilgilendirme özeti + PDF.
- **Hedef (karar):** Ürün-kolaylaştırıcı (enabler). Sertifika vaadi değil; müşterinin standartları *işletmesine + denetimde kanıt göstermesine* yardım.
- **Öncelik (karar):** **AS9100 önce** (ilk müşteri havacılık), ISO 27001 yatay zemin.
- **İçgörü:** Müşterinin istediği **task manager → AS9100 zemini**, **SIEM → ISO 27001 zemini**. Yani yeni kapsam değil, mevcut işin paketlenmesi.
- **İlk hedef:** **NCR + CAPA** şablonları (Operation Core WorkItem tipleri). İç spec: müşterinin NCR/CAPA prosedür ve form şablonları.
- **Müşteri dokümanı (karar):** [AS9100_MUSTERI_OZET.md](./AS9100_MUSTERI_OZET.md) — bilgilendirme özeti (teklif/fiyat yok); ilk kurulum dili.
- **Devam noktası:** [DEVAM.md](./DEVAM.md) · Yol haritası: [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md)

> Bu klasör üst odak indeksine bağlıdır: [../README.md](../README.md). Platform vizyonu: [../operationcore/major_plan.md](../operationcore/major_plan.md).
