# Raporlama Servisi — Vizyon Notları

**Tarih:** 2 Mart 2026  
**Durum:** 📋 Ön notlar — detaylı planlama sonraya bırakıldı

---

## 1. Genel Konsept

Ayrı bir **raporlama servisi** — kullanıcılar kendi raporlarını ve sayfa yapılarını **dinamik olarak** tanımlayabilir.

---

## 2. Rapor Tanımı

| Öğe | Açıklama |
|-----|----------|
| **Veri kaynağı** | MongoDB aggregate pipeline |
| **Tanımlama aracı** | **Wizard** — aggregate oluşturan araç |
| **Kullanıcı** | Kişiler kendi raporlarını kendileri tanımlar |

---

## 3. Sayfa Yapısı

- Sayfa **formatı** ve benzeri bilgiler tanımlanabilir
- Sayfa yapısı da kullanıcı tarafından dinamik tanımlanır

---

## 4. Tetikleme Modları

| Mod | Açıklama |
|-----|----------|
| **Manuel** | Kullanıcı trigger — rapor anında oluşturulur |
| **Zamanlanmış** | MngScheduler veya benzeri — periyodik görev |

**Zamanlama seçenekleri (taslak):**
- Her gün belirli bir saatte
- Haftalık
- (Diğer periyotlar planlama aşamasında)

---

## 5. Çıktı Hedefleri

| Hedef | Açıklama |
|-------|----------|
| **MinIO** | Oluşan raporlar belirli bir klasörde saklanır |
| **E-posta** | Belirli kişilere mail olarak gönderilir |

---

## 6. Çıktı Formatı

- **Şu an:** Sadece **PDF** yeterli

---

## 7. İlişkili Kavramlar

- TCDD GIS raporlama ihtiyaçları bu servisle karşılanabilir (ileride)
- MngScheduler — zamanlanmış görevler için
- MngDataGateway / MongoDB — veri kaynağı
- MngNotifier — e-posta gönderimi
- MinIO — dosya depolama

---

## 8. Sonraki Adımlar

- [ ] Detaylı planlama oturumu
- [ ] Servis mimarisi
- [ ] Wizard ve aggregate builder tasarımı
- [ ] Sayfa yapısı şema tasarımı
