# Genel

**Genel**, projenin kimlik kartıdır. Ad, açıklama, durum, çalışma alanı ve plan tarihleri buradadır. İş listesi burada değildir.

Soldaki menüde **Plan → Genel**.

Üst bantta da projenin adı ve tarih aralığı durur. Kartı kaydedince bant da değişir.

## Alanlar

| Alan | Ne işe yarar |
|---|---|
| Ad | Projenin görünen adı. Boşsa **Kaydet** kapalıdır. Örnek: `Atölye kabulü`. |
| Açıklama | Bir iki cümle. Liste aramasına girmez; kartta durur. |
| Durum | **Taslak**: henüz üzerinde anlaşılmadı. **Aktif**: plan yürürlükte. **Kapalı**: iş bitti diye işaretlendi. Kapatmak kırılımı silmez. |
| Workspace | Operasyon Merkezi’ndeki çalışma alanı. İş bağlamak yalnız bu alandaki işlere olur. **(workspace yok)** bu sette normaldir. Altındaki cümle şunu söyler: paket kurulumu, alan yoksa ince bir iskelet kurabilir. Bu sette paket kurmuyoruz. |
| Plan başlangıç | Planın ilk günü. Örnek: 6 Ekim 2026. |
| Plan bitiş | Planın son günü. Örnek: 24 Ekim 2026. Gantt bu aralığa bakar. |
| Baseline tarihi | Plan dondurulduysa damga ve damgayı basan kişi. Basılmadıysa **Baseline yok** yazar. |

İki düğme vardır:

| Düğme | Ne işe yarar |
|---|---|
| Kaydet | Karttaki alanları yazar. Baseline almaz. |
| Baseline al | Tüm WBS plan tarihlerinin bir kopyasını “plan buydu” diye saklar. Bu sürümde tek baseline vardır. Yeni bir tane açmaz; eskisinin üstüne yazar. |

Baseline penceresi şunu söyler: tüm WBS plan tarihleri mevcut baseline olarak kopyalanır. Kırılım henüz yokken basarsanız dondurulacak tarih de yoktur. Bu sette **Baseline al** düğmesine basmayın. Ekranda “Baseline yok” yazması normaldir.

Baseline alındıktan sonra bir kalemin plan tarihi değişirse Durum’da **Sapma** çıkar. Sapma, “şimdiki plan, dondurulan plandan kaydı” demektir. Gerçekleşen tarih sapma değildir. Gerçekleşen, işin gerçekten başladığı ve bittiği gündür. Onu WBS kaleminde yazarsınız. Pencerenin her alanı, sapmanın nerede göründüğü ve ikinci basışın eskisini silmesi [Baseline](./22-baseline.md) sayfasındadır.

## Örnekte ne görürsünüz?

| Alan | Değer |
|---|---|
| Ad | Atölye kabulü |
| Açıklama | Atölyedeki yeni tezgâhın saha kabulü. |
| Durum | Aktif |
| Workspace | (workspace yok) |
| Plan başlangıç | 6 Ekim 2026 |
| Plan bitiş | 24 Ekim 2026 |
| Baseline | Yok |

## Adımlar

1. **Genel** sekmesini açın.
2. Durumu **Aktif** yapın.
3. Workspace’i **(workspace yok)** bırakın.
4. Tarihlerin 6 Ekim ve 24 Ekim olduğunu doğrulayın.
5. **Kaydet**.
6. **Baseline al** düğmesine basmayın.

## Ne değildir?

- İş listesi değildir. Kırılım **WBS** sekmesindedir.
- Takvim değildir. Çubuklar **Gantt** sekmesindedir.
- Baseline almak projeyi bitirmez. Yalnızca “plan buydu” diye not düşer.
- Workspace seçmek iş kaydı açmaz. İş kaydı Operasyon Merkezi’ndedir. Bağ, WBS kaleminden kurulur.

Sıradaki: [Gantt](./04-gantt.md)
