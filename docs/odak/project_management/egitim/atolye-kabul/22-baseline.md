# Baseline

**Baseline**, plan tarihlerinin dondurulmuş kopyasıdır. “İlk anlaştığımız günler bunlardı” diye saklanan nottur. İş bitirmez. Tarihi kendisi değiştirmez. Ayrı bir sekme de değildir.

Düğme **Plan → Genel** kartındadır. Adı **Baseline al**.

Atölye kabulü yürüyüşünde bu düğmeye basmayın. 03 Genel sayfası kartta **Baseline yok** yazsın diye boş bırakır. Bu sayfa, sapmanın nasıl çıktığını kendi denemenizde göstermek içindir. Denemeyi bitirince planı eski güne alıp düğmeye bir kez daha basarsanız sapma kapanır.

## Ne kopyalanır?

Düğme, o anda duran her iş kırılımı kaleminin **plan başlangıç** ve **plan bitiş** gününü kopyalar. Kopya kalemin üzerinde durur. Plan kutuları yerinde kalır. Sonradan planı değiştirirsiniz. Kopya, basıldığı gündeki haliyle durur. Ta ki düğmeye yeniden basana kadar.

Kopyalanmayanlar:

| Alan | Baseline’a girer mi? |
|---|---|
| Plan başlangıç, plan bitiş | Evet. Günün kendisi kopyalanır. Saat farkı sayılmaz. |
| Gerçekleşen başlangıç, gerçekleşen bitiş | Hayır. İşin gerçekten başladığı gün sapma değildir. |
| Yüzde, ağırlık, ad, tür | Hayır. |
| Kapı, karar, bütçe, hakediş | Hayır. Baseline yalnız plan tarihidir. |

Proje kartına üç damga yazılır:

| Damga | Ne işe yarar |
|---|---|
| Baseline tarihi | Düğmeye basıldığı an. Kartta görünür. |
| Basan kişi | O anki kullanıcı adı. Tarihin yanında parantez içinde durur. |
| Not | Pencerede yazdığınız cümle. Kayıtla birlikte saklanır. Kart bu cümleyi tekrar göstermez. Tarih ve kişi görünür. Notu sonra okuyacağınız bir yer yoktur. Cümleyi, neden bastığınızı hatırlamak için yazın. |

Bu sürümde tek kopya vardır. İkinci basış, eski kopyanın üstüne yazar. Eski tarihi, eski notu ve eski sapmayı saklayan bir arşiv açılmaz.

Kırılım boşken de düğme çalışır. Damga yazılır. Kopyalanacak gün olmadığı için hiçbir kalemde tarih donmaz. Önce WBS kurun, sonra basın.

## Pencere

**Baseline al** bu pencereyi açar.

Cümle şudur: tüm WBS plan tarihleri mevcut baseline olarak kopyalanır. Faz 1'de tek baseline vardır.

| Parça | Ne işe yarar |
|---|---|
| Not | İsteğe bağlı. Örnek: `Kabul planı 6 Ekim’de donduruldu.` Boş bırakılırsa notsuz damga düşer. |
| Vazgeç | Pencereyi kapatır. Kopya alınmaz. |
| Baseline al | Kopyayı yazar ve pencereyi kapatır. |

## Nerede görünür?

| Yer | Baseline yokken | Kopya dururken, plan aynı günse | Plan günü kopyadan kayınca |
|---|---|---|---|
| Genel kartı | **Baseline yok** | **Baseline tarihi** ve basan kişi | Damga durur. Sapma burada yazılmaz. |
| Proje üst bandı | Çip yok | Çip yok | **Sapma** çipi |
| Proje listesi, Baseline kolonu | Baseline yok | Basıldığı gün | **Sapma** çipi |
| WBS tablosu, Baseline kolonu | Çizgi (—) | **Baseline'da** | **Sapma** çipi |
| WBS süzgeci **Sapma** | Boş | Boş | Yalnız kayan kalemler |
| Durum, Uyarılar | Sapma satırı yok | Sapma satırı yok | **Sapma** |
| Dashboard sağlığı | Sapma yüzünden değişmez | Aynı | **İzle**. Gecikme, red kapı veya bütçe aşımı varsa zaten **Müdahale** olabilir. Sapma tek başına müdahale yapmaz. |

Gantt’ta ayrı bir baseline çubuğu yoktur. Çubuk her zaman güncel plan tarihidir. Kopyayı Gantt’ta göremezsiniz. Kaymayı WBS kolonu ve Durum söyler.

## Sapma ne zaman çıkar?

Karşılaştırma gün gün yapılır. Aynı takvim günü sapma değildir. Saat farklı olsa da aynı gün sayılır.

Bir kalemde kopya hiç yoksa (iki tarih de boş) o kalem sapmaz. Baseline’dan sonra eklenen yeni kalem böyledir. Kolonda çizgi durur. **Baseline'da** da yazmaz, **Sapma** da yazmaz. Onu da dondurmak için düğmeye yeniden basarsınız.

Kopyada gün varsa ve plan başlangıcı veya plan bitişi o günden farklıysa kalem sapmıştır. Biri bile farklıysa yeter. İkisini de eski güne alırsanız sapma kapanır. Düğmeye yeniden basmak da kapanır. Çünkü yeni kopya, kaymış planın kendisi olur. “Eski plan buydu” bilgisi gider.

Örnek. 1.1 Kontrol listesinin planı 6 Ekim – 17 Ekim. Baseline alındı.

| Yaptığınız | 1.1 kolonu |
|---|---|
| Hiçbir plan gününü değiştirmediniz | Baseline'da |
| Plan bitişi 20 Ekim yaptınız | Sapma |
| Plan bitişini yine 17 Ekim yaptınız | Baseline'da |
| Gerçekleşen bitişe 18 Ekim yazdınız, plan 17 Ekim’de kaldı | Baseline'da |
| 1.3 diye yeni görev eklediniz, baseline’a bir daha basmadınız | Çizgi. Sapma değil. |

1 sapmış, 1.2 sapmamış olabilir. Üst banttaki **Sapma** çipi, bir kalem bile sapmışsa yanar. Liste kolonu da proje için tektir: bir sapma bütün projeyi sapmış gösterir.

## Örnek

Atölye kabulünde kırılım kurulu olsun. 1.1, 6–17 Ekim. 1.2, 24 Ekim. Bu sayfayı okurken yürüyüşü bozmak istemiyorsanız örneği uygulayıp sonunda geri alın.

1. **Plan → Genel**. **Baseline yok** yazdığını görün.
2. **Baseline al**. Not: `Kabul planı donduruldu.` Düğmeye basın.
3. Kartta **Baseline tarihi** ve sizin kullanıcı adınız görünür. Notun metni kartta tekrar çıkmaz.
4. **WBS** açın. 1, 1.1 ve 1.2 kolonunda **Baseline'da** yazsın. Süzgeç **Sapma** boş olsun.
5. 1.1’i düzenleyin. Plan bitişi 17 Ekim yerine 20 Ekim olsun. Kaydedin.
6. 1.1 kolonunda **Sapma** çipi durur. 1 ve 1.2 **Baseline'da** kalır. Üst bantta **Sapma** yanar.
7. **Durum → Uyarılar**. **Sapma** satırı 1.1’i gösterir. Kapıyı ve iş bağını bu satır kapatmaz.
8. **Dashboard**. Sağlık, sapma yüzünden en az **İzle** olur. Atölye kabulünde açık kapı zaten izlemedir. Sapma onu müdahaleye çevirmez.
9. Gantt’a bakın. 1.1 çubuğu 20 Ekim’e uzar. Eski 17 Ekim çubuğu çizilmez.
10. Geri almak için 1.1 plan bitişini 17 Ekim yapın. Çip **Baseline'da** olur. Üst banttaki sapma da kalkar. Damga durur. Tarihi silmenin yolu yoktur. Yeni bir basış damgayı yeniler.

İkinci basışı da görün. 1.1’i yine 20 Ekim yapın. Sapma çıksın. **Baseline al** deyin, nota `20 Ekim’e güncellendi` yazın. Sapma kapanır. 20 Ekim artık dondurulan gündür. 17 Ekim’e dönmek yeni bir sapma olur. Eski not kartta görünmez. Yeni damganın tarihi, ikinci basışın anıdır.

## Adımlar

1. Kırılımı kurmadan basmayın.
2. Plan günleri üzerinde anlaşıldıysa **Genel → Baseline al**.
3. Kısa bir not yazın. Kartta görünmeyeceğini bilin.
4. Tarihi kaydıracaksanız önce WBS’te kaydırın, sonra **Durum**da **Sapma** satırını okuyun.
5. Kayma kabul edildiyse düğmeye yeniden basın. Eski kopya gider.
6. Atölye kabulü listesini sade tutmak istiyorsanız deneme bitince planı ilk güne alın. Sapma kalksın. Damga kalabilir. Yürüyüş sayfası damgayı boş bekler. Damgayı silmek için ekranda düğme yoktur.

## Ne değildir?

- Baseline almak projeyi bitirmez ve kapıyı geçirmez.
- Gerçekleşen tarihin kopyası değildir. Sapma yalnız plan gününe bakar.
- Gantt’ta ikinci bir çubuk değildir.
- Sürüm listesi değildir. İkinci basış birincinin üstüne yazar.
- Not, sonradan okunan bir sayfa değildir. Yazılır ve kartta tarih ile kişi kalır.
- Yeni kalem kendiliğinden sapmaz. Kopyası yoktur. Dondurmak için yeniden basılır.

Yürüyüş [Genel](./03-genel.md) sayfasından [Gantt](./04-gantt.md) ile devam eder. Sapma satırının listesi [Durum](./08-durum.md) sayfasındadır.
