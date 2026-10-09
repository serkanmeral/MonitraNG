# RAID

**RAID**, projedeki risk, varsayım, sorun ve harici bağımlılık defteridir. Dört harf dört türdür. Kararlar ayrı sekmededir. Gantt’taki FS oku ayrı sekmededir. Olasılık dağılımı ve simülasyon yoktur.

Soldaki menüde **Kontrol → RAID**.

| Tür | Soru | Örnek |
|---|---|---|
| Risk | Henüz olmadı. Olursa zarar. | Jeneratör tedarikçisi dört hafta gecikebilir. |
| Varsayım | Doğru saydığımız şey. | Şantiye enerjisi kesilmez. |
| Sorun | Şu an olan aksaklık. | Kick-off tutanak imzası eksik. |
| Bağımlılık | Dışarıda beklenen. | Belediye kazı izni. |

RAID bağımlılığı iki WBS arasındaki FS oku değildir. Dış dünyadır. İzin bekliyorsanız buraya yazın. “İzin sonrası montaj” sırası için ayrıca FS ekleyin. İkisi birlikte durabilir.

## Liste

Liste boşsa “Henüz RAID kaydı yok” yazar. Üstte **RAID ekle** ve tür süzgeçleri vardır: **Tümü**, **Risk**, **Varsayım**, **Sorun**, **Bağımlılık**.

Satırda tür çipi, durum, etki, sahip ve işlemler durur. Kapalı kayıt Durum sayacına girmez.

## Pencere

**RAID ekle** dört karttan başlar. Kartın altındaki cümle o türün sorusudur. Ad kutusunun örneği seçilen türe göre değişir.

| Alan | Ne zaman | Ne işe yarar |
|---|---|---|
| Tür | Her zaman | Risk, varsayım, sorun veya bağımlılık. Tür değişince durum listesi de değişir. |
| Ad | Her zaman | Kısa başlık. Risk örneği: `Jeneratör tedarik gecikmesi`. |
| Açıklama | Her zaman | Ne olabilir, ne varsayıldı, ne oldu. |
| Olasılık | Yalnız risk | Düşük, orta, yüksek. 1, 2, 3 diye sayılır. |
| Etki | Her zaman | Düşük, orta, yüksek. |
| Skor | Yalnız risk | Olasılık × etki. Canlı hesaplanır. 1 ile 9 arası. Etki yüksek veya skor 6 ve üzeriyse “Yüksek risk — Durum’da sayılır” görünür. Değilse “Defterde kalır” yazar. |
| Yanıt | Yalnız risk | **Kaçın**, **Azalt**, **Devret**, **Kabul** veya yok. Diğer türlerde bu kutu yoktur. |
| Sahip | Her zaman | Kim izliyor. Giriş hesabı olmak zorunda değildir. Ad yazılır. |
| Tarih | Her zaman | İzleme tarihi. |
| Etkilenen WBS | Her zaman | Bağlarsanız uyarı o kalemin satırına yapışır. Boşsa kayıt yalnız bu listede durur. Gantt oku çizilmez. |
| Durum | Düzenlerken | Türe göre değişir. Aşağıdaki tablo. |

| Tür | Durumlar |
|---|---|
| Risk | Açık, azaltılıyor, kapalı |
| Varsayım | Açık, doğrulandı, geçersiz |
| Sorun | Açık, işlemde, çözüldü |
| Bağımlılık | Açık, bekliyor, kapalı |

Kapalı, doğrulandı, geçersiz ve çözüldü Durum sayacına girmez. Açık sorun **Açık sorun** olur. Yüksek açık risk **Yüksek risk** olur. İkisi de uyarıdır. Kapı gibi kapatma kilidi değildir.

Silmek “Bu RAID kaydı silinecek. WBS ve işler silinmez” der.

## Örnek

Atölye kabulüne bunu yazmayın. Yürüyüşteki Durum listesi yalnız açık kapı, iş bağsız ve kanıt yok göstersin. Kendi defteriniz için:

Kayıt: `Jeneratör tedarikçisi 4 hafta gecikebilir`  
Tür: Risk. Etki yüksek. Olasılık orta. Skor 6. Sahip: satın alma. Yanıt: azalt.  
Etkilenen WBS: teslimat kalemi.

Aynı konuda kapsamı değiştiriyorsanız ayrıca **Kararlar**’a kapsam değişikliği yazın. RAID “olabilir”. Karar “kabul ettik”tir.

## Adımlar

1. **RAID** sekmesini açın. Atölye kabulünde listenin boş olduğunu doğrulayın.
2. Deneme için **RAID ekle**. Risk kartını seçin.
3. Adı yazın. Olasılık orta, etki yüksek olsun. Skorun 6 olduğunu ve yüksek risk cümlesinin çıktığını görün.
4. **Vazgeç** deyin. Atölye kaydını bozmayın. Kendi projenizde **Kaydet**.

## Ne değildir?

- Karar defteri değildir.
- Gantt oku değildir.
- Silmek WBS’i silmez.
- Yüksek risk, işi kilitlemez. Kilidi istiyorsanız **Kapı** bağlayın.

Sıradaki: [Kaynak](./14-kaynak.md)
