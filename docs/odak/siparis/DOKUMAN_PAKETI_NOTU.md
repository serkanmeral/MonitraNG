# Döküman paketi — taslak not

**Durum:** Fikir aşaması · sırası gelince detaylandırılacak  
**Tarih:** 13 Haziran 2026  
**Bağlam:** Eski Kalite’de NAS/klasör üzerinden manuel dosya ilişkilendirme yükü

---

## Problem (özet)

Eski uygulamada birçok işlem için kullanıcı:

1. Uygulamada kayıt açar (iş paketi, NCR, GKK, ürün vb.)
2. İlgili PDF/çizimi **NAS’taki doğru klasöre** elle kopyalar veya sabit adlandırma kuralına uydurur
3. Uygulama dosyayı “yüklemez”; **yol + isim kuralına** güvenerek link üretir

Bu akış her yeni iş / revizyon için tekrarlanır — müşteri geri bildirimi: *“yorucu”*.

---

## Kavram: Döküman paketi

**Döküman paketi**, önceden tanımlı bir **dosya/şablon setidir**. Paket merkezi depolamada yönetilir; iş kayıtları pakete değil, paket seçimi üzerinden dosyalara bağlanır.

| Bileşen | Açıklama |
|---------|----------|
| **Paket tanımı** | Ad, açıklama, kapsam (ör. iş paketi açılışı, NCR, sevkiyat) |
| **Paket üyeleri** | Pakete linklenmiş dosyalar (PDF, çizim, checklist, şablon vb.) |
| **Paket seçimi (UX)** | Kullanıcı tek tek dosya aramak yerine **tek tıkla paket seçer** |
| **Otomatik linkleme** | Seçilen paketin dosyaları ilgili **iş kaydına** (WI, dataset, NCR vb.) otomatik ilişkilendirilir |

**Hedef deneyim:** “Bu iş için hangi 5 dosyayı NAS’a koyayım?” sorusu yerine — “Standart sipariş açılış paketi” → bitti.

---

## Taslak kullanım örnekleri

| Senaryo | Paket (örnek ad) | Otomatik link hedefi |
|---------|------------------|----------------------|
| İş paketi açılışı | Standart müşteri sipariş paketi | İş paketi WI |
| PO revizyonu | PO revizyon ekleri paketi | Aynı WI · revizyon metadata |
| Uygunsuzluk | NCR form + kanıt listesi paketi | NCR work item |
| Ürün / revizyon | Rev A çizim seti paketi | Ürün master veya prod rev kaydı |
| Sevkiyat | Sevkiyat evrak paketi | Sevkiyat dataset kaydı |

*(Örnek isimler placeholder — müşteri walkthrough sonrası netleşecek.)*

---

## MonitraNG ile ilişki (yüksek seviye)

```
┌─────────────────────────────────────┐
│  Döküman paketi kataloğu (master)   │
│  Paket ↔ dosya referansları         │
└──────────────┬──────────────────────┘
               │ tek tık seçim
┌──────────────▼──────────────────────┐
│  İş kaydı (WI / dataset / NCR)      │
│  Otomatik dosya linkleri (instance) │
└─────────────────────────────────────┘
               │
┌──────────────▼──────────────────────┐
│  Merkezi dosya depolama             │
│  (Document Intelligence / object)   │
└─────────────────────────────────────┘
```

- **Faz 1b** tek dosya (PO PDF) ihtiyacını karşılar.
- **Döküman paketi** muhtemelen **Faz 1b sonrası** veya **Faz 2** civarı ayrı bir yapı taşı olarak ele alınır (NAS yükünü toplu çözmek için).

---

## Sonra detaylandırılacak konular

- [ ] Paket tanımını kim yönetir? (admin, KYS sorumlusu, workspace rolü)
- [ ] Paket **versiyonlama** (revizyon değişince paket v2 mi, dosya v2 mi?)
- [ ] Zorunlu vs opsiyonel paket üyeleri
- [ ] Aynı işe birden fazla paket seçilebilir mi?
- [ ] Paket şablonu vs gerçek müşteri dosyası (PO gibi dış kaynak)
- [ ] Eski NAS klasör yapısından paket migrasyonu / eşleme
- [ ] API ve UI yüzeyi (OC WI ekleri mi, ayrı modül mü?)
- [ ] Tam vs redacted dosya setleri (eski `polink` / `porlink` ayrımı)

---

## İlgili dokümanlar

- [MIMARI_KARAR.md](./MIMARI_KARAR.md) — dosya / API sınırları
- [FAZ_PLANI.md](./FAZ_PLANI.md) — Faz 1b PO PDF
- [LEGACY_KALITE_OVERVIEW.md](./LEGACY_KALITE_OVERVIEW.md) — eski NAS/klasör modeli
- [DEVAM.md](./DEVAM.md) — checkpoint
