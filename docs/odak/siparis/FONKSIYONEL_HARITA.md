# Eski Kalite uygulaması — fonksiyonel harita

**Durum:** v1.0 · 13 Haziran 2026  
**Hedef kitle:** İş birimi, proje paydaşları (teknik olmayan özet)  
**Kaynak:** Lokal referans ortamı + sunucu analizi (`192.168.20.30` / `localhost:8080`)

---

## Uygulama ne yapıyor?

**Kalite**, Odak’ın günlük operasyonlarının büyük bölümünü **tek programda** topluyor:

> Müşteri siparişinden üretime, kalite kontrolden sevkiyata ve (kısmen) faturalamaya kadar süreci kayıt altına alır; kalite belgeleri, cihazlar ve eğitimler gibi destek işlerini de aynı çatı altında tutar.

Resmî adı “Kalite” olsa da program **yalnızca kalite modülü değil**; asıl günlük kullanım **iş paketi (sipariş) takibi** etrafında dönüyor.

---

## Ana hizmet alanları

### 1. Sipariş ve planlama *(işin kalbi)*

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **İş paketleri** | Müşteri siparişi bir proje gibi açılır: numara, müşteri, termin, sorumlular. |
| **Sipariş kalemleri** | Müşteri PO satırları tek tek: PO no, kalem no, ürün/hizmet, miktar, planlanan sevk. |
| **Müşteri sipariş belgesi (PDF)** | Müşterinin gönderdiği sipariş dosyası iş paketine bağlanır. |
| **Ürün tanımları** | Üretilen parçaların listesi ve revizyon bilgileri. |

**Kim kullanır:** Planlama, proje sorumluları, yönetim.

**Menü yolu (eski):** Planlama → İş Paketleri · Ürünler

---

### 2. Sevkiyat

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Sevkiyat listesi** | Hangi iş paketinden ne kadar, ne zaman, nereye gidecek — kayıt ve arama. |
| **Sevkiyat kalite formları** | Sevk öncesi/sonrası kalite kontrol kayıtları. |
| **Performans / gecikme takibi** | Planlanan vs gerçekleşen sevkiyat (yönetim raporları). |

**Kim kullanır:** Depo, sevkiyat, kalite.

**Menü yolu (eski):** Sevkiyatlar → Sevkiyat Listesi · Kalite Kontrol Formları

---

### 3. Kalite yönetimi

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Uygunsuzluk (NCR)** | Hatalı/şüpheli ürün veya süreç kaydı; etkilenen miktar, durum. |
| **Düzeltici faaliyet (CAPA)** | Kök neden analizi ve düzeltme planı. |
| **Müşteri şikayetleri** | Dışarıdan gelen şikayetlerin takibi. |
| **FAI** | İlk parça / ilk üretim onayı. |
| **Giriş kalite kontrol (GKK)** | Tedarikten gelen malzemenin kabul kontrolü. |
| **Ölçüm kontrol formları (MCF)** | Ölçüm ve muayene sonuçlarının form halinde saklanması. |
| **Uygunluk belgeleri (CoC)** | “Ürün şartlara uygundur” belgeleri. |
| **Denetimler** | İç/dış denetim kayıtları. |
| **Raporlar / istatistikler** | Kalite performansı, uygunsuzluk trendleri, süreç süreleri. |

**Kim kullanır:** Kalite ekibi, üretim (bildirim), yönetim.

**Menü yolu (eski):** Kalite → (Uygunsuzluklar, CAPA, FAI, GKK, MCF, …)

---

### 4. Stok ve satın alma *(malzeme tarafı)*

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Malzeme kartları** | Hammadde/sarf listesi, stok bilgisi. |
| **Alım emirleri** | Tedarikçiden sipariş — **müşteri siparişi değil**, iç satın alma. |

**Kim kullanır:** Satın alma, depo, planlama.

**Menü yolu (eski):** DBA → Malzemeler · Alım Emirleri

---

### 5. Muhasebe *(fatura tarafı)*

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Kesilen faturalar** | Müşteriye kesilen faturaların takibi. |
| **Alım faturaları** | Tedarikçi faturaları; geciken ödemeler listesi. |

**Kim kullanır:** Muhasebe, ERP sorumlusu, yönetim.

**Menü yolu (eski):** Muhasebe → Kesilen Faturalar · Alım Faturaları

---

### 6. Tanımlar ve ortak veriler

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Firma listesi** | Müşteri ve tedarikçi kartları, iletişim. |
| **Tolerans türleri** | Kalite/ölçüm formlarında kullanılan standart tanımlar. |

**Kim kullanır:** Tanımları kalite/planlama günceller; herkes listelerden seçer.

**Menü yolu (eski):** Tanımlamalar → Firma Listesi · Tolerans Türleri

---

### 7. Cihazlar ve kalibrasyon

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Cihaz listesi** | Ölçüm aletleri, makineler. |
| **Kalibrasyon / bakım takibi** | Sonraki kalibrasyon/bakım tarihleri. |
| **Arıza kayıtları** | Cihaz arızalarının izlenmesi. |

**Kim kullanır:** Kalite, bakım, üretim.

**Menü yolu (eski):** Cihazlar → (liste, kayıtlar, kalibrasyon, bakım, arıza)

---

### 8. Doküman yönetimi

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Onaylı dokümanlar** | Prosedür, talimat, form arşivi. |
| **İptal edilen dokümanlar** | Yürürlükten kalkan dokümanların ayrı listesi. |

**Kim kullanır:** Kalite, yönetim; personel görüntüler.

**Menü yolu (eski):** Dokümanlar

---

### 9. İnsan kaynakları, eğitim, görevler

| Hizmet | Ne işe yarar? |
|--------|----------------|
| **Personel listesi** | Çalışan kayıtları. |
| **Eğitimler** | Kim hangi eğitimi aldı; istatistikler. |
| **Etkinlikler / görev takvimi** | Toplantı, hatırlatma, tamamlanan görevler. |

**Kim kullanır:** İK, kalite (yetkinlik), yönetim.

**Menü yolu (eski):** İnsan Kaynakları · Etkinlikler · Görevler

---

## Basit iş akışı (sipariş odaklı)

```
Müşteri siparişi (PO)
        ↓
    İş paketi açılır
        ↓
    Kalemler girilir (PO satırları)
        ↓
    Üretim  ←→  Kalite (NCR, FAI, ölçüm …)
        ↓
    Sevkiyat
        ↓
    Fatura (muhasebe)
```

Kalite adımları üretim ve sevkiyat boyunca **aynı iş paketine bağlı** devreye girer.

---

## Kim neye bakıyor?

| Rol | En çok kullandığı alanlar |
|-----|---------------------------|
| Planlama / proje | İş paketleri, kalemler, termin |
| Üretim | İş paketi detayı, malzeme, uygunsuzluk bildirimi |
| Kalite | NCR, CAPA, FAI, GKK, ölçüm formları |
| Depo / sevkiyat | Sevkiyat listesi, stok |
| Satın alma | Alım emirleri, malzemeler |
| Muhasebe | Kesilen / alım faturaları |
| Yönetim | Raporlar, istatistikler, fiyat alanları |

---

## MonitraNG ile ilişki (özet)

| Eski Kalite alanı | MonitraNG karşılığı (hedef) |
|------------------|----------------------------|
| İş paketi + kalemler | **Odak Sipariş** modülü + dataset |
| Üretim durumu, NCR, CAPA | **Operation Core** workspace (Odak Üretim) |
| Sevkiyat | Faz 2 — dataset veya OC |
| Malzeme, alım emri, fatura | İleride ayrı modül / entegrasyon |
| Cihaz, eğitim, doküman | KYS — sipariş projesi kapsamı dışı (ayrı plan) |

Faz ayrımı detayı: [FAZ_PLANI.md](./FAZ_PLANI.md)

---

## İlgili dokümanlar

- Teknik özet: [LEGACY_KALITE_OVERVIEW.md](./LEGACY_KALITE_OVERVIEW.md)
- Ekran eşlemesi: [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md)
- Mimari: [MIMARI_KARAR.md](./MIMARI_KARAR.md)
