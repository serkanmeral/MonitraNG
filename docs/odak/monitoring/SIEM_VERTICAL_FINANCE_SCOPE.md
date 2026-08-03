# SIEM-Hafif — Dijital Banka Kapsam Matrisi (Hazır / Planlı / Yapılmayacak)

**Durum:** İç değerlendirme + müşteri sunumu hazırlığı  
**Son güncelleme:** 11 Temmuz 2026  
**İlgili:** [SIEM_VERTICAL_FINANCE.md](./SIEM_VERTICAL_FINANCE.md) · [SIEM_ROADMAP.md](./SIEM_ROADMAP.md) · [DEVAM.md](./DEVAM.md)

> Enpara tarzı **online / dijital banka** müşterisine sunumda: MonitraNG’nin **şu an cevap verdiği**, **ileride planladığı** ve **bilerek yapmayacağı** alanlar. Regülasyon (BDDK vb.) için hukuki danışmanlık değildir.

---

## 1. Konumlandırma (tek cümle)

MonitraNG, dijital bankanın **tüm güvenliğini** değil; **iç IT / altyapı siber operasyonları** (log toplama, hedefli tespit, kontrollü müdahale, olay kaydı) için **SIEM-hafif tamamlayıcı katman**dır.

```text
┌─────────────────────────────────────────────────────────┐
│  Fraud / AML · Core banking · HSM          ← YAPILMAYACAK │
│  WAF / API GW ürün · MFA/OTP ürün          ← YAPILMAYACAK │
│  (log entegrasyonu mümkün)                 ← PLANLI / kısmi │
├─────────────────────────────────────────────────────────┤
│  SIEM-hafif (iç IT log + alarm + panel)    ← HAZIR        │
│  Onaylı müdahale (workflow)                ← HAZIR (pilot)│
│  5651 / WORM / derin uyum arşivi           ← PLANLI       │
│  Incident → OC iş kaydı                    ← PLANLI       │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Hazır olanlar (bugün sunulabilir)

Odak’ta doğrulanmış SIEM-hafif hattı: **ingest → `sec_events` → kural → alarm → operatör UI** (+ onaylı workflow aksiyonları).

| Alan | Ne var? | Kanıt / not |
|------|---------|-------------|
| Merkezi log toplama | Firewall syslog, Windows (WEF/WEC/NxLog), Linux rsyslog | Hibrit toplama; Engine + Reactor |
| Olay deposu | `sec_events` | Normalize + ham alan |
| Hedefli tespit | U1–U10 senaryo paketi (brute-force, fail→success, FW deny/spike, yetki, hesap/grup vb.) | Alarm kuralları + E2E |
| Alarm Merkezi | Açık alarmlar, lifecycle (ack/suppress/resolve), kurallar UI | Güvenlik Yönetimi menüsü |
| SIEM paneli | Dashboard + olay arama | `/apps/siem-center` |
| Onaylı müdahale | `alarm.raised` → Workflow → onay → örn. IP blok | Pilot E2E (U1/U4) |
| On-prem veri | Loglar müşteri ortamında | Dağıtım modeli |
| Banka senaryoları (anlatım) | B1–B6 (iç brute-force, sınır ihlali, denetim izi, jump host vb.) | [SIEM_VERTICAL_FINANCE.md](./SIEM_VERTICAL_FINANCE.md) §4 |

**Sunum dili (hazır):**  
İç IT ve altyapı güvenlik olaylarını merkezileştirir; brute-force ve sınır ihlali gibi hedefli senaryolarda alarm üretir; operatör paneli ve onaylı müdahale hattı vardır; veri on-prem kalır.

---

## 3. Gelecekte yapılması planlananlar

İki katman: **yol haritasında olanlar** ve **dikey olarak değerlendirilebilir uzantılar** (henüz kilitli ürün kararı değil).

### 3.1 Yol haritasında / sıradaki (SIEM ürün hattı)

| Öncelik | Konu | Durum | Not |
|---------|------|--------|-----|
| Yüksek | Alarm → Operation Core iş kaydı | Planlı | Incident / eskalasyon operasyon değeri |
| Yüksek | Operatör olgunlaştırma | Planlı | Hub bildirim, runbook/deep link, lifecycle backfill |
| Orta | Sequence kural adım düzenleme | Planlı | Backend update + UI |
| Orta | SIEM ops sertleştirme | Planlı | Kaynak etiketleri, whitelist vb. |
| Ertelenmiş (ihtiyaç doğunca) | 5651 / WORM arşiv | Spike var, kod yok | [SIEM_WORM_5651_SPIKE.md](./SIEM_WORM_5651_SPIKE.md) |
| Ertelenmiş | Denetim / uyum rapor paketleri | Planlı (Faz 5) | ISO/BGYS maddelerine **teknik kanıt** |
| Uzun vade | LogAlarm / tam SIEM paritesi | Bilinçli ertelenmiş | Geniş kaynak, arama, sertifikasyon — [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) |

### 3.2 Dikey olarak ileride geliştirilebilir (ayrı epik / ürün kararı gerekir)

Bunlar “yapılmayacak” değil; **SIEM-hafif çekirdeğin doğal uzantısı veya komşu ürün** olarak değerlendirilebilir.

| Alan | Ne yapılabilir? | Ne yapılmaz? |
|------|-----------------|--------------|
| WAF / API GW **loglarından** tespit | Credential stuffing benzeri kurallar (log verilirse) | WAF/API GW **ürünü** yazmak |
| Fraud-lite | İşlem/API loglarında korelasyon / anomali alarmı | Gerçek zamanlı banka fraud motoru |
| BGYS / BDDK **kanıt üretimi** | Rapor, retention, olay kaydı, audit trail | “BDDK uyumunu ürünle garanti” |
| CSPM / bulut audit ingest | Cloud audit log + kurallar | Tam bulut güvenlik platformu |
| Secrets / vault benzeri | Ayrı IAM/güvenlik ürünü | SIEM’in parçası sanmak |
| 7/24 SOC | Platform üstünde **hizmet** (MSSP) | Yazılım özelliği olarak “SOC motoru” |

**Önerilen iç öncelik sırası:** SIEM derinleştirme → incident/kanıt → kanal log senaryoları → (fırsat olursa) fraud-lite / CSPM.

---

## 4. Yapılmayacaklar (bilinçli kapsam dışı)

Bunlar müşteriye **açıkça söylenmemeli** veya “partner / ayrı ürün” diye ayrılmalı.

| Alan | Neden yapılmayacak? |
|------|---------------------|
| **Fraud engine (tam)** | Gerçek zamanlı işlem skorlama, kart/ödeme entegrasyonu — ayrı uzman ürün |
| **AML / MASAK motoru** | Tipoloji + yasal raporlama + sorumluluk — danışmanlık + uzman AML |
| **Müşteri MFA / OTP / cihaz bağlama** | Banka kanal IAM / mobil stack — Keeper tarzı platform IAM’den farklı ürün |
| **WAF / API GW (ürün olarak)** | Ağ/güvenlik appliance; rekabetçi ürün yazmak mantıklı değil |
| **HSM / PCI CHD araçları** | Donanım + sertifikasyon |
| **SAST / DAST motoru** | Genelde entegre edilir, sıfırdan yazılmaz |
| **“BDDK / BGYS’yi ürünle garanti ederiz”** | Hukuk + kurum süreci; yazılım tek başına yetmez |
| **Splunk / QRadar seviyesinde hazır kural kütüphanesi (hemen)** | Bilinçli SIEM-hafif; parite uzun vade ve ayrı yatırım |

---

## 5. Karşılanabilirlik özeti (tek bakış)

| Banka ihtiyacı | Sınıf |
|----------------|:-----:|
| İç IT log (FW, AD, bastion, sunucu) | ✅ Hazır |
| Brute-force / iç erişim tespiti | ✅ Hazır |
| Firewall deny / sınır / trafik anomalisi | ✅ Hazır |
| Timeline / olay arama / dashboard | ✅ Hazır |
| Onaylı IP blok (workflow) | ✅ Hazır (pilot) |
| Incident → OC iş kaydı | 🟡 Planlı |
| 5651 / WORM / derin uyum arşivi | 🟡 Planlı (ertelenmiş) |
| ISO/BGYS teknik kanıt raporları | 🟡 Planlı |
| WAF/API loglarından kanal tespiti | 🟡 Planlı (entegrasyon) |
| Fraud-lite (log tabanlı) | 🟡 Değerlendirilebilir |
| Müşteri işlem fraud (tam) | ❌ Yapılmayacak |
| AML / MASAK | ❌ Yapılmayacak |
| Müşteri auth / MFA ürünü | ❌ Yapılmayacak |
| WAF / API GW ürünü | ❌ Yapılmayacak |
| HSM / PCI araçları | ❌ Yapılmayacak |
| BDDK “tam uyum paketi” | ❌ Yapılmayacak |

---

## 6. Satış / sunum dili

**Söylenebilir**
- İç IT ve altyapı güvenlik olaylarını merkezileştiririz.
- Hedefli senaryolarda (brute-force, sınır ihlali vb.) alarm üretiriz.
- Operatör paneli, olay arama ve onaylı müdahale hattımız vardır.
- On-prem; regülasyon maddelerine **teknik katkı** (log/izleme/olay kaydı).

**Söylenmemeli**
- “Bankanın tüm güvenliğini çözeriz.”
- “Fraud / AML yerine geçeriz.”
- “BDDK uyumunu ürünle garanti ederiz.”
- “Hazır 5651 / WORM arşivimiz var.” (henüz yok — planlı)

---

## 7. Minimum banka POC (öneri)

| Bileşen | Kapsam |
|---------|--------|
| Kaynak | 1 firewall (syslog deny), 1 DC/WEC (4625), opsiyonel Linux sshd |
| Kurallar | U1, U4 (U2 sequence demo) |
| UI | Alarm Merkezi + SIEM paneli |
| Müdahale | Alert only veya onaylı blok (ortama göre) |
| Fraud / kanal / AML | **Dahil değil** |

---

## 8. Referanslar

- [SIEM_VERTICAL_FINANCE.md](./SIEM_VERTICAL_FINANCE.md) — dikey kapsam notu (alanlar, B1–B6)
- [SIEM_ROADMAP.md](./SIEM_ROADMAP.md) — ürün fazları
- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) — parite / konumlandırma
- [SIEM_WORM_5651_SPIKE.md](./SIEM_WORM_5651_SPIKE.md) — uyum arşivi spike
- [DEVAM.md](./DEVAM.md) — güncel implementasyon durumu
