# Stok, depo, demirbaş ve zimmet

Odak testte kurulmuş işin planı. Katalog paketi değildir. Abt çekirdeği ve sonraki dilim [yol haritası OC-04](development_roadmap.md) içindedir.

**Tarih:** 4 Kasım 2026  
**Kaynak:** Odak test (`192.168.20.20`) çekirdeği 11 Temmuz 2026. Abt günlük kullanım dilimi 4 Kasım 2026’da duraklatıldı.  
**Ayrıntı:** [docs/odak/zimmet/PLAN.md](../odak/zimmet/PLAN.md), [DEVAM.md](../odak/zimmet/DEVAM.md)

## Ne olduğu

Fabrika personeline laptop, monitör, çanta gibi malzeme verilir. Stok girişi, demirbaş ve zimmet aynı hikâyedir. Dört katman vardır.

| Katman | Yer | Ne tutar |
|---|---|---|
| Tanımlar | Dinamik form | Ürün grubu, katalog, depo, lokasyon |
| Stok girişi | Operasyon Merkezi | Malın depoya girmesi |
| Demirbaş | Dinamik form | Eldeki fiziksel parça |
| Zimmet | Operasyon Merkezi | Parçanın kişiye verilmesi ve iadesi |

Katalogdaki ürün ile eldeki parça ayrıdır. *Dell Latitude 5520* bir katalog kaydıdır. Seri numarası `ABC123` olan laptop bir demirbaşdır. Zimmet, demirbaş kaydına yapılır.

Laptop ve monitör seri numarasıyla tek tek izlenir. Çanta ve aksesuar miktarla da izlenebilir. Tedarikçi listesi yeniden kullanılır. Yeni bir tedarikçi veri seti açılmaz.

## Çalışma alanları

İki çalışma alanı vardır. Satınalma üçüncüsü olarak konuşuldu ve kurulmadı.

**Zimmet Depo** (`GIR`). Tek tip: depo girişi. Akış taslak, mal kabul, stoklandı, kapalıdır. Kayıtta katalog ürünü, miktar, depo, lokasyon, tedarikçi, fatura no, giriş tarihi, kaynak ve seri listesi durur. Kaynak `manuel` veya `satinalma` olur. İkincisi satınalma alanı için boş bırakıldı.

**Personel Zimmet** (`ZIM`). İki tip vardır.

| Tip | Akış |
|---|---|
| Zimmet verme | Talep, onay bekliyor, teslim edildi, aktif, kapalı |
| Zimmet iade | İade açık, iade tamam, kapalı |

Bir personele aynı anda birden fazla demirbaş verilebilir. İade de birden fazla parçayı birlikte alır.

## Veri

| Kayıt | Ne |
|---|---|
| `zimmet_urun_gruplari` | Laptop, monitör, çanta, sarf. Seri ile mi, demirbaş mı |
| `zimmet_markalar` | Marka. Katalog ve demirbaş buraya bağlanır |
| `zimmet_urunler` | Model, garanti. Gruba ve markaya bağlı |
| `zimmet_depolar` | Depo |
| `zimmet_depo_lokasyonlari` | Raf veya bölme. Depoya bağlı |
| `zimmet_demirbaslar` | Fiziksel parça. Numara `DMB-00001`. Seri no tekildir |
| `zimmet_demirbas_hareketleri` | Yaşam kaydı. Teslim, iade, hurda ve kayıp cümleleri |
| `tedarikciler` | Mevcut liste |

Demirbaş depoda, zimmetli, bakımda, hurda veya kayıp olabilir. Günlük geçiş depo ile zimmet arasındadır. Kayıtta depo, lokasyon ve kişi durur. Hurda ve kayıp henüz bir geçiş değildir.

Menü **Dinamik Formlar → Envanter Yönetimi** altındadır: demirbaşlar, ürün kataloğu, ürün grupları, ürün markaları, depolar, depo lokasyonları. İki çalışma alanı Operasyon Merkezi ağacında görünür.

## Kapanınca ne olur

Kurallar çalışma alanının geçişine yazılır. Ayrı bir zimmet servisi yoktur.

| Geçiş | Sonuç |
|---|---|
| Depo girişi kapanır | Katalog satırındaki miktar kadar demirbaş oluşur |
| Teslim | Seçilen demirbaşlar zimmetli olur, kişi yazılır |
| İade | Seçilen demirbaşlar depoya döner, kişi alanı temizlenir |

Hata geçişi geri alır. Stok yarım kalmaz.

## Rapor ve belge

Altı rapor vardır: depolar, ürünler, ürün grupları, demirbaş envanteri, garanti, personel özeti. Üç belge vardır: personel dökümü, teslim tutanağı, iade tutanağı. Tutanak rapor satırından da açılır. Abt’de teslim ve iade geçişi tutanağı kendisi yazar; düğme yalnız zimmetli satırda görünür. Odak test sunucusu bu davranışı henüz taşımaz.

## Biten ve duran

Uçtan uca çekirdek Odak testte kapandı: tanım, stok girişi, demirbaş, verme, iade, rapor, belge. Aynı çekirdek abt alanına 2 Kasım 2026’da indi. Günlük kullanım dilimi 3 Kasım’da AbtApp’e alındı ve 4 Kasım 2026’da duraklatıldı. Ayrıntı yol haritası OC-04’tedir.

Abt’de kapananlar: depo ve rafın teslimde temizlenmesi, iadede geri yazılması, kilitli demirbaş formu, marka ve ürün grubu süzgeci, yaşam kaydı, geçişin tutanak yazması, katalog belge kartı.

Duruyor:

- Teslim ve iadenin belirli gruplara bağlanması
- Üretici seri numarası ve garantinin katalog ayından hesaplanması
- Hurda ve kaybın bir geçişle işlenmesi. Sözlük ve liste süzgeci durur
- Satınalma çalışma alanı ve depo girişine bağlanması
- Ürün görseli

Kurulum betikleri `docs/odak/zimmet/scripts/` altındadır. Kayıt kimlikleri Odak teste aittir. Başka bir domaine kopyalanınca yeniden üretilir.
