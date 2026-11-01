# Stok, depo, demirbaş ve zimmet

Odak testte kurulmuş işin planı. Katalog paketi değildir. AnkaraBT yol haritasındaki OC-04 bu gövdeyi henüz taşımaz.

**Tarih:** 1 Kasım 2026  
**Kaynak:** Odak test (`192.168.20.20`), son kapanış 11 Temmuz 2026  
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
| `zimmet_urunler` | Marka, model, garanti. Gruba bağlı |
| `zimmet_depolar` | Depo |
| `zimmet_depo_lokasyonlari` | Raf veya bölme. Depoya bağlı |
| `zimmet_demirbaslar` | Fiziksel parça. Numara `DMB-00001`. Seri no tekildir |
| `tedarikciler` | Mevcut liste |

Demirbaşın durumu depoda veya zimmetlidir. Kayıtta depo, lokasyon ve kişi durur.

Menü **Dinamik Formlar → Zimmet Yönetimi** altındadır: demirbaşlar, ürün kataloğu, ürün grupları, depolar, depo lokasyonları. İki çalışma alanı Operasyon Merkezi ağacında görünür.

## Kapanınca ne olur

Kurallar çalışma alanının geçişine yazılır. Ayrı bir zimmet servisi yoktur.

| Geçiş | Sonuç |
|---|---|
| Depo girişi kapanır | Katalog satırındaki miktar kadar demirbaş oluşur |
| Teslim | Seçilen demirbaşlar zimmetli olur, kişi yazılır |
| İade | Seçilen demirbaşlar depoya döner, kişi alanı temizlenir |

Hata geçişi geri alır. Stok yarım kalmaz.

## Rapor ve belge

Altı rapor vardır: depolar, ürünler, ürün grupları, demirbaş envanteri, garanti, personel özeti. Üç belge vardır: personel dökümü, teslim tutanağı, iade tutanağı. Tutanak rapor satırından açılır. Geçiş bitince kendiliğinden belge oluşmaz.

## Biten ve duran

Uçtan uca çekirdek Odak testte kapandı: tanım, stok girişi, demirbaş, verme, iade, rapor, belge. Üretim ortamına kurulmadı.

Duruyor:

- Satınalma çalışma alanı ve depo girişine bağlanması
- Hurda ve kayıp
- Geçiş bitince tutanağın kendiliğinden yazılması
- Demirbaş satırında zimmet geçmişi
- Ürün görseli

Kurulum betikleri `docs/odak/zimmet/scripts/` altındadır. Kayıt kimlikleri Odak teste aittir. Başka bir domaine kopyalanınca yeniden üretilir.
