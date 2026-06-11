# Odak Üretim — Yeni emir

Bu form **Odak Üretim** workspace'inde yeni bir **üretim emri** (`ODF-…`) açmak içindir. Kayıt **Yeni** durumunda oluşur; ürün, miktar ve planlama bilgileri sonraki **Planla** geçişinde tamamlanır.

## Zorunlu alanlar

| Alan | Ne yazmalıyım? |
|------|----------------|
| **Başlık** | Emri tanımlayan kısa özet (ör. parça adı + müşteri referansı). Board ve profilde ana görünen metindir. |
| **Tip** | Varsayılan **Üretim emri** gelir; bu form için değiştirmeniz gerekmez. |
| **Öncelik** | Aciliyet (Normal / Yüksek / Acil). Planlama ve takip için kullanılır. |
| **Emir tipi** | Üretim sınıfı: **Seri üretim**, **FAI (ilk parça)**, **Rework** veya **Prototip**. Varsayılan *Seri üretim*'dir. |
| **Müşteri** | Lookup ile müşteri seçin (`odak_musteriler` kataloğu). |
| **Müşteri sipariş no** | Müşterinin PO / sipariş referans numarası (ör. `PO-2026-0142`). |

## Önerilen alan

| Alan | Açıklama |
|------|----------|
| **Açıklama** | Teknik not, teslimat beklentisi veya iç referans. Zorunlu değildir; profilden sonradan da eklenebilir. |

## Bu formda olmayan alanlar

Ürün grubu, ürün/parça, miktar ve planlanan tarih **kayıt açılışında istenmez**. Bunlar emir **Planla** geçişinde girilir:

- Ürün grubu → Ürün/parça (ürün listesi gruba göre filtrelenir)
- Miktar, planlanan tarih, emir tipi (gerekirse güncellenir)

Üretim, kalite, depo ve sevkiyat alanları ilgili aşama geçişlerinde doldurulur; profil ekranından salt okunur izlenebilir.

## Kayıt sonrası akış (özet)

1. **Kaydet** — Emir anahtarı otomatik üretilir (`ODF-0001`, …).
2. **Üretim panosu** → **Yeni** sütununda listelenir.
3. Profilden **Planla** → ürün ve plan bilgileri girilir → **Planlandı**.
4. **Üretime al** → **Üretimde** → **Kaliteye gönder** → … → **Sevkiyat** → **Kapat**.

Alternatif: acil işlerde **Doğrudan üretime al** geçişi planlama adımını atlayabilir (iş merkezi bilgisi gerekir).

## Emir tipi seçenekleri

| Değer | Ne zaman? |
|-------|-----------|
| **Seri üretim** | Standart seri sipariş |
| **FAI (ilk parça)** | İlk parça onayı / numune |
| **Rework** | Yeniden işleme / tamir |
| **Prototip** | Prototip / geliştirme lotu |

## İpuçları

- Başlıkta **ne üretileceğini**, müşteri sipariş no alanında **resmi PO referansını** kullanın.
- Müşteri katalogda yoksa önce master data (müşteriler) güncellenmelidir.
- İptal için profilden **İptal et** geçişi kullanılabilir (henüz üretime alınmamış emirler).
- Sorun yaşarsanız workspace yöneticisine emir anahtarını (`ODF-…`) iletin.
