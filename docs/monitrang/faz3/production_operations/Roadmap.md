# Production Operations — Roadmap (Faz 3)

**Teklif:** §4.4 Üretim Operasyonu  
**Klasör:** `docs/monitrang/faz3/production_operations/`  
**Durum:** Planlama iskeleti  
**Son güncelleme:** 13 Temmuz 2026

---

## 1. Amaç

Fiziksel ürün üretimi için Operation Core **Üretim workspace**; Monitoring asset verilerinin emir/süreçte kullanımı.

**Dahil değil:** Tam MES; IT helpdesk senaryosu.

## 2. Kapsam özeti (tekliften)

| Alan | Madde |
|:---|:---|
| Workspace | 1× Üretim OC workspace; dinamik tip/durum/akış/form |
| Emir | Üretim emri yaşam döngüsü; isteğe bağlı NCR |
| Köprü | Sensör görünürlük + alarm→emir notu (standart) |
| Wow | Canlı şerit, bağlamlı açıklama, vardiya özeti, deep link |
| İş paketi | İsteğe bağlı parametre (zorunlu değil) |
| Opsiyon | Süreç tetiki / NCR taslak (O8) |

## 3. Mevcut / yeni

| | Durum (kabaca) |
|:---|:---|
| Odak Üretim workspace seed | Referans / şablon adayı |
| Monitoring ↔ WI köprüsü | **Yeni entegrasyon** |
| Üretim wow seti | Planlanacak — AI özet/bağlam: [../ai_platform/Roadmap.md](../ai_platform/Roadmap.md) |

## 4. Fazlar (taslak)

| Faz | Hedef |
|:---|:---|
| PROD-0 | Gap: mevcut Odak Üretim ↔ teklif |
| PROD-1 | Workspace iskeleti (emir akışı ± NCR) |
| PROD-2 | Asset↔emir eşlemesi + canlı görünüm |
| PROD-3 | Alarm/anomaly → emir notu + bildirim |
| PROD-4 | Wow: vardiya özeti, deep link, bağlamlı AI |
| PROD-O | O8 süreç tetiki / NCR taslak |

## 5. Bağımlılıklar

- **Monitoring** (asset / metrik / alarm) — önce veya paralel  
- **ai_platform** — PROD-3/4 anomaly açıklama / özet (omurga hazır olunca)  
- MngOperations, Mng.Ui OC  
- [../MIGRATION.md](../MIGRATION.md) — OC seed script’leri kritik

## 6. Kabul (özet)

Fiziksel üretim workspace canlı; sensör yoksa simulator ile demo; tam MES yok.

---

İş takibi: [work.md](./work.md)
