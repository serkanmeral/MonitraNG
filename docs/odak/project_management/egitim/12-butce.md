# Bütçe

**Bütçe**, iş paketi zarfıdır: planlanan ve gerçekleşen tutar. Fatura, muhasebe ve kur dönüşümü yoktur.

## Ekranda ne vardır?

Üstte WBS **paket tablosu**, altta **kalem tablosu**. İkisi de arama ve sayfalıdır; kart listesi yoktur. TRY ve USD karışıksa üst toplam gösterilmez (çeviri yoktur).

Paket: teslimat, aşım/uygun, plan, gerçekleşen, kalan. Satır açılınca o WBS’teki kalemler görünür.

**Kalem ekle** penceresi dört kategoriden (işçilik / malzeme / taşeron / diğer) kartla başlar. Plan ve gerçekleşen yazılınca kalan canlı hesaplanır; aşımda “Durum’da sayılır” uyarısı çıkar.

Kalem: ad, kategori, para birimi, plan, gerçekleşen, kalan, not, WBS.

## Örnek

Kalem: `Saha kablolama`  
Kategori: Malzeme  
Plan: 80.000 TRY  
Gerçekleşen: 95.000 TRY  
Kalan negatif görünür, aşım chip’i yanar.

Bu bir muhasebe fişi değildir. Tedarikçi faturası Kütüphane’ye yüklenip kanıt olarak bağlanabilir; bütçe satırı yine elle güncellenir.

## Ne değildir?

- Fatura uygulaması değildir
- Otomatik kur çevirmez (USD ve TRY ayrı kalemler olabilir)
- Kaynak saatinden maliyet üretmez

## Sonraki adım

Aşım bir kapsam kararıysa **Kararlar**. Teslimatı durduracaksa **Kapı**.
