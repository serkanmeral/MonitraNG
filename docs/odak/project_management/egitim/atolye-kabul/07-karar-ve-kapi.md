# Karar ve kapı

İkisi de **Teslimat** grubundadır. Karar “şunu kabul ettik” der. Kapı “bu nokta geçilmeden iş kapanmasın” der.

RAID “olabilir veya oldu” der. Karar “kabul ettik” der. Kapı “geçilmeden kapatma” der. Üçü aynı teslimatta yan yana durabilir. RAID sayfası [RAID](./13-raid.md) içindedir.

## Kararlar

Soldaki menüde **Teslimat → Kararlar**.

Üstteki cümle: metni yazıp kaydedince **Kararlar** klasörüne resmi sayfa oluşur. Bu bir RAID kaydı değildir.

Liste boşsa “Henüz karar yok” yazar. **Karar ekle** pencereyi açar.

| Alan | Ne işe yarar |
|---|---|
| Ad | Kararın başlığı. Örnek: `Kabul kriteri onaylandı`. |
| Tür | **Genel** sıradan karardır. **Kapsam değişikliği** “işin sınırı değişti” der. Açık kapsam değişikliği Durum’da ayrı satır olur. Bu sette tür **Genel** kalsın. |
| Durum | **Açık**: henüz kapanmadı. **Kabul**: karar yürürlükte. **Geçersiz**: yerine yenisi geldi. Bu sette kabul edebilirsiniz. Metin yine sayfa olarak durur. |
| Karar metni | Resmi cümle. **Metin yaz** seçiliyse bu metin Kararlar klasörüne sayfa olur. Kaynak tektir: o sayfa. |
| Var olan belge | Metin yazmak yerine kütüphanedeki bir sayfayı resmi kayıt diye seçersiniz. Kararlar veya Wiki klasöründe belge yoksa “henüz belge yok” der. Bu sette metin yazın. Var olan belge seçmeyin. |
| Etki | İsteğe bağlı. Etkilenen belgeyi kütüphaneden **Etkilenen** diye bağlarsınız. Resmi kayıt ile etkilenen aynı şey değildir. |
| Etkilenen WBS | Kapsam değişikliğinde hangi kalemin etkilendiği. Genel kararda boş kalabilir. |
| Etkilenen işler | OC işleri. Bu sette iş olmadığı için boş kalır. |

**Vazgeç** kapatır. **Kaydet** hem karar satırını hem, metin yazdıysanız, kütüphane sayfasını yazar.

Karar satırını silmek “Bu karar kaydı silinecek. Bağlı belgeler silinmez” der. Sayfa **Kararlar** klasöründe kalır.

## Kapılar

Soldaki menüde **Teslimat → Kapılar**.

Üstteki cümle: açık veya reddedilen kapı, bağlı WBS alt ağacında ve ondan sonra gelen işlerde kapatmayı kilitler. **Geçti** veya **Feragat** kilidi kaldırır. Bütün kriterler işaretlenmeden geçilemez. Red ve feragat not ister.

Liste boşsa “Henüz aşama kapısı yok” yazar. **Kapı ekle** pencereyi açar.

| Alan | Ne işe yarar |
|---|---|
| Ad | Kapının adı. Örnek: `Saha kabul`. |
| Kilometre taşı | Hangi WBS anına yapıştığı. **1.2 Saha kabul** seçin. **(WBS bağlama)** kapıyı bir kaleme yapıştırmaz. Bu sette yapıştırın. |
| Durum | **Açık**: kilit durur. **Geçti**: kriterler tamam, kilit kalktı. **Red**: geçilmedi, not zorunlu, kilit durur. **Feragat**: bilerek geçildi sayılmadı, not zorunlu, kilit kalkar. |
| Kriterler | Geçmek için işaretlenecek maddeler. **Kriter ekle** satır açar. Öneri çipleri de vardır: Kapsam net, Plan onaylı, Açık kapsam değişikliği yok, Kanıt belgesi var. Çip, metni kutuya yazar. Siz yine de kaydedersiniz. |
| Not | Red ve feragatte zorunlu. Açık ve geçti için isteğe bağlıdır. |

Kriterler eksikken **Geçti** seçip kaydetmeye kalkarsanız “Tüm kriterler işaretlenmeden kapıdan geçilemez” der.

Kapıyı silmek WBS kalemini ve belgeyi silmez.

Kapı, hakedişteki kabul adımını da tutabilir. O bağ bu sette yoktur. Hakediş sayfası onu ayrı anlatır. Burada kapı, iş kapatmayı kilitler.

## Örnekte ne görürsünüz?

**Kararlar** listesinde bir satır: **Kabul kriteri onaylandı**. Metin: `Kontrol listesindeki maddeler eksiksizse tezgâh kabul edilir.` Kayıt, kütüphanede **Kararlar** klasörüne bir sayfa yazar.

**Kapılar** listesinde bir satır: **Saha kabul**. Kilometre taşı **1.2**. Durum **Açık**. İki kriter vardır: `Kontrol listesi tamam` ve `Karar kayıtlı`. İkisi de işaretsizdir.

Açık kapı, 1.2 altındaki ve ondan sonra gelen işlerin kapanmasını kilitler. Bu sette bağlı iş olmadığı için kilit bir işin üzerinde görünmez. Kapı yine de açıktır. Durum sayfası **Açık kapı** der.

## Adımlar

1. **Kararlar → Karar ekle**.
2. Başlık: `Kabul kriteri onaylandı`. Tür: **Genel**. Metin yaz: `Kontrol listesindeki maddeler eksiksizse tezgâh kabul edilir.` Var olan belge seçmeyin. Kaydedin.
3. Kütüphane’de **Kararlar** klasörünün oluştuğunu doğrulayın.
4. **Kapılar → Kapı ekle**. Ad: `Saha kabul`. Kilometre taşı: **1.2 Saha kabul**.
5. İki kriter ekleyin: `Kontrol listesi tamam`, `Karar kayıtlı`. Kaydedin. Durum açık kalsın.
6. Kriterleri işaretlemeyin ve **Geçti** demeyin. Bu sette kapıyı açık bırakıyoruz ki Durum ekranında görünsün.

## Ne değildir?

- Karar, RAID kaydı değildir. Risk ve sorun **RAID** sekmesindedir.
- Kapı, Gantt’taki elmasın kendisi değildir. Elmas 1.2 tarihidir. Kapı, o tarihte geçilmesi gereken kontroldür.
- Kapıyı silmek WBS kalemini ve belgeyi silmez.
- Karar sayfası kütüphanede durur. Karar satırını silmek o sayfayı kendiliğinden silmez.
- Feragat, “kapı geçti” demek değildir. Kilidi kaldırır ve not ister. Hakedişte feragat, dönemin kabulünü açmaz. O kural hakediş sayfasındadır.

Sıradaki: [Durum](./08-durum.md)
