# SIEM-Hafif — Dijital Banka / Finans Dikeyi (Kapsam Notu)

**Durum:** Taslak — iç değerlendirme; müşteri sunumu değil
**Son güncelleme:** 3 Haziran 2026
**Ana plan:** [SIEM_PLANNING.md](./SIEM_PLANNING.md)

> Enpara benzeri **online / dijital banka** kurulumlarında MonitraNG'nin **nerede değer ürettiği**, **nerede yetmediği** ve **nasıl konumlandırılması gerektiği**. Regülasyon (BDDK vb.) için **hukuki danışmanlık değildir** — teknik ürün kapsamı notudur.

---

## 1. Konumlandırma (tek cümle)

MonitraNG, dijital bankanın **tüm güvenliğini** değil; **iç IT / altyapı siber operasyonları** (log toplama, hedefli tespit, kontrollü müdahale, olay kaydı) için **SIEM-hafif tamamlayıcı katman** olarak konumlandırılır.

---

## 2. Dijital bankanın güvenlik alanları

### 2.1 Regülasyon ve denetim (Türkiye — BDDK vb.)

| Beklenti | MonitraNG katkısı |
|----------|-------------------|
| Bilgi sistemlerinde loglama, olay izleme | 🟡 Merkezi `sec_events`, retention planı (SIEM §9) |
| Güvenlik olayı kaydı ve eskalasyon | 🟡 Alarm + Operation Core WorkItem (Faz 2–3) |
| Erişim kontrolü kanıtı | 🟡 AD/bastion logları (U1/U2) |
| BGYS / sertifikasyon | ❌ Kurum süreci + danışmanlık; ürün tek başına yeterli değil |

### 2.2 Müşteri kanalı (mobil, web, open banking API)

| Beklenti | MonitraNG katkısı |
|----------|-------------------|
| MFA, OTP, cihaz bağlama | ❌ Banka uygulama / IAM stack'i |
| API gateway, WAF, bot koruması | ❌ Ürün olarak yok; **log entegrasyonu** mümkün (syslog) |
| Müşteri credential stuffing (kanal) | 🟡 API GW / WAF logları verilirse kısmi (U1 benzeri) |

### 2.3 Dolandırıcılık ve işlem güvenliği (Fraud / AML)

| Beklenti | MonitraNG katkısı |
|----------|-------------------|
| Gerçek zamanlı işlem skorlama | ❌ Ayrı fraud engine |
| AML / MASAK şüpheli işlem | ❌ Ayrı AML platformu |
| Davranış analizi (müşteri profili) | ❌ Kapsam dışı |

### 2.4 Altyapı ve siber operasyon (SIEM/SOC)

| Beklenti | MonitraNG katkısı |
|----------|-------------------|
| Firewall, AD, VPN, bastion logları | ✅ SIEM-hafif çekirdek |
| Brute-force, yetkisiz erişim (iç) | ✅ U1, U2 |
| Sınır / segmentasyon ihlali | ✅ U4 |
| Onaylı müdahale (iç IP blok) | 🟡 Faz 3 (Workflow + firewall API) |
| 7/24 SOC | ❌ Hizmet ayrı (iç veya partner) |

### 2.5 Veri, PCI, KVKK

| Beklenti | MonitraNG katkısı |
|----------|-------------------|
| HSM, KMS, kart verisi (CHD) | ❌ |
| PCI-DSS ortam araçları | ❌ Kısmi log desteği |
| KVKK — log maskeleme | 🟡 Politika + alan minimizasyonu (SIEM §9) |
| PAM (privilege erişim) | 🟡 Jump host + AD izi |

### 2.6 Uygulama ve SDLC güvenliği

| Beklenti | MonitraNG katkısı |
|----------|-------------------|
| SAST/DAST, dependency scan | ❌ DevSecOps araçları |
| Secrets / vault | ❌ |
| K8s / bulut güvenlik posture (CSPM) | ❌ MVP dışı; audit log ingest Faz 2+ değerlendirme |

---

## 3. Karşılanabilirlik özeti

| Banka ihtiyacı | Durum | Not |
|----------------|:-----:|-----|
| İç IT log toplama (FW, AD, bastion, sunucu) | ✅ | Hibrit toplama §5 |
| Brute-force / iç erişim tespiti | ✅ | U1, U2 |
| Firewall deny / sınır alarmı | ✅ | U4 |
| Timeline / olay arama | 🟡 | Faz 2 UI |
| Onaylı IP blok | 🟡 | Faz 3 |
| Incident kaydı | 🟡 | Operation Core |
| ISO 27001 A.8.15 / A.8.16 kanıt desteği | 🟡 | Tam sertifika değil |
| Müşteri işlem fraud | ❌ | |
| AML | ❌ | |
| Müşteri auth / MFA | ❌ | |
| WAF / API GW (ürün) | ❌ | Log kaynağı olabilir |
| BDDK “tam uyum paketi” | ❌ | |
| Splunk/QRadar seviyesi SIEM kütüphanesi | ❌ | SIEM-hafif |

---

## 4. Banka için değerli senaryolar (MonitraNG)

| ID | Senaryo | Kaynak | SIEM plan |
|----|---------|--------|-----------|
| B1 | Ops / admin brute-force | AD, bastion | U1 |
| B2 | Fail→success iç oturum | AD | U2 |
| B3 | Prod segmentine beklenmeyen trafik | Firewall | U4 |
| B4 | Jump host dışı prod erişim denemesi | FW + bastion | U4 + T1 |
| B5 | Denetim: kim, ne zaman, hangi IP | `sec_events` + alarm audit | §4, §9 |
| B6 | Veri lokasyonu (on-prem ingest) | Dağıtım kararı | On-prem |

---

## 5. Tipik banka yığını (referans)

```text
┌─────────────────────────────────────────────────────────┐
│  Fraud / AML engine              ← MonitraNG DEĞİL     │
│  Core banking, ödeme HSM         ← MonitraNG DEĞİL     │
│  WAF, API GW, mobil güvenlik     ← ayrı (log verebilir) │
│  EDR / CSPM                      ← ayrı                  │
├─────────────────────────────────────────────────────────┤
│  SIEM-hafif (iç IT log + alarm)  ← MonitraNG            │
├─────────────────────────────────────────────────────────┤
│  Incident / doküman / workflow   ← Operation Core + WF │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Minimum banka POC kapsamı (öneri — Odak/lab)

Savunma tedarikçisi T0+T1 mantığıyla hizalı, **iç IT odaklı** pilot:

| Bileşen | Kapsam |
|---------|--------|
| Kaynak | 1 firewall (syslog deny), 1 DC veya WEC test (4625), opsiyonel 1 Linux sshd |
| Kurallar | U1, U4 (U2 Faz 2) |
| Müdahale | Alert only (Faz 1); onaylı blok Faz 3 |
| Fraud / kanal | **Dahil değil** |

---

## 7. Satış / iç iletişim dili

**Söylenebilir:**
- İç IT ve altyapı güvenlik olaylarını merkezileştirir.
- Brute-force ve sınır ihlali gibi hedefli senaryolarda alarm üretir.
- On-prem veri; regülasyon kanıtına **teknik katkı** (log/izleme maddeleri).

**Söylenmemeli:**
- “Bankanın tüm güvenliğini çözeriz.”
- “Fraud / AML yerine geçeriz.”
- “BDDK uyumunu ürünle garanti ederiz.”

---

## 8. Sonraki adımlar (bu dikey için)

1. Gerçek banka fırsatında **kaynak envanteri** (§20 şablonu + API GW log formatı).
2. BDDK / BGYS danışmanı ile **hangi maddelere kanıt** sunulacağını eşleştirme.
3. Bulut (AWS/Azure) audit log ingest — ayrı epik (MVP dışı).

---

## 9. Referanslar

- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- `docs/content/security/CYBERSECURITY_SOLUTION_PLANNING.md`
- `docs/odak/compliance/ISO27001_PLAN.md`
