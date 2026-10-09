# Proje açmak

**Yeni proje**, boş bir plan dosyası açar. İçinde henüz iş, belge veya karar yoktur. Kod ve ad yazılmadan **Kaydet** kapalıdır.

Menü: **Kaynaklar → Projeler**.

## Liste

Listenin üstünde **Yenile** ve **Yeni proje** vardır. Altında üç süzgeç ve bir arama kutusu durur.

| Parça | Ne işe yarar |
|---|---|
| Tümü | Bütün projeler. |
| Aktif | Durumu aktif olanlar. Taslak ve kapalı burada görünmez. |
| Dikkat | Üzerinde uyarı bayrağı olanlar. Bayrak, Durum sayfasındaki uyarıların özetidir. |
| Proje ara | Kod, ad veya durumda arar. |
| Yenile | Listeyi sunucudan tekrar okur. |

Tablonun kolonları:

| Kolon | Ne işe yarar |
|---|---|
| Kod | Kısa kimlik. Örnekte siz yazarsınız. Listede aranır. |
| Ad | Projenin görünen adı. Satıra tıklayınca proje açılır. |
| Durum | Taslak, aktif veya kapalı. |
| % | İlerleme yüzdesi. Kırılım yokken boş veya sıfır görünür. |
| Plan başlangıç | Planın ilk günü. |
| Plan bitiş | Planın son günü. |
| Baseline | Plan dondurulduysa o tarihin damgası. Bu sette boş kalır. |
| Dikkat | Varsa uyarı özeti. Yeni projede boştur. |
| İşlemler | Satırı siler. |

Silmek projeyi, iş kırılımını ve bağımlılıklarını siler. Geri alınmaz. Kütüphanedeki belgeler bu düğmenin cümlesinde sayılmaz; silmeden önce neyin gideceğini okuyun.

## Yeni proje penceresi

| Alan | Ne işe yarar |
|---|---|
| Kod | Kısa ad. Boşsa kayıt olmaz. Atölye kabulünde `ATOLYE` yazın. Sonra değiştirilmez diye düşünmeyin; liste bunu arar. |
| Ad | `Atölye kabulü`. Boşsa kayıt olmaz. |
| Açıklama | Bir iki cümle. `Atölyedeki yeni tezgâhın saha kabulü.` |
| Durum | **Taslak** henüz üzerinde anlaşılmadı demektir. **Aktif** plan yürürlükte demektir. **Kapalı** iş bitti demektir. Bu pencerede taslak bırakın; aktif yapmayı Genel sayfasında göreceksiniz. |
| Plan başlangıç | `2026-10-06`. Gantt ve sapma bu güne bakar. |
| Plan bitiş | `2026-10-24`. |
| İş paketi | Hazır iskelet. **Boş proje (iskelet yok)** seçili kalsın. Paket seçerseniz kayıt, kırılım ve klasör basar. Bu sette ağacı elle kuracağız. Paketin ne bastığı [Paketler](./12-paketler.md) sayfasındadır. |

Seçili paketin altında kısa bir açıklama cümlesi çıkar. Boş projede çıkmaz.

**Vazgeç** pencereyi kapatır. **Kaydet** projeyi açar. Kod ve ad doluysa düğme açıktır.

## Örnekte ne görürsünüz?

Kayıttan sonra listede bir satır: kod `ATOLYE`, ad **Atölye kabulü**, tarihler 6 Ekim 2026 – 24 Ekim 2026. Durum önce taslaktır. Satıra tıklayınca solda **Plan**, **Teslimat**, **Kontrol**, **Taraflar** grupları durur.

Çalışma alanı (workspace) bu pencerede sorulmaz. İş bağlamak için Operasyon Merkezi’nde bir alan gerekir. Onu Genel sayfasında görürsünüz. Bu sette bağlamıyoruz. Durum sayfası bunun nasıl göründüğünü gösterir.

## Adımlar

1. **Kaynaklar → Projeler** açın.
2. **Yeni proje** deyin.
3. Kod: `ATOLYE`. Ad: `Atölye kabulü`. Açıklama: `Atölyedeki yeni tezgâhın saha kabulü.`
4. Plan başlangıç: `2026-10-06`. Plan bitiş: `2026-10-24`. İş paketi boş kalsın.
5. **Kaydet**. Liste satırına tıklayın.

## Ne değildir?

- Proje açmak iş kırılımı kurmaz.
- Proje açmak belge üretmez.
- Paket seçmek zorunda değilsiniz. Paket, kırılımı hazır basan kestirmedir. Önce ağacın ne olduğunu görün.

Sıradaki: [Genel](./03-genel.md)
