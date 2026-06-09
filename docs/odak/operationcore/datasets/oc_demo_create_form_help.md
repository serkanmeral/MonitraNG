# OC Demo — Yeni iş kaydı

Bu form **OC Demo Board** üzerinde yeni bir iş öğesi (work item) açmak içindir. Kayıt oluşturulduktan sonra board listesinde görünür; durum geçişleri profil ekranından veya (Kanban board'larda) sürükle-bırak ile yapılabilir.

## Zorunlu alanlar

| Alan | Ne yazmalıyım? |
|------|----------------|
| **Başlık** | Kısa, tek cümlelik özet. Board'da ve bildirimlerde görünen ana metindir. |
| **Tip** | Varsayılan **OC Demo Task** gelir; genelde değiştirmeniz gerekmez. |

## Önerilen alanlar

| Alan | Açıklama |
|------|----------|
| **Açıklama** | Detay, adımlar, bağlam. **İşi «Kapat» (resolve) ile bitirmeden önce doldurulması gerekir** — workspace kuralı boş açıklamada geçişe izin vermez. |
| **Atanan** | İşin sahibi / ilk sorumlu kişi. Boş bırakılırsa atanmamış kalır; «Bana atanan» widget'larında görünmez. |
| **Öncelik** | Aciliyet; SLA ve liste renklendirmesi buna göre değerlendirilir. |
| **Board** | Varsayılan **OC Demo Board** seçili gelir; kayıt hangi board'da listeleneceğini belirler. |

## Ek alanlar (workspace'te tanımlıysa)

Demo ortamında aşağıdaki pool alanları formda görünebilir:

- **Tedarikçi** — Lookup ile seçilir; demo tedarikçi kataloğundan kayıt seçin.
- **Ülke / Şehir** — Önce **Ülke** seçin; **Şehir** listesi buna göre filtrelenir. Ülke değişince şehir sıfırlanır.

Bu alanlar zorunlu değildir; doldurmanız senaryoya bağlıdır.

## Kayıt sonrası akış

1. **Kaydet** — İş anahtarı (`OCD-…`) otomatik üretilir.
2. Board'da **Açık** sütununda listelenir.
3. Profilden **Başlat** → **Devam** durumuna geçer.
4. Açıklama doluysa **Kapat** → **Tamam** ile kapanır.

## İpuçları

- Başlığı «Ne?» sorusuna cevap verecek şekilde yazın; açıklamada «Nasıl / neden?» detayını verin.
- Atama yapmadan önce board'da filtre ve pano widget'larını test edecekseniz kendinize atayabilirsiniz.
- Sorun yaşarsanız workspace yöneticisine **OC Demo Workspace** bağlamını iletin.
