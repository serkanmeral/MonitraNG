# Paketler

**Paketler**, bu proje için hazır bir WBS + belge klasörü + (gerekirse) ince OC iskeleti kurar. Uygulama mağazası değildir. İmzasız üçüncü taraf paket kurulmaz; raftakiler birinci taraf ve doğrulanmıştır.

## Ne zaman kullanılır?

Boş bir projeyi elle WBS’lemek yerine “PMO / kabul / kalite” gibi bilinen bir iskeleti basmak istediğinizde. SEED-PMO zaten basılmış bir örnektir.

## Rafta ne vardır?

Aynı motor, farklı içerik. Hepsi v1.1.0 (TEST, 17 Eylül 2026):

| Paket | Ne iskeleti |
|---|---|
| PMO | Plan, tutanak, karar, durum, teslimat |
| Kalite | Prosedür, form, kayıt, revizyon |
| Kabul | Saha / teslim kabul kırılımı |
| Teklif | Teklif hazırlığı |
| Mimari | Mimari teslimat ve diyagram klasörü |
| ECO / ECN | Mühendislik değişiklik emri (ECO) ve bildirimi (ECN) |
| Onboarding | Karşılama, yetkinlik, göreve hazır |

Aynı projeye ikinci paket **eklenir** (WBS numarası devam eder). Yanlış paketi **Sök** ile geri alırsınız; dolu klasör ve paylaşılan isimler kalır.

## Ekranda ne vardır?

Kurulmadı / Kurulu / Sürüm geride listesi.

- **Önizle** — bu projedeki mevcut WBS’e göre oluştur / atla / güncelle
- **Kur** — ilk kurulum
- **Eksikleri tamamla** / **Güncelle**
- **Sök** — önizlemeli geri alma

Önizleme yalan söylemez: zaten duran kalem **atlanır**, eksik olan **oluşturulur**.

**Kur / Eksikleri tamamla / Güncelle** sırasında kalıcı bir ilerleme penceresi açılır. Backend WBS ve OC’yi tek istekte basar; listedeki adımlar kütüphane klasörleri, başlangıç sayfaları ve varsa diyagram dosyasıdır. Yüzde çubuğu yoktur. Pencere iş bitmeden kapanmaz.

Yeni proje oluştururken paket seçilirse aynı liste açılır. **Sök** onayından sonra da aynı pencere WBS/iş ve klasör adımlarını gösterir.

## Kurulum ne üretir?

- WBS kalemleri
- Kütüphane altında paket klasörleri, başlangıç sayfaları ve varsa boş draw.io (ör. `Onboarding akışı.drawio`)
- Workspace yoksa ince OC iskeleti: kural, SLA, pano
- Özet üst iş ve yaprak iş kayıtları

App Store gibi rastgele uygulama yüklemez.

Diyagram **boş tuval**dır; paket örnek bir akış çizmez. Düzenlemek **Kütüphane** veya Süreç sekmesinden (Diyagramı düzenle) yapılır. Dosya zaten varsa tekrar kur **atlar**.

Yeni projede OC iş anahtarı proje kodunu izler (`LAB-ONBOARDING-0001`). Eski lab workspace’lerinde kesik önek kalmış olabilir (`LABONBOARDIN-0001`); bu o workspace’in kurulduğu andaki kuraldır.

Wiki, Kararlar, Yüklemeler ve Toplantı notları paket raftan gelmez; proje kütüphanesinin varsayılan klasörleridir. Sök bunları paket malı sanıp silmez.

## Sökme neyi siler, neyi bırakır?

- Workspace, kural, SLA, pano **silinmez**
- Kullanılmamış (açık, ilerlemesiz) iş kayıtları silinebilir
- İlerleme varsa WBS ve iş kalır
- Boş WBS kalemleri ve boş DI klasörleri silinir
- Dolu klasör ve paylaşılan isimler kalır (ör. Diyagram)

## Örnek

Yeni proje “Saha kabulü” açtınız, workspace boş.

1. Paketler → Kabul paketini önizleyin  
2. “Workspace oluşturulacak” satırını okuyun  
3. Kurun; ilerleme listesinin bitmesini bekleyin  
4. WBS’te kabul kırılımı, Kütüphane’de klasörler, OC’de işler oluşur  
5. Yapraklara kanıt bağlamak hâlâ sizin işinizdir; paket sihirli kapatmaz

Yanlış paketi kurduysanız sökme önizlemesine bakın. Dolu Kararlar klasörü silinmez.

## Ne değildir?

- Kütüphane’nin yerine geçmez
- Otomatik kanıt bağlama yapmaz
- Mevcut WBS’i sessizce ezmez (önizlemede atla)
- Diyagram editörü veya resmi süreç kaydı değildir

## Sonraki adım

Kurulumdan sonra **WBS** ve **Kütüphane**. İşi yürütmek için **Durum**.
