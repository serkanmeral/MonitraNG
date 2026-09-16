# RAID

**RAID**, projedeki Risk, Varsayım, Sorun ve harici Bağımlılık defteridir. Kararlar ayrı sekmededir. Gantt FS değildir. Monte Carlo yoktur.

## Dört tür

| Tür | Soru | Örnek |
|---|---|---|
| **Risk** | Olursa zarar | Ana tedarikçi gecikmesi |
| **Varsayım** | Doğru saydığımız şey | “Enerji kesintisi olmaz” |
| **Sorun** | Şu an olan aksaklık | Kargo gümrükte bekliyor |
| **Bağımlılık** | Dışarıda beklenen | Belediye kazı izni |

RAID bağımlılığı iki WBS arasındaki FS oku değildir. Dış dünyadır.

## Ekranda ne vardır?

Liste: tür (renkli çip), durum, etki, sahip, işlemler. Filtre çipleri de aynı dört türdür.

**RAID ekle** penceresi dört karttan başlar. Kartın altındaki cümle o türün sorusudur. Ad alanındaki örnek, seçilen türe göre değişir.

Risk seçilince olasılık ve etki açılır; skor canlı hesaplanır (`olasılık × etki`, 1–3). Etki yüksek veya skor 6 ve üzeriyse “Yüksek risk — Durum’da sayılır” görünür. Yanıt (kaçın / azalt / devret / kabul) yalnız risktedir. Diğer türlerde yalnızca etki vardır.

WBS bağlarsanız uyarı o teslimat satırına yapışır. Bağlamazsanız kayıt yalnız bu listede durur. Gantt FS oku çizilmez. Kapalı kayıt Durum sayacına girmez.

Durum örnekleri: açık, azaltılıyor, kapalı, doğrulandı, geçersiz, işlemde, bekliyor, çözüldü (türe göre).

## Durum sekmesine yansıma

Yüksek (elevated) açık risk → **Yüksek risk**. Açık sorun → **Açık sorun**. Bunlar uyarıdır; kapı gibi kapatma kilidi değildir.

## Örnek

Kayıt: “Jeneratör tedarikçisi 4 hafta gecikebilir”  
Tür: Risk, etki yüksek, olasılık orta, sahip: satın alma, etkilenen WBS: `3.2 Teslimatlar`, yanıt: azalt (yedek tedarikçi teklifi).

Aynı konuda kapsamı değiştiriyorsanız ayrıca **Kararlar**’a kapsam değişikliği yazın. RAID “olabilir / oldu”, karar “kabul ettik”tir.

İzin bekliyorsanız RAID bağımlılığı; WBS’te “izin sonrası montaj” sırası için ayrıca FS ekleyin. İkisi birlikte durabilir.

## Ne değildir?

- Karar defteri değildir
- Gantt oku değildir
- Silmek WBS’i silmez

## Sonraki adım

Riskin teslimatı durdurmasını istiyorsanız **Kapı** bağlayın. Sadece izlemek yetiyorsa RAID yeter.
