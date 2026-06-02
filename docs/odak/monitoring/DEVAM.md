# DEVAM — SIEM-Hafif Planlama (Kaldığımız Yer)

**Son güncelleme:** 1 Haziran 2026, ~21:40
**Durum:** ▶️ Workflow taslağı netleşti — **devam edilebilir** (bağımlılık çözüldü)

---

## 1. Tek cümlede durum

SIEM-hafif planının Faz 0 (çerçeve) çıktısı hazır ([SIEM_PLANNING.md](./SIEM_PLANNING.md)); workflow taslağı tamamlandı ([Workflow Backend Implementation Plan v1](../workflow/Workflow%20Backend%20Implementation%20Plan%20v1.md)) ve **Faz 2–3'ü bloke eden iki karar (§12.1 korelasyon motoru, §8 onaylı müdahale) çözüldü.**

---

## 2. Bağımlılık çözümü (workflow taslağı tamamlandı)

Workflow taslağı netleşti ve SIEM'i bloke eden iki konu kapatıldı:

- **SIEM_PLANNING.md §12.1 — Korelasyon motoru:** ✅ **KARAR → ayrı CEP/tespit bileşeni (`MngCorrelator`).** Workflow per-instance orkestrasyon motorudur; stateful kayan-pencere korelasyonu onun işi değildir. Korelatör `sec_events` akışından alert üretir, workflow Event Trigger ile tüketir.
- **SIEM_PLANNING.md §8 — Onaylı müdahale:** ✅ **Tamamen MngWorkflow ile karşılanıyor** (Event Trigger → Approval → Block IP/Engine komutu → Delay-TTL/Unblock → audit → WorkItem). Eşleme: Workflow Plan §12.2.

**Temiz seam:** İki sistem RabbitMQ alert event'i üzerinden bağlanır (SIEM §5 `Corr -->|alert| WF`). Detay: Workflow Plan §12.

---

## 3. Kilitli kararlar (değişmedi)

| Konu | Karar |
|------|-------|
| Ürün kapsamı | SIEM-hafif (hedefli senaryolar, kademeli derinleştir) |
| İlk faz kaynakları | Firewall (syslog) · AD/login · Sunucu/endpoint · Bastion/VPN |
| Dağıtım | On-prem |
| Tespit sonrası | Onaylı müdahale (operatör onayı; otomatik kalıcı blok yok) |
| Uyum hedefi | ISO/IEC 27001 |

---

## 4. Üretilen dosyalar

| Dosya | İçerik |
|-------|--------|
| [README.md](./README.md) | Klasör index + kapsam kararları |
| [SIEM_PLANNING.md](./SIEM_PLANNING.md) | Ana plan: yetenek eşlemesi, gap analizi, `sec_events` modeli, mimari akış, U1–U7 senaryolar, korelasyon kural modeli, onaylı müdahale, retention, ISO katkısı, 4 fazlık yol haritası, açık kararlar |
| DEVAM.md | Bu dosya |

**Çapraz bağlar eklendi:** `docs/odak/README.md` (ağaç + tablo), `docs/odak/compliance/README.md` (modül haritası).

---

## 5. Workflow planlaması bitti — sıradaki adımlar

1. ✅ **§12.1 kapatıldı:** Korelasyon motoru = ayrı CEP bileşeni (`MngCorrelator`). SIEM_PLANNING.md güncellendi.
2. ✅ **§8 netleşti:** Onaylı müdahale akışı MngWorkflow ile karşılanıyor (eşleme Workflow Plan §12.2); SIEM_PLANNING.md §8'e not düşüldü.
3. ✅ **Tespit motoru taslağı çıkarıldı:** SIEM-özel `MngCorrelator` yerine platform geneli **Alarm & Rule Engine** olarak genelleştirildi → `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md`. SIEM korelasyonu (U1/U2/U4) bu motorun bir kural ailesi.
4. ⏭️ **Faz 2 senaryolarını** (U1/U2/U4) Alarm Engine kural modeliyle (`mon_alarm_rules`) somutlaştır.
5. ⏭️ Gerekirse `MONITORING_WORKFLOW.md` (IFTTT planı) ile yeni workflow engine arasındaki ilişkiyi netleştir (yerine geçiş / üstüne biniş / ayrı).

---

## 6. Workflow'dan bağımsız ilerletilebilecekler (istenirse)

Bunlar workflow kararını **beklemez** (SIEM_PLANNING.md Faz 0–1):

- `sec_events` veri modelinin kesinleştirilmesi (§4)
- Engine syslog/Windows Event listener teknik spike (§5)
- Reactor normalizer / parser tasarımı (§5)
- Müşteri kaynak envanteri + örnek log toplama (§13)

> Not: Kullanıcı tercihi **"önce workflow"** olduğu için yukarıdakiler de şimdilik beklemede; bu liste, istenirse paralel ilerleme için referanstır.

---

## 7. Açık kararlar (SIEM_PLANNING.md §12 — özet)

1. Korelasyon motoru: Workflow genişletme vs `MngCorrelator` → **workflow taslağına bağlı**
2. Syslog toplama yeri: Engine içi vs ayrı collector
3. Firewall pilot marka (API + kimlik bilgisi) — müşteri envanteri gerekli
4. `sec_events` store: MongoDB yeterli mi, OpenSearch değerlendirilecek mi
5. Retention süreleri + WORM gereksinimi
6. Baseline (U7) süresi + yanlış-pozitif politikası

---

## 8. İlgili dokümanlar

- Ana plan: [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- Mevcut IFTTT workflow planı: `docs/content/monitoring_plans/MONITORING_WORKFLOW.md`
- Ürün geneli güvenlik vizyonu: `docs/content/security/CYBERSECURITY_SOLUTION_PLANNING.md`
- ISO 27001: `docs/odak/compliance/ISO27001_PLAN.md`
