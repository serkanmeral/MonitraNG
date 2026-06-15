# Odak Sipariş — faz planı (yapılacaklar / yapılmayacaklar)

**Durum:** v1.1 · 15 Haziran 2026  
**Amaç:** Eski **Kalite** uygulamasının hangi hizmetlerinin MonitraNG’ye taşınacağını faz faz netleştirmek.  
**İlke:** Önce müşterinin **günlük kullandığı sipariş ekranları**; arkada Operation Core süreç motoru.

Fonksiyon listesi: [FONKSIYONEL_HARITA.md](./FONKSIYONEL_HARITA.md)

---

## Özet tablo

| Faz | Odak | MonitraNG yüzeyi | Eski Kalite karşılığı |
|-----|------|-------------------|------------------------|
| **Faz 0** | Analiz ve referans | Lokal / sunucu inceleme | Tüm modüller (okuma) |
| **Faz 1** | Sipariş MVP | Odak Sipariş hub + OC workspace | İş paketi + kalemler |
| **Faz 2** | Sevkiyat + genişletme | Hub sekmeleri + dataset | Sevkiyat listesi, sevk miktarı |
| **Faz 3** | Migrasyon ve canlı geçiş | Prod veri + eğitim | Açık paketler, master veri |
| **Faz 4+** | Diğer operasyon modülleri | Ayrı planlar | Stok, satın alma, muhasebe |
| **Yapılmayacak** | — | Bilinçli dışarıda | KYS ağırlıklı modüller (tek seferde) |

---

## Faz 0 — Analiz ve referans ✅ (devam ediyor)

**Amaç:** Eski uygulamayı anlamak; ekran ve alan referansı.

### Yapılacaklar

- [x] Kaynak kod ve veritabanı keşfi (`192.168.20.30`)
- [x] Lokal referans ortamı (`localhost:8080` — native stack)
- [x] Fonksiyonel harita
- [x] UX uyumluluk haritası (taslak)
- [x] Mimari karar (hibrit model)
- [ ] Ekran ekran walkthrough (İş Paketleri, detay, kalemler)
- [ ] Müşteri ile terminoloji onayı

### Yapılmayacaklar

- MonitraNG’de yeni ekran geliştirme
- Toplu veri migrasyonu
- Eski uygulamayı değiştirme

---

## Faz 1 — Sipariş MVP (ilk canlı değer)

**Amaç:** Müşterinin **en çok kullandığı** akış: iş paketi listesi, detay, kalemler. Tanıdık menü ve terimler.

### Yapılacaklar

#### İş paketi (üst kayıt)

- [x] DG dataset **`odak_is_paketleri`** — deploy edildi (Odak test)
- [x] Toplu migrasyon **824 paket** (SQL dump → DG, MO yok)
- [ ] **İş Paketleri listesi** — Hub UI (kod hazır, deploy bekliyor)
- [ ] **Arama paneli** — paket no, isim, müşteri, PO no, proje no, ürün tanımı
- [ ] **İş paketi oluşturma / düzenleme** — müşteri, termin, sorumlular, durum
- [ ] **İş paketi detay** — üst özet paneli (eski `view.ctp` mantığı)
- [ ] OC **work item** olarak süreç motoru (durum: planlandı → üretim → kalite → sevk) — **sonraki faz**
- [x] Master veri: **müşteri** (`odak_musteriler`) — 87 legacy migrate

#### Sipariş kalemleri

- [x] Dataset: **`odak_siparis_kalemleri`** — deploy edildi (Odak test)
- [x] AF form — `odak-siparis-kalemleri-form`
- [x] Toplu migrasyon: **2759 / 2767 kalem** (~%99,7) · `parentPackageId`
- [x] POC migrasyon (MO): 3 paket — referans; birincil model artık DG-only
- [ ] Detay sekmesi: **Kalemler** tablosu (hub UI — deploy bekliyor)
- [ ] Alanlar: PO no, kalem no, proje no, tanım, miktar, birim, sevk tarihi, sevk adresi

#### Kalite (mevcut altyapı)

- [ ] Mevcut **NCR / CAPA** workspace bağlantısı (`parentItemId` → iş paketi)
- [ ] Menüden veya profilden **Kalite kuyruğuna** link

#### UX / organizasyon

- [ ] Yan menü: **Odak Sipariş → İş Paketleri**
- [ ] Kullanıcıya **“workspace” jargonu gösterilmez** — “İş Paketi” dili
- [ ] POC migrasyon: **1–3 örnek paket** + kalemleri — ✅ 1 paket (2018-004)

### Yapılmayacaklar (Faz 1)

- Sevkiyat modülü (Faz 2)
- PO PDF görüntüleme / dosya arşivi (Faz 1b veya Faz 2)
- **Döküman paketi** — tek tık paket seçimi, dosyaların işe otomatik linklenmesi ([DOKUMAN_PAKETI_NOTU.md](./DOKUMAN_PAKETI_NOTU.md) · Faz 1b+ taslak)
- Excel dışa aktarma
- Fiyat / maliyet alanları (ERP rolü — sonra)
- FAI otomatik kuralları
- Faturalama, alım emirleri, stok
- Cihaz, eğitim, doküman yönetimi
- Eski uygulamanın pixel-perfect kopyası
- Tam geçmiş migrasyon (825 paket) — **824/825 tamamlandı** (1 tuple bozuk); kalemler ~%99,7

---

## Faz 1b — Sipariş tamamlayıcı (MVP sonrası küçük paket)

**Amaç:** Faz 1’de ertelenen ama sipariş ekranında beklenen ekler.

### Yapılacaklar

- [ ] **Müşteri sipariş PDF** görüntüleme (Document Intelligence / dosya storage)
- [ ] **Excel export** — iş paketi listesi (eski `export`)
- [ ] **Fiyat alanları** — birim/toplam (rol bazlı görünürlük)
- [ ] Gelişmiş arama / DataTables satır genişletme (opsiyonel)

### Yapılmayacaklar

- Sevkiyat operasyonu (Faz 2)
- Muhasebe entegrasyonu

---

## Faz 2 — Sevkiyat ve operasyon derinliği

**Amaç:** İş paketi kalemlerinden **gerçek sevkiyat** takibi; plan vs gerçekleşen.

### Yapılacaklar

- [ ] **Sevkiyat listesi** hub (Planlanan / Tümü)
- [ ] Dataset: **`odak_sevkiyatlar`** + **`odak_sevkiyat_kalemleri`**
- [ ] İş paketi detay: **Sevkiyatlar** sekmesi
- [ ] Kalem bazında **sevk miktarı** vs planlanan miktar
- [ ] OC durum senkronu (ör. “Sevk edildi” geçişi)
- [ ] Sevkiyat arama (irsaliye, tarih, müşteri, paket no)
- [ ] Ürün master genişletme (`odak_urunler` ← eski `products`)

### Yapılmayacaklar

- Tam QCF / MCF form üretimi (ayrı kalite projesi)
- Faturalama otomasyonu
- Stok hareket defteri

---

## Faz 3 — Migrasyon ve canlı geçiş

**Amaç:** Eski veriyi MonitraNG’ye taşımak; kullanıcıları yeni sisteme almak.

### Yapılacaklar

- [x] Migrasyon scriptleri (`packages` → `odak_is_paketleri`, `packageitems` → `odak_siparis_kalemleri`)
- [x] Müşteri / firma eşlemesi (`legacyFirmId`)
- [x] **Full migrasyon (DG-only):** 824 paket · 2759 kalem · Odak test
- [ ] Mongo / DG doğrulama UAT (kullanıcı walkthrough)
- [ ] Kalan 8 kalem manuel inceleme
- [ ] PO PDF dosya migrasyonu
- [ ] NCR/CAPA geçmiş linkleri (kademeli)
- [ ] Kullanıcı eğitimi — “aynı iş, yeni sürüm” dili
- [ ] Eski sistem **salt okunur** paralel dönem (opsiyonel)

### Yapılmayacaklar

- Eski Kalite uygulamasını MonitraNG içinde host etmek (fork/wrap)
- Tüm modüllerin tek seferde taşınması

---

## Faz 4+ — Diğer operasyon modülleri (ayrı planlar)

Her biri **Odak Sipariş projesi dışında** veya **sonraki workspace** olarak ele alınır. Aynı platform deseni (hub + dataset + isteğe bağlı OC süreci) uygulanabilir.

| Modül | Eski Kalite | Not |
|-------|-------------|-----|
| **Stok / envanter** | Malzemeler, stok | Dataset + hareket; WI yalnız onay/talep |
| **Satın alma** | Alım emirleri | Tedarik süreci |
| **Muhasebe** | Kesilen / alım faturaları | ERP entegrasyonu tercih edilebilir |
| **Zimmet / demirbaş** | (kısmen personel/cihaz) | Ayrı ihtiyaç analizi |

Detay: Faz 4+ için ayrı `docs/odak/...` klasörü açılır; bu belge yalnızca **başlık** seviyesinde.

---

## Yapılmayacaklar (bilinçli kapsam dışı)

Aşağıdakiler **Odak Sipariş / MonitraNG ilk dalgasında taşınmayacak** (ayrı KYS veya uzun vadeli program):

| Başlık | Eski Kalite modülü | Gerekçe (kısa) |
|--------|-------------------|----------------|
| **Tam doküman yönetimi (KYS)** | Dokümanlar, iptal listesi | Ayrı KYS kapsamı; siparişten bağımsız |
| **Eğitim yönetimi** | Eğitim listesi, istatistik | İK/KYS modülü |
| **Etkinlik / görev takvimi** | Etkinlikler, görevler | Genel productivity; sipariş MVP değil |
| **Cihaz kalibrasyon modülü** | Cihazlar, kalibrasyon, bakım, arıza | T37 / cihaz takibi ayrı proje |
| **Denetim modülü** | Denetimler | KYS |
| **GKK / MCF / FAI formları (tam)** | Giriş KK, ölçüm formları, FAI listesi | Yüksek form karmaşıklığı; NCR/CAPA OC’de var |
| **Uygunluk belgeleri (CoC)** | CoC | Kalite belge üretimi — sonra |
| **Müşteri şikayetleri (ayrı modül)** | Şikayetler | CAPA ile kısmen örtüşür; sonra |
| **KYT istatistik paketi** | Uygunsuzluk istatistikleri, performans | Raporlama fazı |
| **ERP / muhasebe çift kayıt** | Faturalar | Entegrasyon veya ERP’de kalır |
| **Eski uygulama birebir klonu** | Tüm UI | Bakım maliyeti; mental model yeterli |

**Not:** “Yapılmayacak” = **hiçbir zaman değil**; **bu sipariş programının Faz 1–3 kapsamında değil**.

---

## MonitraNG bileşen eşlemesi (faz bazlı)

| Bileşen | Faz 1 | Faz 2 | Faz 3 |
|---------|-------|-------|-------|
| OC workspace (WI + akış) | ✅ | ✅ | ✅ |
| Odak Sipariş hub UI | ✅ | genişler | stabil |
| `odak_siparis_kalemleri` | ✅ | ✅ | ✅ ~%99,7 |
| `odak_is_paketleri` | ✅ | ✅ | ✅ 824/825 |
| `odak_sevkiyat_*` | — | ✅ | migrate |
| NCR / CAPA (mevcut seed) | link | link | migrate link |
| Dashboard / widget | opsiyonel | ✅ | ✅ |
| Workspace otomasyon (SW-A*) | opsiyonel | ✅ | ✅ |

---

## Karar bekleyen noktalar

| # | Konu | Seçenekler |
|---|------|------------|
| 1 | Workspace adı | Odak Üretim genişlet vs **Odak Sipariş** yeni workspace |
| 2 | Faz 1 sevkiyat sekmesi | Boş placeholder vs Faz 2’ye bırak |
| 3 | Migrasyon kapsamı | Tüm geçmiş vs açık + son N yıl |
| 4 | Eski sistem paralel süre | Salt okunur kaç ay? |

---

## İlgili dokümanlar

- [FONKSIYONEL_HARITA.md](./FONKSIYONEL_HARITA.md)
- [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md)
- [MIMARI_KARAR.md](./MIMARI_KARAR.md)
- [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md)
- [DEVAM.md](./DEVAM.md)
