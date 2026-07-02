# Odak Sipariş Planlama — planlama ve dokümantasyon

**Son güncelleme:** 15 Haziran 2026  
**Durum:** DG-only toplu migrasyon tamamlandı (~%99,7 kalem) · Hub UI deploy bekliyor

---

## Amaç

Odak müşterisinin eski **Kalite** (CakePHP) uygulamasındaki **müşteri sipariş / iş paketi** süreçlerini MonitraNG üzerinde karşılamak:

- Eski veriyi migrate etmek
- Kullanıcı direncini azaltmak (**tanıdık ekran düzeni + terimler**)
- Operation Core workspace’i **süreç motoru** olarak kullanmak
- Tablo ağırlıklı iş nesnelerini (kalemler, stok hareketleri) **DG dataset** katmanında tutmak

**Kapsam dışı (şimdilik):** Tam KYS (eğitim, denetim, cihaz kalibrasyonu), ERP faturalama, tedarik alım emirleri — ayrı faz veya modül.

---

## İlgili kaynaklar

| Kaynak | Rol |
|--------|-----|
| **[FONKSIYONEL_HARITA.md](./FONKSIYONEL_HARITA.md)** | **Eski uygulama hizmetleri** (teknik olmayan özet) |
| **[FAZ_PLANI.md](./FAZ_PLANI.md)** | **Yapılacaklar / yapılmayacaklar** faz faz |
| [LEGACY_KALITE_OVERVIEW.md](./LEGACY_KALITE_OVERVIEW.md) | Eski uygulama özeti, sunucu, veritabanı |
| [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md) | Ekran → MonitraNG UX eşlemesi (checklist) |
| [MIMARI_KARAR.md](./MIMARI_KARAR.md) | Workspace + dataset hibrit modeli |
| [DOKUMAN_PAKETI_NOTU.md](./DOKUMAN_PAKETI_NOTU.md) | **Döküman paketi** fikri (NAS yükü · taslak) |
| [DEVAM.md](./DEVAM.md) | Oturum checkpoint · Faz 1 ilerleme |
| **[CANLI_GECIS_KAPSAMI.md](./CANLI_GECIS_KAPSAMI.md)** | **Canlı geçiş BLOCKER/WARN · sprint · scriptler** |
| [current_status.md](./current_status.md) | Son oturum durumu (1 Tem 2026) |
| [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md) | Tablo → hedef eşlemesi |
| [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md) | **Lokal çalışan ortam** (PHP+MySQL, WSL/Docker yok) |
| [DOCKER_LOCAL_PLAN.md](./DOCKER_LOCAL_PLAN.md) | Docker Desktop (WSL gerekir — bu makinede bloklu) |
| [../is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md](../is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md) | Mevcut Odak Üretim OC workspace taslağı |

---

## Eski sistem — kısa özet

| Öğe | Değer |
|-----|--------|
| Uygulama | **Kalite** · CakePHP 3.10 |
| Kaynak sunucu | `192.168.20.30` · `/home/odak/html/kalite/` |
| Web | `http://192.168.20.30/kalite/` |
| Veritabanı | MySQL `kalite` (~825 iş paketi, ~2769 kalem, ~3776 sevkiyat) |
| Sipariş merkezi | **Planlama → İş Paketleri** (`packages` + `packageitems`) |

---

## Hedef MonitraNG deseni (özet)

```
Kullanıcıya:  Odak Sipariş modülü (tanıdık menü + liste + sekmeli detay)
Arka planda:  OC workspace (durum akışı, NCR/CAPA, otomasyon)
Veri:         DG master + sipariş kalemleri / sevkiyat dataset'leri
```

Detay: [MIMARI_KARAR.md](./MIMARI_KARAR.md)

---

## Klasör yapısı (hedef)

```
docs/odak/siparis/
├── README.md
├── DEVAM.md
├── FONKSIYONEL_HARITA.md   ← eski app hizmet haritası
├── FAZ_PLANI.md            ← fazlar, yapılacak / yapılmayacak
├── LEGACY_KALITE_OVERVIEW.md
├── UX_UYUMLULUK_HARITASI.md
├── MIMARI_KARAR.md
├── VERI_MIGRASYON_PLANI.md
├── DOCKER_LOCAL_PLAN.md
├── DOKUMAN_PAKETI_NOTU.md
├── datasets/
│   ├── odak_is_paketleri_dataset.json
│   ├── odak_siparis_kalemleri_dataset.json
│   └── *_automated_form.json
├── scripts/
│   ├── setup-odak-siparis-datasets.ps1
│   ├── migrate-legacy-from-sql-dump.ps1
│   ├── migrate-remaining-lines.ps1
│   └── verify-legacy-dg-migration.ps1
├── referans/          (ileride: wireframe, alan sözlüğü)
└── docker/            (Compose sablonlari)
```

---

## Sonraki adımlar

1. **Hub UI deploy** — DG-only paket listesi/detay (`Mng.Ui/pages/apps/odak-siparis/`)
2. Kullanıcı walkthrough + UX geri bildirimi
3. Git commit (script + dataset + UI refactor)
4. Kalan 8 kalem manuel inceleme · eksik paket `"9"`
5. MO `workItemId` entegrasyonu (sonraki faz)

Checkpoint: [DEVAM.md](./DEVAM.md)
