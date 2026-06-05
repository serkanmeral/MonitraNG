# Operasyon Merkezi — Kullanıcı Rehberi

Operasyon Merkezi; iş kayıtlarını (work item) oluşturduğunuz, takip ettiğiniz ve sonuçlandırdığınız çalışma alanıdır. Help desk talepleri, geri bildirim kayıtları, görevler veya operasyonel istekler bu modül üzerinden tek bir akışta yönetilir.

Bu rehber **son kullanıcıya** yöneliktir: bir işi nasıl açarsınız, listede/panoda nasıl takip edersiniz, profilinde neler yaparsınız ve nasıl filtreleyip ararsınız.

> **İpucu:** Gördüğünüz alanlar ve sütunlar çalışma alanınıza (workspace) göre değişebilir; formlar ve panolar yöneticiniz tarafından özelleştirilir. Adımlar her yapılandırmada aynıdır.

**Örnek çalışma alanları (Odak prod):**

| Çalışma alanı | Anahtar öneki | Örnek |
| --- | --- | --- |
| MonitraNG Geri Bildirim | `MNG` | `MNG-0001` |
| IT Destek | `HD` (yapılandırmaya göre) | `HD-0042` |

---

## 1. Başlangıç ve gezinme

| Adım | Nasıl |
| --- | --- |
| Modüle giriş | Sol menüden **Operasyon Merkezi** |
| Açılış ekranı | **Çalışma alanı** — sol tarafta **Gezinme** ağacı (çalışma alanları → panolar) |
| Bir panoyu açma | Ağaçtan bir pano seçin → **Board'u aç** |
| Bildirimler | Üst başlıktaki bildirim simgesi |

Çalışma alanı ekranında:

- **Tümünü aç / Tümünü kapat** ile ağacı genişletip daraltabilirsiniz.
- Her çalışma alanının bir **anahtar öneki** (ör. `MNG`, `HD`) ve bağlı **panoları** vardır.
- Pano kartlarında görünüm tipini gösteren bir etiket bulunur: **Kanban** veya **Liste**.
- Pano seçildiğinde orta alanda **Özet** görünümü açılır. Panoya bir **özet pano (dashboard)** bağlandıysa **Özet | Pano** anahtarıyla pano widget'larını da görebilirsiniz.

---

## 2. İş kaydı oluşturma

İş kaydı oluşturmanın birincil yolu **pano** üzerindendir.

1. Bir panoyu açın: `Operasyon Merkezi → (pano seç) → Board'u aç`.
2. Üst çubuktan **Yeni iş** düğmesine tıklayın.
   - Bu düğme yalnızca **düzenleme yetkiniz** varsa görünür.
3. Açılan pencerede form alanlarını doldurun. Form, çalışma alanının tanımına göre dinamik olarak yüklenir.
4. **Oluştur**'a tıklayın. Kayıt eklenir ve liste/pano otomatik yenilenir.

### Sık karşılaşılan alanlar

| Alan | Açıklama |
| --- | --- |
| **Başlık** | İşin kısa adı (zorunlu) |
| **Açıklama** | Ayrıntılı metin |
| **İş tipi** | Olay, hizmet talebi, geri bildirim vb. |
| **Öncelik** | Düşük → kritik |
| **Atanan** | İşi üstlenen kişi (ad/kullanıcı adı/e-posta ile aranır) |
| **İzleyenler** | Bilgilendirilecek kişiler |
| **Etiketler** | Sınıflandırma etiketleri (çalışma alanı kataloğundan) |
| **Bitiş tarihi** | Hedeflenen tamamlanma tarihi (tanımlıysa) |

> **Zorunlu alanlar** yıldız (`*`) ile işaretlenir. Eksik bırakırsanız **Eksik zorunlu alanlar** uyarısı çıkar ve ilgili alanın altında **Bu alan zorunludur** yazar.

---

## 3. Liste görünümü

Liste, çok sayıda işi bir tablo üzerinde takip etmenin en hızlı yoludur. Sunucu taraflı sayfalama, sıralama ve filtreleme sunar.

### Sık kullanılan sütunlar

| Sütun | İçerik |
| --- | --- |
| **Anahtar** | İş numarası (ör. `MNG-12`) |
| **Başlık** | İşin adı |
| **Durum** | Renkli durum etiketi |
| **Atanan** | Sorumlu kişi |
| **Öncelik** | Renkli öncelik etiketi |
| **Tip** | İş tipi |
| **Oluşturan / Oluşturma** | Kaydı açan kişi ve tarih |
| **Geçen süre** | Açılışından bu yana geçen süre |
| **SLA durumu** | Hedeflere göre durum (SLA tanımlı çalışma alanlarında) |

**Kullanım:**

- Sıralanabilir sütun başlığına tıklayarak sıralayın.
- Alt çubuktan sayfa boyutunu seçin: **10 / 25 / 50 / 100**.
- Her satırın sonunda **İşlemler** sütunu vardır:
  - **Profili gör** (göz simgesi)
  - **Düzenle** (kalem — düzenleme yetkisi gerektirir; pano üzerinden hızlı düzenleme)
  - **Sil** (çöp kutusu — onay ister)
- Üst çubukta **Yenile** ve **Yeni iş** düğmeleri bulunur.

---

## 4. Kanban (pano) görünümü

Panonuz Kanban olarak yapılandırıldıysa, üstteki **Liste | Kanban** anahtarıyla görünümü değiştirebilirsiniz.

- **Sütunlar** iş durumlarını temsil eder; her sütun başlığında o durumdaki iş sayısı görünür.
- **Kart** üzerinde işin **anahtarı**, **başlığı** ve atanan kişinin **baş harfleri** yer alır. Karta tıklayınca **profil** açılır.
- Bir kartı başka bir sütuna **sürükleyip bırakarak** durumunu değiştirebilirsiniz (düzenleme yetkisi gerekir):
  - Geçiş tanımlıysa durum güncellenir ve **Durum güncellendi** bildirimi çıkar.
  - Geçiş **zorunlu alan** istiyorsa, sistem sizi **profilden uygulamaya** yönlendirir veya geçiş penceresinde alanları doldurmanızı ister.

> **Not:** Bazı sütunlara doğrudan geçiş tanımlı olmayabilir; bu durumda kart geri döner ve bir uyarı gösterilir.

---

## 5. İş kaydı profili

Bir işin tüm detayını profil sayfasında görürsünüz. Üst kısımda işin **anahtarı** ve **başlığı**, durum geçişi düğmeleri ve sekmeler bulunur.

### Profilde düzenleme

Düzenleme yetkiniz varsa profil üst çubuğundaki **Düzenle** düğmesiyle **Detaylar** sekmesinde alanları doğrudan güncelleyebilirsiniz:

1. **Düzenle**'ye tıklayın (üst çubuk).
2. **Detaylar** sekmesinde form alanlarını değiştirin.
3. **Kaydet** ile onaylayın veya **İptal** ile vazgeçin.

Yetkiniz yoksa profil **salt okunur** görünür (**Salt okunur** rozeti).

### Durum geçişi uygulama

1. Üstteki **geçiş** düğmesine tıklayın (etiket, geçiş ya da hedef durum adıdır).
2. Açılan **Durum geçişi uygula** penceresinde isterseniz **Yorum (opsiyonel)** ekleyin.
3. Geçiş **zorunlu alan** istiyorsa (ör. kapanışta **çözüm özeti**) bu alanlar pencerede gösterilir — doldurup **Uygula**'ya tıklayın.

### Sekmeler

| Sekme | İçerik |
| --- | --- |
| **Detaylar** | İşin alanları (görüntüleme; düzenleme yetkisiyle yerinde düzenleme) |
| **Yorumlar** | Zengin metin editörü ile yorum, `@` ile kişi etiketleme, dosya eki, yanıt |
| **Aktivite** | Durum/geçiş/sistem olaylarının zaman çizelgesi |
| **Ekler** | İşe eklenen dosyalar (önizleme ve indirme) |

### Sağ panel (özet)

- **SLA** — yanıt ve çözüm hedefleri (tanımlıysa).
- **Detaylar** — durum, öncelik, tip, atanan, oluşturan, tarihler.
- **İzleyenler** — bildirilen kişiler.
- **Bağlı kayıtlar** — ilişkili diğer işler.
- **Politika / kurallar** — bu kayda uygulanan SLA ve kural özeti (salt okunur).

---

## 6. Yorumlar ve kişi etiketleme

Yorumlar sekmesi ekip içi iletişim içindir.

- Yorum kutusuna yazın; **birini etiketlemek için `@`** yazıp kişiyi seçin.
- Biçimlendirme araçları: **Kalın**, **İtalik**, **Üstü çizili**, **Madde listesi**, **Numaralı liste**, **Emoji**.
- **Dosya ekle** ile yoruma dosya iliştirebilirsiniz.
- Göndermek için **Gönder** düğmesine ya da <kbd>Ctrl</kbd> + <kbd>Enter</kbd> tuşlarına basın.
- Bir yoruma **Yanıtla** ile tek seviye yanıt verebilirsiniz.
- Kendi yorumunuzu **Düzenle** / **Sil** ile yönetebilirsiniz (silme işlemi geri alınamaz).

---

## 7. Filtreleme ve arama

### Hızlı arama

Üstteki arama kutusuna yazdığınızda (kısa bir gecikmeyle) sonuçlar sunucudan filtrelenir. **Aramayı temizle** ile sıfırlayabilirsiniz.

### Filtre paneli

**Filtreler** bölümünden, panonuzda filtrelenebilir olarak işaretlenen alanlara göre süzme yapabilirsiniz:

| Alan türü | Davranış |
| --- | --- |
| Durum / Öncelik / Tip | Çoklu seçim |
| Atanan / Oluşturan | Kişi seçici |
| Etiket | Çoklu etiket |
| Metin alanları | **İçerir** araması |

- Aktif filtre sayısı bir rozet ile gösterilir; **Filtreleri temizle** ile tümünü kaldırabilirsiniz.

### Gelişmiş arama

**Gelişmiş arama** ile koşul satırları ekleyerek hassas süzme yapabilirsiniz. Tüm koşullar **VE** ile birleştirilir.

- Her koşul: **Alan**, **Operatör**, **Değer**.
- Operatörler: **Eşittir**, **Eşit değildir**, **Şunlardan biri**, **Hiçbiri**, **İçerir**, **İle başlar**, **İle biter**, **Büyüktür**, **Büyük eşittir**, **Küçüktür**, **Küçük eşittir**.

---

## 8. Bildirimler

`Operasyon Merkezi → Bildirimler` ekranında size yönelik tüm bildirimleri görürsünüz: **etiketlenme**, **atama** ve **kural/olay** bildirimleri.

- **Tümü** / **Yalnızca okunmamış** filtreleri.
- **Tümünü okundu işaretle** veya tek tek **Okundu işaretle**.
- Bildirimden **İş kaydını aç** ile doğrudan ilgili işe gidebilirsiniz.

---

## 9. Hızlı ipuçları

- İşe en hızlı erişim: **anahtar** (ör. `MNG-12`) üzerinden listede arama yapın.
- Panoda sürükle-bırak çalışmıyorsa, hedef durum için geçiş tanımlı olmayabilir; **profilden** durum geçişi deneyin.
- **Tamamla** veya **Kapat** gibi geçişlerde ek alan (çözüm özeti vb.) isteniyorsa geçiş penceresinde doldurun.
- Bir alanı göremiyorsanız, çalışma alanınızın formunda tanımlı olmayabilir — yöneticinize danışın.
- Yorumda yanlış kişiyi etiketlediyseniz yorumu **düzenleyebilirsiniz**.

---

*Bu doküman Operasyon Merkezi son kullanıcı deneyimini (Haziran 2026) temel alır. Çalışma alanınıza özel alan ve pano yapılandırmaları için yöneticinizle iletişime geçin.*
