# Durum

**Durum**, planın neresinin eksik veya geç kaldığını listeler. Dashboard’daki grafiklerin satır satır halidir. Grafik buraya gelir. Burası grafiğin kopyası değildir.

Soldaki menüde **Plan → Durum**.

Üstteki cümle zinciri söyler: belge, WBS, iş, kanıt. Gecikme, sapma, eksik onay ve bağsız yapraklar bu listede durur.

## Ekranda ne vardır?

| Parça | Ne işe yarar |
|---|---|
| Uyarılar | Yalnız bayrağı olan satırlar. Bu sette buradan başlayın. |
| Tümü | Bayrağı olsun olmasın kalemler. |
| Belgeler | Belge tarafındaki eksikler: onaysız belge, plan yok, kanıt yok. |
| Hesaplama | Listenin hangi anda üretildiği. Elle yazılmaz. Sayfayı açınca hesaplanır. |
| Boş cümle | “Bu filtrede kalem yok.” O süzgeçte iş yok demektir. Hata değildir. |

Her satır bir uyarı türüdür. Satır, işi kapatmaz. Eksği kapatmak ilgili sekmededir.

| Uyarı | Ne demek | Nerede kapanır |
|---|---|---|
| Geciken | Plan bitiş bugünden önce ve iş bitmemiş. | WBS tarihi veya gerçekleşen bitiş. |
| Kilometre taşı riski | Yakın elmas tehlikede. | WBS ve kapı. |
| Sapma | Plan günü, dondurulan kopyadan kaymış. | Planı eski güne alın veya [Baseline](./22-baseline.md) ile yeni kopya alın. Baseline yoksa çıkmaz. |
| İş bağsız | Yaprak kalemin OC işi yok. | WBS → İş bağla. |
| Açık iş | Bağlı iş hâlâ açık. | Operasyon Merkezi. |
| Kanıt yok | Bağlı işin kanıt belgesi yok. İş yoksa sayfa kanıt sayılmaz. | Kütüphane → Bağla → Kanıt. |
| Plan yok | Bağlı işin plan belgesi yok. | Kütüphane → Bağla → Plan. |
| Onaysız belge | Belge onay döngüsünde değil. | Belgenin kendi durumu. |
| Açık kapsam değişikliği | Türü kapsam değişikliği olan karar açık. | Kararlar. |
| Açık kapı | Kapı geçilmedi. | Kapılar. |
| Reddedilen kapı | Kapı red durumunda. | Kapılar. |
| Yüksek risk | Açık risk, etki yüksek veya skor 6 ve üzeri. | RAID. |
| Açık sorun | RAID türü sorun ve açık. | RAID. |
| Aşırı yük | Bir kaynağın bir haftası 40 saati aşmış. | Kaynak. |
| Bütçe aşımı | Gerçekleşen, planı geçmiş. | Bütçe. |
| Bekleyen okundu | Okudum damgası yok. | Okundu. |
| Geciken okundu | Son tarih geçmiş, damga yok. | Okundu. |
| Açık yükümlülük | Madde henüz karşılanmadı. | Yükümlülük. |
| Geciken yükümlülük | Son tarih geçmiş. | Yükümlülük. |
| İşsiz yükümlülük | Maddeye OC işi bağlanmamış. | Yükümlülük. |
| Açık denetim paketi | Paket teslim edilmemiş. | Denetim. |
| Eksik denetim paketi | Pakette kanıt belgesi yok. | Denetim. |
| Geciken denetim paketi | Son tarih geçmiş. | Denetim. |
| Açık toplantı aksiyonu | Aksiyon bitmemiş. | Toplantı. |
| Geciken toplantı aksiyonu | Aksiyonun son tarihi geçmiş. | Toplantı. |
| İşsiz toplantı aksiyonu | Aksiyon bir işe bağlanmamış. | Toplantı. |
| Açık paydaş | Paylaşım kaydı davetli veya etkin. | Paydaş. |
| Paylaşımsız paydaş | Belge seçilmemiş. | Paydaş. |
| Süresi biten paydaş | Erişim sonu geçmiş. | Paydaş. |
| Taslak süreç haritası | Süreç resmi yapılmamış. | Süreç. |
| Belgesiz süreç haritası | Çizim belgesi bağlanmamış. | Süreç. |

Bu uyarılar kapatma kilidi değildir. Kilidi kapı tutar. Diğerleri bakmanız için satırdır.

Bu sette RAID, bütçe, okundu, toplantı ve süreç kaydı yoktur. O satırların çıkmaması normaldir. Çıkmaları için ilgili sayfadaki örneği kurmanız gerekir.

## Örnekte ne görürsünüz?

Atölye kabulü bu sete kadar bilinçli eksik bırakıldı. **Uyarılar** süzgecinde en az şunlar durur:

| Uyarı | Neden |
|---|---|
| Açık kapı | Saha kabul kapısı geçilmedi. |
| İş bağsız | 1.1 Kontrol listesi bir iş kaydına bağlı değil. |
| Kanıt yok | Bağlı iş olmadığı için kontrol listesi sayfası kanıt sayılamaz. |

Tarih bugünden sonraysa **Geciken** çıkmaz. 24 Ekim geçtikten sonra 1.2 hâlâ açıksa gecikme de belirir.

**Dashboard** sekmesinde aynı tablonun özeti kart ve grafik olarak durur. Açık kapı ve WBS dağılımı orada da görünür. Ayrıntı için yine Durum’a gelin. Dashboard’un her kartı [Dashboard](./11-dashboard.md) sayfasındadır.

## Adımlar

1. **Durum** sekmesini açın. **Uyarılar** seçili olsun.
2. **Açık kapı** satırının Saha kabul kapısı olduğunu doğrulayın.
3. **İş bağsız** satırının 1.1 olduğunu doğrulayın.
4. Eksikleri kapatmak bu sayfadan olmaz. Kapı **Kapılar** sekmesinde geçer. İş bağı **WBS** kaleminden, iş kaydı seçilerek kurulur. Kanıt, o işe **Kütüphane → Bağla → Kanıt** ile bağlanır.
5. Bu yürüyüşte kapıyı geçmeyin ve iş bağlamayın. Uyarıların durması, ekranın doğru çalıştığını gösterir.

## Ne değildir?

- Durum, işi kapatmaz.
- Dashboard’un aynısı değildir. Dashboard bakış, Durum listedir.
- Bir uyarı satırı, o işi yapan ekran değildir. Satır sizi ilgili sekmeye yollar.

Atölye yürüyüşü burada biter. Para, defter ve taraflar sonraki sayfalardadır.

Sıradaki: [Hakediş](./09-hakedis.md)
