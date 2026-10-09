# İş kırılımı

**WBS**, teslimatın ağacıdır. Ekrandaki adı **WBS**’dir. Açıklaması şöyledir: teslimatı üstten alta iş paketlerine böler. Kodlar kendiliğinden numaralanır: 1, 1.1, 1.2. Kodu elle yazmazsınız.

Soldaki menüde **Plan → WBS**.

Üç tür vardır:

| Tür | Ne işe yarar | Gantt |
|---|---|---|
| Özet | Klasör gibidir. Altındakilerin ilerlemesini toplar. Yaprak iş buna bağlanmaz. İlerleme alt kalemlerden gelir. | Kendi çubuğu yoktur. Gruptur. |
| Görev | Yapılacak iştir. İsterseniz Operasyon Merkezi’ndeki bir iş kaydına bağlanır. | Çubuk, plan başlangıç ile plan bitiş arasındadır. |
| Kilometre taşı | Bir andır, süre değildir. | Elmastır. Plan bitiş o gündür. |

## Liste

Üstte arama ve bir durum süzgeci vardır.

| Süzgeç | Ne gösterir |
|---|---|
| Tümü | Bütün kalemler. |
| İş bağlı | Bir OC iş kaydına bağlanmış olanlar. |
| İş bağsız | Bağlanmamış yapraklar. Bu sette 1.1 burada durur. |
| Kanıt yok | Bağlı işi olup kanıt belgesi olmayanlar. İş yoksa kanıt da sayılmaz. |
| Sapma | Baseline alındıktan sonra plan tarihi kayanlar. Baseline yoksa bu süzgeç boştur. |

Boş süzgeçte “Bu filtrede WBS kalemi yok” yazar.

Satırda kod, ad, tür, tarihler, yüzde ve bağlı iş görünür. **Alt kalem**, seçili kalemin altına yeni düğüm açar. **Düzenle** aynı pencereyi açar. Çöp kutusu kalemi, alt kalemlerini ve bağlı bağımlılıklarını siler. Kütüphanedeki belgeyi silmez.

## Kalem penceresi

Ad boş bırakılmamalıdır. Pencere bölüm bölüm açılır.

| Alan | Ne işe yarar |
|---|---|
| Tür | Özet, görev veya kilometre taşı. Türün altında bir cümle o türün ne olduğunu söyler. |
| Ad | Görünen ad. Örnek: `Kontrol listesi`. |
| Üst kalem | Boşsa kalem kök olur ve kodu 1, 2, 3 diye gider. Üst seçilirse kod 1.1 gibi olur. |
| Plan başlangıç | Gantt çubuğunun başı. Sapma da buna bakar. |
| Plan bitiş | Çubuğun sonu. Kilometre taşında bu gün elmastır. |
| Gerçekleşen başlangıç | İşin gerçekten başladığı gün. Planı değiştirmez. |
| Gerçekleşen bitiş | İşin gerçekten bittiği gün. |
| % | 0–100. İş bağlıysa veya alt kalem varsa kilitlenir. Özet, çocuklardan toplar. Görev işe bağlıysa yüzde iş kaydından gelir. |
| Ağırlık | Özet ilerlemeye bu kalemin payı. Kardeşlere göre oranlanır. Varsayılan 1’dir. 1.1’e 2, 1.2’ye 1 yazarsanız özet yüzde, kontrol listesine iki kat bakar. Bu sette 1 kalsın. |
| İş | İsteğe bağlı. **İş bağla** bu workspace’teki bir OC kaydını seçer. Özet kalemde iş üst kayıttır. Bağlanınca yüzde burada kilitlenir. **Bağı çöz** bağı kaldırır, OC işini silmez. |

Workspace yoksa iş listesi boştur. “Bu workspace’te bağlanacak iş bulunamadı” yazması bu sette normaldir. İş seçmeyin.

Pencerenin altındaki cümleler şunları ayırır: plan tarihleri Gantt ve sapma içindir. Gerçekleşen tarihler planı değiştirmez. İş bağlı değilse ilerleme elle yazılır.

## Örnekte ne görürsünüz?

| Kod | Ad | Tür | Tarih |
|---|---|---|---|
| 1 | Saha hazırlığı | Özet | alt kalemlerden |
| 1.1 | Kontrol listesi | Görev | 6 Ekim – 17 Ekim 2026 |
| 1.2 | Saha kabul | Kilometre taşı | 24 Ekim 2026 |

1.1 satırında iş bağlı değildir. Bu kasıtlıdır. Durum sayfasında **İş bağsız** uyarısı bundan çıkar. **Kanıt yok** da bundan çıkar: kanıt, bağlı işe yapışır. İş yoksa sayfa klasörde dursa da kanıt sayılmaz.

## Adımlar

1. **WBS kalemi** deyin. Tür **Özet**, ad `Saha hazırlığı`. Üst kalem boş kalsın. Kaydedin. Kod **1** olur.
2. Yeniden **WBS kalemi**. Tür **Görev**, ad `Kontrol listesi`, üst kalem **1 Saha hazırlığı**. Plan başlangıç 6 Ekim, plan bitiş 17 Ekim. Ağırlık 1. Kaydedin. Kod **1.1** olur.
3. Yeniden **WBS kalemi**. Tür **Kilometre taşı**, ad `Saha kabul`, üst kalem **1**. Plan bitiş 24 Ekim. Kaydedin. Kod **1.2** olur.
4. Süzgeci **İş bağsız** yapın. 1.1 orada durur. 1 özettir; yaprak iş ona bağlanmaz.
5. **Gantt** sekmesine dönün. Çubuk ve elmas görünür.

## Ne değildir?

- Klasör ağacı değildir. Belgeler **Kütüphane**’dedir.
- Özet kaleme yaprak iş bağlanmaz.
- Kalemi silmek, kütüphanedeki belgeyi silmez.
- Kod elle yazılmaz. Üst kalemi değiştirirseniz kod yeniden hesaplanabilir.
- FS oku burada çizilmez. Ok, [Bağımlılıklar](./10-bagimlilik.md) sayfasındadır.

Sıradaki: [Kütüphane](./06-kutuphane.md)
