# Dataset Fixing — Kararlar

**Son güncelleme:** 10 Haziran 2026

---

## D-01 — DG backend'de isAdmin / sistem dataset koruması yok

| | |
|---|---|
| **Durum** | ✅ Kararlandı (mevcut) |
| **Tarih** | Bilinçli tasarım kararı (10 Haz 2026'da teyit edildi) |
| **Karar** | MngDataGateway Update/Delete uçlarında `isSystemCategory` veya isAdmin kontrolü **eklenmeyecek** |
| **Gerekçe** | Koruma UI katmanında yeterli; operasyonel/script erişimi admin token ile serbest kalmalı |
| **Etki** | Manager API'ye doğrudan erişirse teorik olarak sistem dataset'ini değiştirebilir — pratikte UI dışı erişim beklenmiyor |

---

## D-02 — UI deploy onayı

| | |
|---|---|
| **Durum** | ✅ Kararlandı |
| **Tarih** | 10 Haziran 2026 |
| **Karar** | Dataset fixing kapsamında UI deploy **yalnızca kullanıcı talebi ile** |
| **Gerekçe** | Sahaya çıkış öncesi UI değişikliği ayrı onay süreci |

---

## D-03 — Backend deploy (Odak)

| | |
|---|---|
| **Durum** | ✅ Kararlandı |
| **Tarih** | 10 Haziran 2026 |
| **Karar** | Backend servis deploy'u (özellikle `mngdatagateway`) agent tarafından otomatik yapılabilir |
| **Not** | Bu çalışmanın ilk fazında backend **kod değişikliği beklenmiyor**; deploy ihtiyacı doğarsa uygulanır |

---

## D-04 — Kategori konsolidasyon modeli

| | |
|---|---|
| **Durum** | ✅ Uygulandi (Odak, 10 Haz 2026) |
| **Karar** | Modül bazlı sistem kategorileri + ReferenceDatasets / LegacyDatasets |
| **Manifest** | [scripts/category-taxonomy-manifest.json](./scripts/category-taxonomy-manifest.json) |

---

## D-05 — OperationCore dataset'lerinin sistem kategorisine alınması

| | |
|---|---|
| **Durum** | ✅ Uygulandi |
| **Aksiyon** | `OperationCoreDatasets.isSystemCategory = true` |

---

## D-07 — İş vs referans kategori ayrımı

| | |
|---|---|
| **Durum** | ✅ Uygulandi |
| **Karar** | `ulkeler`/`sehirler` → **ReferenceDatasets** |

---

## D-09 — K1–K5 onayları (10 Haz 2026)

| Karar | Secim |
|-------|--------|
| K1 Widget | S1 + S2 ayri (System Datasets + WidgetDatasets) |
| K2 Document Intelligence | Sistem kategorisi |
| K3 tm_* | LegacyDatasets |
| K4 tst_* | LegacyDatasets (AF form silme ayri adim) |
| K5 Reference | Manager duzenleyebilir (ReferenceDatasets, isSystemCategory: false) |

---

## D-06 — Legacy dataset silme

| | |
|---|---|
| **Durum** | ✅ Uygulandi (Odak, 10 Haz 2026) |
| **Script** | [cleanup-legacy-datasets-odak.ps1](./scripts/cleanup-legacy-datasets-odak.ps1) |
| **Silinen** | 14 schema, 3 AF form, 3 side menu, LegacyDatasets kategorisi |
| **Not** | MongoDB collection verisi schema silme ile kalir; gerekirse ayri veri temizligi |

---

## D-07 — İş vs referans kategori ayrımı

| | |
|---|---|
| **Durum** | ⏳ Önerildi |
| **Tarih** | 10 Haziran 2026 |
| **Karar** | `BusinessDatasets` yalnızca operasyonel master (`tedarikciler`); `ulkeler`/`sehirler` → yeni **ReferenceDatasets** |
| **Gerekçe** | Manager'ın AF iş verisi ile lookup sözlüğünü ayırt etmesi |

---

## D-08 — Kategorisiz dataset politikası

| | |
|---|---|
| **Durum** | ✅ Kararlandı |
| **Tarih** | 10 Haziran 2026 |
| **Karar** | Production'da kategorisiz dataset bırakılmaz; uygun kategori yoksa **yeni kategori oluşturulur** |
| **Etki** | 13 kategorisiz dataset Faz 3'te atanacak |

---

## D-04 (eski) — Kategori konsolidasyon modeli (arşiv)

<details>
<summary>Eski tek-kategori seçenekleri (superseded by CATEGORIES.md)</summary>

Seçenek A: Tek "System Datasets" · Seçenek B: Modül bazlı — **modül bazlı + anlamlı isimler** benimsendi.
</details>

---

## D-10 — Production uygulama

| | |
|---|---|
| **Durum** | ⏸ Ertelendi (10 Haz 2026) |
| **Gerekçe** | Erken; Odak hedeflerine ulaşıldı |
| **Sonraki** | Ayrı oturum, bakım penceresi, prod BaseUrl manifest |

---

## Karar Şablonu (yeni kararlar için)

```markdown
## D-XX — Başlık

| | |
|---|---|
| **Durum** | ⏳ Bekliyor / ✅ Kararlandı / ❌ Reddedildi |
| **Tarih** | |
| **Karar** | |
| **Gerekçe** | |
| **Etki** | |
```
