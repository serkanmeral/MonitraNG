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

**Kur / Eksikleri tamamla / Güncelle** sırasında kalıcı bir ilerleme penceresi açılır. Backend WBS ve OC’yi tek istekte basar; listedeki adımlar kütüphane klasörleri ve başlangıç sayfalarıdır. Yüzde çubuğu yoktur. Pencere iş bitmeden kapanmaz.

## Kurulum ne üretir?

- WBS kalemleri
- Kütüphane altında paket klasörleri ve varsa başlangıç sayfaları
- Workspace yoksa ince OC iskeleti: kural, SLA, pano
- Özet üst iş ve yaprak iş kayıtları

App Store gibi rastgele uygulama yüklemez.

Paket JSON’unda `diagram` (ör. `Onboarding akışı.drawio`) tanımlı olsa bile kurulum bugün **draw.io dosyası basmaz**. Diyagram klasörü oluşur, içi boş kalır. Çizim **Kütüphane → Yeni çizim** veya Süreç sekmesinden yapılır.

Uzun proje kodlarında OC iş anahtarı kısaltılır (ör. `LAB-ONBOARDING` → `LABONBOARDIN-0001`). Bu kurulum hatası değildir.

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
